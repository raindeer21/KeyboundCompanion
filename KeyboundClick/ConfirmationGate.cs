namespace KeyboundClick;

// One click per visible popup, after its eligibility has stayed stable for a second.
internal sealed class ConfirmationGate
{
    private long popup;
    private long? readySince;
    private bool clicked;

    public bool TryAcquire(long now, long identity, bool eligible)
    {
        if (identity != popup)
        {
            popup = identity;
            clicked = false;
            readySince = null;
        }
        if (identity == 0 || !eligible)
        {
            readySince = null;
            return false;
        }
        if (clicked) return false;
        readySince ??= now;
        if (now - readySince.Value < 1000) return false;
        clicked = true;
        return true;
    }
}

internal static class ConfirmationPolicy
{
    public static bool CanCommence(bool enabled, byte contentType, uint poppedDutyId, string? buttonText) =>
        enabled && contentType == 2 && poppedDutyId == 1095 && buttonText?.Trim() == "出发";

    public static bool CanExit(bool enabled, bool inDuty, string? buttonText) =>
        enabled && inDuty && buttonText?.Trim() == "退出";
}
