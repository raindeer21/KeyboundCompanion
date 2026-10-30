namespace KeyboundClick;

internal interface IActivityKeyboard
{
    bool IsGameForeground { get; }
    bool HasUserInput(ushort ownedKey);
    bool Press(ushort key);
    bool Release(ushort key);
}

// Independently implemented from the documented foreground-key approach in
// SiGeoMa/ff14-keybound-brawler. No upstream source is incorporated.
internal sealed class AntiIdlePulse(IActivityKeyboard keyboard)
{
    private static readonly ushort[] Keys = [0x7C, 0x7D, 0xA0, 0xA1, 0x25, 0x27];
    private const long IntervalMs = 20_000;
    private long nextAt;
    private long releaseAt;
    private int index;
    private ushort heldKey;
    private bool armed;
    private bool active;

    public bool IsBusy => active || heldKey != 0;
    public int Completed { get; private set; }
    public string Status { get; private set; } = "等待自动模式";

    public void Update(long now, bool allowed)
    {
        if (!allowed || !keyboard.IsGameForeground || keyboard.HasUserInput(heldKey))
        {
            Cancel(now);
            if (heldKey == 0)
                Status = !allowed ? "等待允许输入" : !keyboard.IsGameForeground ? "后台暂停，切回游戏后恢复" : "正在手动操作，已暂停";
            return;
        }
        if (!armed)
        {
            armed = true;
            nextAt = now + IntervalMs;
            Status = "已就绪，约每 20 秒一次";
        }
        if (heldKey != 0)
        {
            if (now < releaseAt) return;
            if (!ReleaseHeld()) return;
            if (!active) return;
            if (++index == Keys.Length)
            {
                active = false;
                Completed++;
                nextAt = now + IntervalMs;
                Status = $"已完成 {Completed} 次防闲置输入";
            }
            return;
        }
        if (!active)
        {
            if (now < nextAt) return;
            active = true;
            index = 0;
        }
        if (!keyboard.Press(Keys[index]))
        {
            active = false;
            nextAt = now + IntervalMs;
            Status = "按键发送失败，稍后重试";
            return;
        }
        heldKey = Keys[index];
        releaseAt = now + 50;
        Status = "正在发送防闲置按键";
    }

    public void Cancel(long now)
    {
        active = false;
        armed = false;
        index = 0;
        nextAt = now + IntervalMs;
        ReleaseHeld();
    }

    private bool ReleaseHeld()
    {
        if (heldKey == 0) return true;
        if (!keyboard.Release(heldKey))
        {
            Status = "按键释放失败，正在重试";
            return false;
        }
        heldKey = 0;
        return true;
    }
}
