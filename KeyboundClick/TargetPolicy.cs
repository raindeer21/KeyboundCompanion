namespace KeyboundClick;

internal sealed record Candidate(uint EntityId, uint DataId, string Label, string Word, uint Kind, bool IsBoss);

internal static class TargetPolicy
{
    internal static Candidate? Choose(IReadOnlyList<Candidate> candidates, Random random)
    {
        if (candidates.Count == 0) return null;
        var bosses = candidates.Where(c => c.IsBoss).ToArray();
        return bosses.Length > 0 ? bosses[random.Next(bosses.Length)] : candidates[random.Next(candidates.Count)];
    }
}
