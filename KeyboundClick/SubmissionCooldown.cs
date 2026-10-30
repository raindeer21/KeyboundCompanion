namespace KeyboundClick;

// One shared gate for all submission sources. Timestamp units are Stopwatch ticks.
internal sealed class SubmissionCooldown(long ticksPerSecond)
{
    private long nextAllowedAt;
    internal bool ProtectionEnabled { get; set; } = true;

    internal int RemainingMs(long now) => ProtectionEnabled
        ? (int)Math.Ceiling(Math.Max(0, Volatile.Read(ref nextAllowedAt) - now) * 1000d / ticksPerSecond)
        : 0;

    internal bool TryAcquire(long now)
    {
        if (ProtectionEnabled && now < Volatile.Read(ref nextAllowedAt)) return false;
        // Keep tracking while disabled so re-enabling protects the most recent submission.
        Volatile.Write(ref nextAllowedAt, now + ticksPerSecond);
        return true;
    }

    // Measure from completion as well: a slow native call cannot shorten the next gap.
    // Also runs when the native call returns false or throws.
    internal void Complete(long now) => Volatile.Write(ref nextAllowedAt, now + ticksPerSecond);
}
