namespace KeyboundClick;

// One shared gate for all submission sources. Timestamp units are Stopwatch ticks.
internal sealed class SubmissionCooldown(long ticksPerSecond)
{
    private long nextAllowedAt;

    internal int RemainingMs(long now) => (int)Math.Ceiling(Math.Max(0, Volatile.Read(ref nextAllowedAt) - now) * 1000d / ticksPerSecond);

    internal bool TryAcquire(long now)
    {
        if (now < Volatile.Read(ref nextAllowedAt)) return false;
        Volatile.Write(ref nextAllowedAt, now + ticksPerSecond);
        return true;
    }

    // Measure from completion as well: a slow native call cannot shorten the next gap.
    // Also runs when the native call returns false or throws.
    internal void Complete(long now) => Volatile.Write(ref nextAllowedAt, now + ticksPerSecond);
}
