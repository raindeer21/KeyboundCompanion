namespace KeyboundClick;

// Submit a deliberate "1" only after observing it in the minigame input field.
// Shares the same hard cooldown as every normal/manual submission.
internal sealed class AntiIdleSubmit(IActivityKeyboard keyboard, SubmissionCooldown cooldown)
{
    private enum Phase { Waiting, DigitHeld, AwaitDigit, AwaitCooldown, EnterHeld, AwaitClear }
    private Phase phase;
    private ushort heldKey;
    private bool armed;
    private bool reservedSubmission;
    private long nextAt;
    private long releaseAt;
    private long deadline;
    private long session;
    public bool IsBusy => phase != Phase.Waiting || heldKey != 0;
    public int Completed { get; private set; }
    public string Status { get; private set; } = "等待自动模式";

    public void Update(long now, long timestamp, bool allowed, string? text, long inputSession)
    {
        if (!allowed || text == null || !keyboard.IsGameForeground || keyboard.HasUserInput(heldKey) ||
            (phase != Phase.Waiting && inputSession != session))
        {
            Cancel(now, timestamp);
            if (heldKey == 0) Status = !allowed || text == null ? "等待允许输入" : !keyboard.IsGameForeground ? "后台暂停，切回游戏后恢复" : "输入状态变化，已暂停";
            return;
        }
        if (phase == Phase.Waiting)
        {
            if (heldKey != 0 && !ReleaseHeld()) return;
            CompleteCooldown(timestamp);
            if (text.Length != 0)
            {
                armed = false;
                Status = "输入框已有文字，等待清空";
                return;
            }
            if (!armed)
            {
                armed = true;
                nextAt = now + 20_000;
                Status = "已就绪，约每 20 秒输入 1 并回车";
            }
            if (now < nextAt) return;
            if (!keyboard.Press(0x31)) { Fail(now, timestamp, "数字键发送失败"); return; }
            heldKey = 0x31;
            session = inputSession;
            releaseAt = now + 50;
            deadline = now + 2_000;
            phase = Phase.DigitHeld;
            Status = "正在输入 1";
            return;
        }
        if (text.Length != 0 && text != "1")
        {
            Fail(now, timestamp, "输入内容变化，已取消回车");
            return;
        }
        if (now >= deadline)
        {
            Fail(now, timestamp, "未确认输入或提交，请检查输入框");
            return;
        }
        switch (phase)
        {
            case Phase.DigitHeld:
                if (now >= releaseAt && ReleaseHeld()) phase = Phase.AwaitDigit;
                break;
            case Phase.AwaitDigit:
                if (text == "1") { phase = Phase.AwaitCooldown; deadline = now + 3_000; }
                break;
            case Phase.AwaitCooldown:
                if (text != "1") { Fail(now, timestamp, "输入内容变化，已取消回车"); break; }
                if (!cooldown.TryAcquire(timestamp)) break;
                reservedSubmission = true;
                if (!keyboard.Press(0x0D)) { Fail(now, timestamp, "回车发送失败"); break; }
                heldKey = 0x0D;
                releaseAt = now + 50;
                deadline = now + 2_000;
                phase = Phase.EnterHeld;
                Status = "已按回车，等待提交确认";
                break;
            case Phase.EnterHeld:
                if (now >= releaseAt && ReleaseHeld()) phase = Phase.AwaitClear;
                break;
            case Phase.AwaitClear:
                if (text.Length == 0)
                {
                    CompleteCooldown(timestamp);
                    phase = Phase.Waiting;
                    nextAt = now + 20_000;
                    Completed++;
                    Status = $"已确认 {Completed} 次防闲置提交";
                }
                break;
        }
    }

    public void Cancel(long now, long timestamp)
    {
        ReleaseHeld();
        CompleteCooldown(timestamp);
        phase = Phase.Waiting;
        armed = false;
        nextAt = now + 20_000;
    }

    private void Fail(long now, long timestamp, string message)
    {
        Cancel(now, timestamp);
        if (heldKey == 0) Status = message;
    }

    private void CompleteCooldown(long timestamp)
    {
        if (!reservedSubmission) return;
        cooldown.Complete(timestamp);
        // A failed release still owns Enter; keep extending the cooldown until released.
        if (heldKey == 0) reservedSubmission = false;
    }

    private bool ReleaseHeld()
    {
        if (heldKey == 0) return true;
        if (!keyboard.Release(heldKey)) { Status = "按键释放失败，正在重试"; return false; }
        heldKey = 0;
        return true;
    }
}
