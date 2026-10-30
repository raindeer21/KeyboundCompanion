using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KeyboundClick;

public sealed unsafe partial class Plugin
{
    private readonly ConfirmationGate commenceGate = new();
    private readonly ConfirmationGate exitGate = new();
    private string confirmationStatus = "等待魔光键影的出发或结算窗口";
    private int commenceClicks;
    private int exitClicks;
    private bool confirmationFault;

    // Runs independently of the combat window: the commence popup is outside the duty.
    private bool UpdateConfirmations(long now)
    {
        if (confirmationFault) return false;
        try
        {
            var confirm = Gui.GetAddonByName<AddonContentsFinderConfirm>("ContentsFinderConfirm");
            var confirmVisible = Client.IsLoggedIn && confirm != null && confirm->IsVisible;
            var finder = ContentsFinder.Instance();
            var commence = confirmVisible && confirm->IsReady ? confirm->CommenceButton : null;
            var commenceEvent = FindClickEvent((AtkUnitBase*)confirm, commence);
            var canCommence = confirmVisible && confirm->IsReady && finder != null && commenceEvent != null &&
                ConfirmationPolicy.CanCommence(configuration.AutoCommence,
                    (byte)finder->QueueInfo.PoppedQueueEntry.ContentType,
                    finder->QueueInfo.PoppedQueueEntry.Id, ButtonText(commence));
            if (commenceGate.TryAcquire(now, confirmVisible ? (long)confirm : 0, canCommence))
            {
                CancelActivity();
                DispatchClick((AtkUnitBase*)confirm, commenceEvent);
                commenceClicks++;
                confirmationStatus = $"已点击魔光键影“出发”（{commenceClicks} 次）";
                Log.Information("Clicked Keybound duty commence button");
                return true;
            }

            var result = Gui.GetAddonByName<AtkUnitBase>("KTGResult");
            var resultVisible = Client.IsLoggedIn && result != null && result->IsVisible;
            var exit = resultVisible && result->IsReady ? result->GetComponentButtonById(48) : null;
            var exitEvent = FindClickEvent(result, exit);
            var canExit = resultVisible && exitEvent != null &&
                ConfirmationPolicy.CanExit(configuration.AutoExitResults, window.InDuty, ButtonText(exit));
            if (exitGate.TryAcquire(now, resultVisible ? (long)result : 0, canExit))
            {
                CancelActivity();
                DispatchClick(result, exitEvent);
                exitClicks++;
                confirmationStatus = $"已点击魔光键影结算“退出”（{exitClicks} 次）";
                Log.Information("Clicked Keybound result exit button");
                return true;
            }
        }
        catch (Exception ex)
        {
            confirmationFault = true;
            confirmationStatus = "自动确认读取失败，重新勾选开关后重试";
            Log.Error(ex, "Keybound confirmation paused");
        }
        return false;
    }

    private static string? ButtonText(AtkComponentButton* button) =>
        button == null || button->ButtonTextNode == null ? null : VisibleText(button->ButtonTextNode->NodeText);

    private static AtkEvent* FindClickEvent(AtkUnitBase* addon, AtkComponentButton* button)
    {
        if (addon == null || button == null || button->OwnerNode == null || !button->IsEnabled ||
            !button->OwnerNode->IsVisible()) return null;
        var evt = button->OwnerNode->AtkEventManager.Event;
        // Only dispatch the button's registered click to its own addon.
        for (var count = 0; evt != null && count < 32; count++, evt = evt->NextEvent)
            if (evt->State.EventType == AtkEventType.ButtonClick && evt->Listener == (AtkEventListener*)addon)
                return evt;
        return null;
    }

    private static void DispatchClick(AtkUnitBase* addon, AtkEvent* registered)
    {
        var evt = *registered;
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, (int)evt.Param, &evt, &data);
    }
}
