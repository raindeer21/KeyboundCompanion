namespace KeyboundClick;

internal sealed class IntervalSchedule(Random random)
{
    internal long DueAt { get; private set; } = long.MaxValue;
    internal int LastDelayMs { get; private set; }
    internal void Reset(long now, int baseDelayMs)
    {
        var baseline = Math.Clamp(baseDelayMs, 1000, 10000);
        var minimum = Math.Max(1000, (int)Math.Ceiling(baseline * 0.8));
        var maximum = (int)Math.Floor(baseline * 1.2);
        LastDelayMs = random.Next(minimum, maximum + 1);
        DueAt = now + LastDelayMs;
    }
    internal bool IsDue(long now) => now >= DueAt;
}
