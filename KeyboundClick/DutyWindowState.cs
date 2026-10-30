namespace KeyboundClick;

internal sealed class DutyWindowState
{
    private bool observed;
    public bool InDuty { get; private set; }
    public bool IsOpen { get; private set; }
    public bool CanExecute => InDuty && IsOpen;

    public void Observe(bool inDuty)
    {
        // Loading/reloading inside the duty is not a new entry.
        if (observed && inDuty != InDuty) IsOpen = inDuty;
        InDuty = inDuty;
        observed = true;
    }

    public void SetOpen(bool value) => IsOpen = value;
}
