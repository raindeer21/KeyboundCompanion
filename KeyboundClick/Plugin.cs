using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace KeyboundClick;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IGameGui Gui { get; private set; } = null!;
    [PluginService] internal static ISigScanner Scanner { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Signature = "40 55 56 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 8B 02";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool SubmitWordDelegate(nint agent, AtkValue* value);

    private readonly SubmitWordDelegate? submit;
    private readonly ClickGate gate = new();
    private readonly SubmissionCooldown cooldown = new(Stopwatch.Frequency);
    private Candidate[] candidates = [];
    private string status = "等待进入魔光键影";
    private string lastResult = "尚未提交";
    private string? initializationError;
    private readonly DutyWindowState window = new();
    private bool open { get => window.IsOpen; set => window.SetOpen(value); }
    private bool antiIdleEnabled = true;
    private int antiIdleMode; // 0: deliberate 1 + Enter; 1: function/direction keys.
    private string activityInputStatus = "等待副本输入框";
    private readonly AntiIdlePulse antiIdle = new(new WindowsActivityKeyboard());
    private readonly AntiIdleSubmit antiIdleSubmit;
    private readonly Configuration configuration;
    private long saveSettingsAt;
    private bool pausedByError;
    private bool disposed;
    private uint requestedTerritory;
    private long requestedAt;
    private long refreshedAt;
    private bool ready;
    private int submissionCount;
    private bool automatic;
    private int intervalMs = 1000;
    private readonly IntervalSchedule schedule = new(Random.Shared);

    public Plugin()
    {
        antiIdleSubmit = new(new WindowsActivityKeyboard(), cooldown);
        try { configuration = Pi.GetPluginConfig() as Configuration ?? new Configuration(); }
        catch (Exception ex)
        {
            configuration = new Configuration();
            Log.Warning(ex, "Could not load KeyboundClick settings; using defaults");
        }
        configuration.Normalize();
        automatic = configuration.Automatic;
        intervalMs = configuration.IntervalMs;
        antiIdleEnabled = configuration.AntiIdleEnabled;
        antiIdleMode = configuration.AntiIdleMode;
        schedule.Reset(Environment.TickCount64, intervalMs);
        try
        {
            if (Scanner.TryScanText(Signature, out var address))
                submit = Marshal.GetDelegateForFunctionPointer<SubmitWordDelegate>(address);
            else initializationError = "当前游戏版本的词条接口未匹配，提交已禁用";
        }
        catch (Exception ex)
        {
            initializationError = "词条接口初始化失败";
            Log.Error(ex, initializationError);
        }
        Commands.AddHandler("/ktgclick", new CommandInfo((_, _) => open = !open) { HelpMessage = "打开或关闭键影随行窗口" });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Log.Information("KeyboundClick loaded; signature resolved: {Resolved}", submit != null);
    }

    private void Open() => open = true;

    private static bool IsInDuty()
    {
        var main = GameMain.Instance();
        return Client.IsLoggedIn && main != null && main->CurrentContentFinderConditionId == 1095;
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        var now = Environment.TickCount64;
        try
        {
            if (saveSettingsAt != 0 && now >= saveSettingsAt) SaveSettings();
            // Observe duty transitions even while the window is hidden.
            window.Observe(IsInDuty());
            if (!window.CanExecute || pausedByError)
            {
                ready = false;
                candidates = [];
                if (!window.InDuty)
                {
                    pausedByError = false;
                    status = "当前不在魔光键影，可提前调整设置。";
                }
                gate.Cancel();
                antiIdle.Cancel(now);
                antiIdleSubmit.Cancel(now, Stopwatch.GetTimestamp());
                schedule.Reset(now, intervalMs);
                return;
            }
            var clicked = gate.Consume(); // Consume once; a failed request is never queued.
            var timed = automatic && schedule.IsDue(now);
            if (timed) schedule.Reset(now, intervalMs); // No catch-up bursts after a stalled frame.
            if (clicked || timed || now - refreshedAt >= 150)
            {
                Refresh();
                refreshedAt = now;
            }
            var wasPulsing = antiIdle.IsBusy || antiIdleSubmit.IsBusy;
            // Activity protection also runs between enemy spawns. Input/UI checks below
            // still guard every key, independently of whether a target currently exists.
            var allowActivity = antiIdleEnabled && automatic && initializationError == null;
            long inputSession = 0;
            var inputText = allowActivity ? ReadActivityInput(out inputSession) : null;
            if (antiIdleMode == 0)
            {
                antiIdle.Cancel(now);
                if (clicked && !antiIdleSubmit.IsBusy) antiIdleSubmit.Cancel(now, Stopwatch.GetTimestamp());
                else antiIdleSubmit.Update(now, Stopwatch.GetTimestamp(), allowActivity, inputText, inputSession);
            }
            else
            {
                antiIdleSubmit.Cancel(now, Stopwatch.GetTimestamp());
                if (clicked) antiIdle.Cancel(now);
                else antiIdle.Update(now, allowActivity && inputText == "");
            }
            if (antiIdle.IsBusy || antiIdleSubmit.IsBusy || wasPulsing)
            {
                if (clicked) lastResult = "防闲置输入中，本次点击已取消，请稍后重试";
                return;
            }
            if (!clicked && !timed) return;
            if (!open || (clicked && (Client.TerritoryType != requestedTerritory || now - requestedAt > 1500)))
            {
                lastResult = "本次点击已取消：场景或窗口发生变化";
                return;
            }
            if (!ready || submit == null)
            {
                if (clicked) lastResult = "未提交：" + status;
                return;
            }
            var selected = TargetPolicy.Choose(candidates, Random.Shared);
            if (selected == null) { lastResult = "未提交：没有有效敌人"; return; }
            var agent = AgentModule.Instance()->GetAgentByInternalId((AgentId)398);
            if (agent == null) { lastResult = "未提交：输入界面已关闭"; return; }
            var bytes = Encoding.UTF8.GetBytes(selected.Word + '\0');
            bool matched;
            fixed (byte* text = bytes)
            {
                var value = new AtkValue { Type = AtkValueType.String };
                value.String.Value = text;
                if (!cooldown.TryAcquire(Stopwatch.GetTimestamp()))
                {
                    if (clicked) lastResult = "未提交：两次提交至少间隔 1 秒";
                    return; // Drop early clicks; never queue them for later.
                }
                submissionCount++;
                try
                {
                    matched = submit((nint)agent, &value); // The ONLY submission site, shared by manual and automatic modes.
                }
                finally
                {
                    cooldown.Complete(Stopwatch.GetTimestamp());
                    schedule.Reset(Environment.TickCount64, intervalMs);
                }
            }
            lastResult = $"{(clicked ? "手动" : "自动")}第 {submissionCount} 次：{(selected.IsBoss ? "首领" : "随机敌人")} · {selected.Label}\n词条：{selected.Word}\n{(matched ? "已匹配词条" : "词条未匹配，本次已结束")}";
            Log.Information("Click {Count}: entity={Entity:X}, base={Base}, boss={Boss}, matched={Matched}, word={Word}", submissionCount, selected.EntityId, selected.DataId, selected.IsBoss, matched, selected.Word);
        }
        catch (Exception ex)
        {
            ready = false;
            pausedByError = true;
            antiIdle.Cancel(now);
            antiIdleSubmit.Cancel(now, Stopwatch.GetTimestamp());
            status = "读取失败，提交已暂停";
            lastResult = "本次操作已结束";
            Log.Error(ex, "KeyboundClick read/submit failed; no retry scheduled");
        }
    }

    private static readonly string[] BlockingAddons =
    [
        "SelectYesno", "SelectString", "SelectIconString", "SelectOk", "ContextMenu",
        "ContentsFinderConfirm", "ContentsFinder", "KTGResult", "KTGGameOver", "_KTGGameover"
    ];

    private string? ReadActivityInput(out long inputSession)
    {
        inputSession = 0;
        foreach (var name in BlockingAddons)
        {
            var dialog = Gui.GetAddonByName<AtkUnitBase>(name);
            if (dialog != null && dialog->IsVisible) return ActivityBlocked("确认框或结算界面打开，已暂停");
        }
        var agents = AgentModule.Instance();
        if (agents == null) return ActivityBlocked("等待游戏界面");
        var hud = (KtgHud*)agents->GetAgentByInternalId((AgentId)397);
        var inputAgent = (KtgInput*)agents->GetAgentByInternalId((AgentId)398);
        if (hud == null || hud->IsShown == 0 || hud->HasDirector == 0 || inputAgent == null || inputAgent->Hud != hud) return ActivityBlocked("等待副本输入界面");
        var player = Objects.LocalPlayer;
        if (player == null || player.IsDead || player.StatusList.Any(effect => effect.StatusId == 5347)) return ActivityBlocked("当前禁止输入，已暂停");
        var addon = Gui.GetAddonByName<AtkUnitBase>("KTGInput");
        if (addon == null || !addon->IsVisible) return ActivityBlocked("等待输入框显示");
        var node = addon->GetComponentNodeById(7);
        if (node == null || node->Component == null || node->Component->GetComponentType() != ComponentType.TextInput) return ActivityBlocked("未找到可用输入框");
        var input = (AtkComponentTextInput*)node->Component;
        if (!input->Enabled || input->OwnerAddon != addon) return ActivityBlocked("输入框暂不可用");
        var ui = RaptureAtkModule.Instance();
        if (ui == null) return ActivityBlocked("等待输入焦点");
        var target = ui->AtkModule.TextInput.TargetTextInputEventInterface;
        if (target != null && target != &input->AtkTextInputEventInterface) return ActivityBlocked("其他输入框正在使用，已暂停");
        var raw = VisibleText(input->RawString);
        var evaluated = VisibleText(input->EvaluatedString);
        if (raw == null || evaluated == null || (raw.Length != 0 && evaluated.Length != 0 && raw != evaluated)) return ActivityBlocked("输入内容正在变化，已暂停");
        inputSession = (long)input;
        activityInputStatus = "输入框已就绪";
        return raw.Length != 0 ? raw : evaluated;
    }

    private string? ActivityBlocked(string message) { activityInputStatus = message; return null; }

    private static string? VisibleText(Utf8String text)
    {
        if (text.BufUsed <= 1) return "";
        if (text.StringPtr.Value == null || text.BufUsed > 4096) return null;
        return SeString.Parse(new ReadOnlySpan<byte>(text.StringPtr, (int)text.BufUsed - 1)).TextValue;
    }

    private void Refresh()
    {
        ready = false;
        candidates = [];
        if (initializationError != null) { status = initializationError; return; }
        var main = GameMain.Instance();
        if (!Client.IsLoggedIn || main == null || main->CurrentContentFinderConditionId != 1095)
        { status = "等待进入魔光键影"; return; }
        var module = AgentModule.Instance();
        if (module == null) { status = "等待游戏界面"; return; }
        var hud = (KtgHud*)module->GetAgentByInternalId((AgentId)397);
        var input = module->GetAgentByInternalId((AgentId)398);
        // KTGInput is opened by the HUD agent, not AgentInterface.AddonId.
        // Consequently AgentKTGInput.IsAgentActive() returns false even during live play.
        if (hud == null || hud->IsShown == 0 || hud->HasDirector == 0 || input == null || ((KtgInput*)input)->Hud != hud)
        { status = "等待词条界面"; return; }
        // HUD Stage is a wave/UI state, not the director's GamePhase. Live CN play uses Stage=2.
        var addon = Gui.GetAddonByName<AtkUnitBase>("KTGInput");
        if (addon == null || !addon->IsVisible) { status = "等待输入界面"; return; }
        var player = Objects.LocalPlayer;
        if (player == null || player.IsDead) { status = "当前无法输入"; return; }
        foreach (var effect in player.StatusList)
        {
            if (effect.StatusId == 5347)
            { status = "当前禁止打字"; return; }
        }
        if (hud->Entities.Count is < 0 or > 128) { status = "词条数据异常"; return; }
        var found = new List<Candidate>();
        foreach (var pair in hud->Entities)
        {
            var entity = pair.Item2;
            if (entity.Disposed != 0 || entity.IsVisible == 0) continue;
            var obj = Objects.SearchByEntityId(entity.EntityId);
            if (obj == null || obj.IsDead || !obj.IsTargetable) continue;
            if (!EnemyKinds.IsEnemy(obj.BaseId, entity.Kind)) continue;
            var name = ReadText(entity.Name);
            var title = ReadText(entity.Title);
            var word = !string.IsNullOrWhiteSpace(name) ? name : title;
            if (string.IsNullOrWhiteSpace(word)) continue;
            found.Add(new Candidate(entity.EntityId, obj.BaseId, obj.Name.TextValue, word, entity.Kind, EnemyKinds.IsBoss(obj.BaseId, entity.Kind)));
        }
        candidates = found.ToArray();
        status = candidates.Length == 0 ? "当前没有可提交的敌人词条" : $"可选敌人 {candidates.Length} 个 · 首领 {candidates.Count(c => c.IsBoss)} 个";
        ready = candidates.Length > 0;
    }

    private static string ReadText(Utf8String text)
    {
        if (text.StringPtr.Value == null || text.BufUsed <= 1 || text.BufUsed > 4096) return "";
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(text.StringPtr, (int)text.BufUsed - 1));
    }

    private void Draw()
    {
        if (!open) { PauseExecution(); return; }
        ImGui.SetNextWindowSize(new Vector2(460, 410), ImGuiCond.FirstUseEver);
        var visible = open;
        if (ImGui.Begin("键影随行###KeyboundClick", ref visible))
        {
            ImGui.TextUnformatted("优先首领；没有首领时随机选择敌人。");
            ImGui.TextWrapped(status);
            ImGui.Spacing();
            var remainingMs = cooldown.RemainingMs(Stopwatch.GetTimestamp());
            ImGui.BeginDisabled(!window.InDuty || !ready || gate.IsPending || remainingMs > 0 || antiIdle.IsBusy || antiIdleSubmit.IsBusy);
            if (ImGui.Button("提交一次", new Vector2(-1, 48)))
            {
                requestedTerritory = Client.TerritoryType;
                requestedAt = Environment.TickCount64;
                gate.Request();
            }
            ImGui.EndDisabled();
            ImGui.TextDisabled(remainingMs > 0 ? $"冷却中：剩余 {remainingMs / 1000f:F1} 秒" : "手动和自动共用冷却，两次提交至少间隔 1 秒。");
            ImGui.Spacing();
            if (ImGui.Checkbox("自动间隔提交", ref automatic))
            {
                pausedByError = false;
                schedule.Reset(Environment.TickCount64, intervalMs);
                if (!automatic) CancelActivity();
                SaveSettings();
            }
            ImGui.SetNextItemWidth(250);
            if (ImGui.SliderInt("延迟", ref intervalMs, 1000, 10000, "%d 毫秒"))
            {
                intervalMs = Math.Clamp(intervalMs, 1000, 10000);
                schedule.Reset(Environment.TickCount64, intervalMs);
                saveSettingsAt = Environment.TickCount64 + 500;
            }
            ImGui.TextDisabled($"每次随机：{Math.Max(1000, (int)Math.Ceiling(intervalMs * .8))}～{(int)Math.Floor(intervalMs * 1.2)} 毫秒（±20%，最低 1 秒）");
            ImGui.TextWrapped(automatic ? "自动设置已开启；副本外或关闭窗口时暂停，进本打开后恢复。" : "自动设置已关闭；开关和间隔会自动保存。");
            if (ImGui.Checkbox("防闲置（仅自动模式、游戏前台）", ref antiIdleEnabled))
            {
                if (!antiIdleEnabled) CancelActivity();
                SaveSettings();
            }
            if (antiIdleEnabled)
            {
                if (ImGui.Combo("防闲置方式", ref antiIdleMode, ["错误输入 1 + Enter", "功能键与方向键"], 2))
                {
                    CancelActivity();
                    SaveSettings();
                }
                ImGui.TextWrapped(antiIdleMode == 0
                    ? "约每 20 秒输入 1 并回车；与所有提交共用至少 1 秒冷却，可能影响正确率。"
                    : "约每 20 秒发送功能键和左右方向键；不额外提交词条。");
                ImGui.TextDisabled(!window.InDuty ? "副本外暂停防闲置" : automatic ? (antiIdleMode == 0 ? antiIdleSubmit.Status : antiIdle.Status) : "等待开启自动模式");
                if (window.InDuty && automatic && activityInputStatus != "输入框已就绪") ImGui.TextWrapped(activityInputStatus);
            }
            ImGui.Separator();
            ImGui.TextWrapped(lastResult);
            if (ImGui.CollapsingHeader("当前候选目标"))
                foreach (var c in candidates)
                    ImGui.BulletText($"{(c.IsBoss ? "首领" : "普通")} · {c.Label} → {c.Word}");
        }
        ImGui.End();
        open = visible;
        if (!open) PauseExecution();
    }

    private void SaveSettings()
    {
        configuration.Automatic = automatic;
        configuration.IntervalMs = intervalMs;
        configuration.AntiIdleEnabled = antiIdleEnabled;
        configuration.AntiIdleMode = antiIdleMode;
        configuration.Normalize();
        try
        {
            Pi.SavePluginConfig(configuration);
            saveSettingsAt = 0;
        }
        catch (Exception ex)
        {
            saveSettingsAt = Environment.TickCount64 + 5000;
            Log.Warning(ex, "Could not save KeyboundClick settings; retry scheduled");
        }
    }

    private void PauseExecution()
    {
        gate.Cancel();
        CancelActivity();
    }

    private void CancelActivity()
    {
        antiIdle.Cancel(Environment.TickCount64);
        antiIdleSubmit.Cancel(Environment.TickCount64, Stopwatch.GetTimestamp());
    }

    public void Dispose()
    {
        disposed = true;
        SaveSettings();
        PauseExecution();
        if (antiIdle.IsBusy || antiIdleSubmit.IsBusy)
        {
            CancelActivity();
            if (antiIdle.IsBusy || antiIdleSubmit.IsBusy) Log.Error("Unable to release an activity key during unload");
        }
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/ktgclick");
    }
}
