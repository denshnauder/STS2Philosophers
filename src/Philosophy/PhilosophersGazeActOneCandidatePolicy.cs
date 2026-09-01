namespace STS2Philosophers;

internal static class PhilosophersGazeActOneCandidatePolicy
{
    public const string GenerationKey = "ACT_ONE_THINKERS";
    public const string Kongzi = "KONGZI";
    public const string Mozi = "MOZI";
    public const string Laozi = "LAOZI";

    private static readonly string[] AllThinkers = [Kongzi, Mozi, Laozi];

    public static GeneratedCandidates GetOrGenerate(
        PhilosophyRunState runState,
        ulong randomValue)
    {
        if (runState.GeneratedCandidates.TryGetValue(
                GenerationKey,
                out GeneratedCandidates? existing)
            && IsValid(existing))
        {
            return existing;
        }

        int omittedIndex = (int)(randomValue % (ulong)AllThinkers.Length);
        GeneratedCandidates generated = new()
        {
            GenerationKey = GenerationKey,
            CandidateIds = AllThinkers
                .Where((_, index) => index != omittedIndex)
                .ToList(),
        };
        runState.GeneratedCandidates[GenerationKey] = generated;
        return generated;
    }

    public static bool Contains(GeneratedCandidates candidates, string thinkerId)
    {
        return candidates.CandidateIds.Contains(thinkerId, StringComparer.Ordinal);
    }

    private static bool IsValid(GeneratedCandidates candidates)
    {
        return string.Equals(candidates.GenerationKey, GenerationKey, StringComparison.Ordinal)
            && candidates.CandidateIds.Count == 2
            && candidates.CandidateIds.Distinct(StringComparer.Ordinal).Count() == 2
            && candidates.CandidateIds.All(candidate =>
                AllThinkers.Contains(candidate, StringComparer.Ordinal));
    }
}
