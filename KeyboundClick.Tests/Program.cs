using KeyboundClick;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var random = new Random(20261028);
var normal = new Candidate(1, 19586, "普通敌人", "神罗", 0, false);
var boss = new Candidate(2, 19596, "Boss", "克劳德", 1, true);
var pot = new Candidate(3, 19592, "魔法罐", "给我圣灵药！", 4, false);
for (var i = 0; i < 10000; i++) Check(TargetPolicy.Choose([normal, boss, pot], random) == boss, "Boss must always win");
Check(TargetPolicy.Choose([], random) == null, "Empty input must not produce an action");
var counts = new Dictionary<uint, int> { [1] = 0, [3] = 0 };
for (var i = 0; i < 10000; i++) counts[TargetPolicy.Choose([normal, pot], random)!.EntityId]++;
Check(counts.Values.All(n => n is > 4500 and < 5500), "Random fallback must sample both targets");
var gate = new ClickGate();
Check(!gate.Consume(), "No click must mean no submission");
Check(gate.Request() && !gate.Request(), "Duplicate pending clicks must be coalesced");
Check(gate.Consume() && !gate.Consume(), "One accepted click must be consumed only once");
gate.Request(); gate.Cancel(); Check(!gate.Consume(), "Cancelled clicks must never execute");
Check(EnemyKinds.IsBoss(19596, 1) && !EnemyKinds.IsBoss(19592, 4), "Boss must not be confused with the magic pot");
Check(!EnemyKinds.IsEnemy(19824, 2) && !EnemyKinds.IsEnemy(20146, 3), "Non-enemy objects must not be selected");
foreach (var baseline in new[] {100, 1000, 1100, 1250, 3333, 10000})
{
    var schedule = new IntervalSchedule(random);
    var unique = new HashSet<int>();
    for (var i = 0; i < 10000; i++)
    {
        var now = (long)i * 20000;
        schedule.Reset(now, baseline);
        var clamped = Math.Clamp(baseline, 1000, 10000);
        Check(schedule.LastDelayMs >= Math.Max(1000, Math.Ceiling(clamped * .8)) && schedule.LastDelayMs <= Math.Floor(clamped * 1.2), "Jitter outside capped ±20%");
        Check(!schedule.IsDue(now + schedule.LastDelayMs - 1) && schedule.IsDue(now + schedule.LastDelayMs), "Incorrect timer boundary");
        unique.Add(schedule.LastDelayMs);
    }
    Check(unique.Count > 10, "Interval must be rerandomized");
    schedule.Reset(100000000, baseline);
    Check(!schedule.IsDue(100000000), "Long stalls must not cause catch-up submissions");
}
var cooldown = new SubmissionCooldown(1000000);
Check(cooldown.TryAcquire(0), "First manual submission must be permitted");
Check(!cooldown.TryAcquire(999999), "Automatic submission cannot bypass a manual cooldown");
cooldown.Complete(250000); // Native call took 250 ms; cooldown is measured from completion.
Check(!cooldown.TryAcquire(1000000) && !cooldown.TryAcquire(1249999), "Slow native calls must not shorten the gap");
Check(cooldown.TryAcquire(1250000), "Submission must be allowed at exactly one second after completion");
Check(!cooldown.TryAcquire(1250000), "Same-frame manual/auto requests must produce only one submission");
cooldown.Complete(1250010); // Also the path used after a failed/throwing native call.
Check(!cooldown.TryAcquire(2250009) && cooldown.TryAcquire(2250010), "Failed submissions must also consume the cooldown");
Check(cooldown.RemainingMs(2250011) == 1000, "UI countdown must round up");
var window = new DutyWindowState();
window.Observe(false);
Check(!window.IsOpen, "Startup outside duty must stay hidden");
window.SetOpen(true);
Check(!window.IsOpen, "Commands must not open the window outside the duty");
window.Observe(true);
Check(window.IsOpen, "Entering the duty must open the window");
window.SetOpen(false);
window.Observe(true);
Check(!window.IsOpen, "Manual close must survive subsequent frames in the same duty");
window.SetOpen(true);
window.Observe(false);
Check(!window.IsOpen, "Leaving the duty must hide a manually reopened window");
window.Observe(true);
Check(window.IsOpen, "A later duty entry must open the window again");
var reloadWindow = new DutyWindowState();
reloadWindow.Observe(true);
Check(!reloadWindow.IsOpen, "Reloading inside the duty must not pop up a window at startup");

