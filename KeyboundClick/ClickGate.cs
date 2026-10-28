namespace KeyboundClick;

internal sealed class ClickGate
{
    private int pending;
    internal bool IsPending => Volatile.Read(ref pending) != 0;
    internal bool Request() => Interlocked.CompareExchange(ref pending, 1, 0) == 0;
    internal bool Consume() => Interlocked.Exchange(ref pending, 0) == 1;
    internal void Cancel() => Interlocked.Exchange(ref pending, 0);
}
