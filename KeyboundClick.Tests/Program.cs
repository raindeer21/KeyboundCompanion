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
Console.WriteLine("PASS: Boss priority, random fallback, clicks, capped ±20% timing, shared one-second cooldown, failed/slow calls, and no catch-up bursts.");