var keyboard = new FakeKeyboard();
var pulse = new AntiIdlePulse(keyboard);
pulse.Update(0, true);
pulse.Update(19_999, true);
Check(keyboard.Events.Count == 0, "No activity keys before the 20 second interval");
pulse.Update(20_000, true);
Check(pulse.IsBusy && keyboard.Held == 0x7C, "First pulse must press F13");
pulse.Update(20_049, true);
Check(keyboard.Held == 0x7C, "Keys must remain held for at least 50 ms across frames");
for (var i = 0; i < 6; i++)
{
    if (i > 0) pulse.Update(20_000 + i * 100, true);
    pulse.Update(20_050 + i * 100, true);
}
Check(pulse.Completed == 1 && !pulse.IsBusy && keyboard.Held == 0, "A full pulse must leave every key released");
Check(keyboard.Events.Where(e => e.Down).Select(e => e.Key).SequenceEqual(new ushort[] {0x7C, 0x7D, 0xA0, 0xA1, 0x25, 0x27}), "Activity pulse must use only the documented six keys, never Enter");
pulse.Update(40_549, true);
Check(keyboard.Events.Count == 12, "Next pulse must be 20 seconds after completion");
pulse.Update(40_550, true);
keyboard.Foreground = false;
pulse.Update(40_551, true);
Check(!pulse.IsBusy && keyboard.Held == 0 && keyboard.Events.Count == 14, "Focus loss must release the owned key without sending another key");
pulse.Update(100_000, true);
Check(keyboard.Events.Count == 14, "No keys may be pressed in another application");
keyboard.Foreground = true;
keyboard.UserTyping = true;
pulse.Update(100_001, true);
Check(keyboard.Events.Count == 14, "Physical user input must suppress activity keys");
keyboard.UserTyping = false;
pulse.Update(100_002, true);
pulse.Update(120_002, true);
pulse.Update(120_003, false);
Check(!pulse.IsBusy && keyboard.Held == 0, "Stopping, closing, dialogs or input locks must release the current key");
var eventsBefore = keyboard.Events.Count;
pulse.Update(500_000, true);
Check(keyboard.Events.Count == eventsBefore, "Resuming must not immediately catch up missed pulses");
pulse.Update(520_000, true);
keyboard.FailRelease = true;
pulse.Cancel(520_001);
Check(pulse.IsBusy && keyboard.Held != 0, "A failed key release must remain tracked");
keyboard.FailRelease = false;
pulse.Update(520_002, false);
Check(!pulse.IsBusy && keyboard.Held == 0, "Cancellation must retry a failed release");
keyboard.FailPress = true;
pulse.Update(530_000, true);
pulse.Update(550_000, true);
Check(!pulse.IsBusy && keyboard.Held == 0, "A failed key press must not create a stuck-key state");
var shared = new SubmissionCooldown(1000);
var numberKeyboard = new FakeKeyboard();
var wrongAnswer = new AntiIdleSubmit(numberKeyboard, shared);
wrongAnswer.Update(0, 0, true, "", 7);
Check(shared.TryAcquire(19_500), "Normal submission setup must succeed");
shared.Complete(19_500);
wrongAnswer.Update(20_000, 20_000, true, "", 7);
wrongAnswer.Update(20_050, 20_050, true, "1", 7);
wrongAnswer.Update(20_100, 20_100, true, "1", 7);
wrongAnswer.Update(20_499, 20_499, true, "1", 7);
Check(!numberKeyboard.Events.Any(e => e is { Key: 0x0D, Down: true }), "Error submission must respect the preceding normal submission's cooldown");
wrongAnswer.Update(20_500, 20_500, true, "1", 7);
Check(numberKeyboard.Held == 0x0D, "Enter is allowed only once the cooldown expires and 1 is observed");
wrongAnswer.Update(20_550, 20_550, true, "", 7);
wrongAnswer.Update(20_551, 20_551, true, "", 7);
Check(wrongAnswer.Completed == 1 && !wrongAnswer.IsBusy && numberKeyboard.Held == 0, "Error submission completes only after input clears and Enter is released");
Check(!shared.TryAcquire(21_550) && shared.TryAcquire(21_551), "Error submission must reserve a full second before the next normal/manual submission");

