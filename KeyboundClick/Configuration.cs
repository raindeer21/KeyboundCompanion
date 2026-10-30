using Dalamud.Configuration;

namespace KeyboundClick;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Automatic { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public bool AntiIdleEnabled { get; set; } = true;
    public int AntiIdleMode { get; set; }
    public bool AutoCommence = true;
    public bool AutoExitResults = true;
    public bool SubmissionProtection = true;

    public void Normalize()
    {
        IntervalMs = Math.Clamp(IntervalMs, SubmissionProtection ? 1000 : 100, 10000);
        AntiIdleMode = Math.Clamp(AntiIdleMode, 0, 1);
    }
}