foreach (var interruption in new[] { "different text", "session", "foreground", "disabled", "timeout" })
{
    var backend = new FakeKeyboard();
    var attempt = new AntiIdleSubmit(backend, new SubmissionCooldown(1000));
    attempt.Update(0, 0, true, "", 7);
    attempt.Update(20_000, 20_000, true, "", 7);
    if (interruption == "foreground") backend.Foreground = false;
    attempt.Update(interruption == "timeout" ? 22_001 : 20_100, 22_001,
        interruption != "disabled", interruption == "different text" ? "玩家自己的文字" : "",
        interruption == "session" ? 8 : 7);
    Check(!attempt.IsBusy && backend.Held == 0, "Interruption must release digit key: " + interruption);
    Check(!backend.Events.Any(e => e is { Key: 0x0D, Down: true }), "Never press Enter without confirmed owned text: " + interruption);
}
var gapKeys = new FakeKeyboard();
var gapPulse = new AntiIdlePulse(gapKeys);
gapPulse.Update(0, true);
gapPulse.Update(10_000, false);
gapPulse.Update(19_999, true);
gapPulse.Update(20_000, true);
Check(gapPulse.IsBusy && gapKeys.Held == 0x7C, "Enemy/wave gaps must not restart the function-key timer");
gapPulse.Cancel(20_001);
var gapSubmitKeys = new FakeKeyboard();
var gapSubmit = new AntiIdleSubmit(gapSubmitKeys, new SubmissionCooldown(1000));
gapSubmit.Update(0, 0, true, "", 7);
gapSubmit.Update(10_000, 10_000, false, null, 0);
gapSubmit.Update(19_999, 19_999, true, "", 7);
gapSubmit.Update(20_000, 20_000, true, "", 7);
Check(gapSubmit.IsBusy && gapSubmitKeys.Held == 0x31, "Enemy/wave gaps must not restart the 1 + Enter timer");
gapSubmit.Cancel(20_001, 20_001);
Console.WriteLine("PASS: targets, shared normal/manual/error cooldown, confirmed error input, duty windows, both activity modes, interruptions, wave gaps and key cleanup.");

sealed class FakeKeyboard : IActivityKeyboard
{
    public bool Foreground = true;
    public bool UserTyping;
    public bool FailPress;
    public bool FailRelease;
    public ushort Held;
    public readonly List<(ushort Key, bool Down)> Events = [];
    public bool IsGameForeground => Foreground;
    public bool HasUserInput(ushort ownedKey) => UserTyping;
    public bool Press(ushort key)
    {
        if (FailPress || !Foreground) return false;
        if (Held != 0) throw new Exception("Two activity keys were held simultaneously");
        Held = key;
        Events.Add((key, true));
        return true;
    }
    public bool Release(ushort key)
    {
        if (FailRelease) return false;
        if (Held != key) throw new Exception("Attempted to release an unowned key");
        Held = 0;
        Events.Add((key, false));
        return true;
    }
}
