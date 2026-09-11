namespace STS2Philosophers;

internal static class WesternActOneCandidatePolicy
{
    public const string GenerationKey = "WESTERN_ACT_ONE_THINKERS";
    public const int CandidateCount = 3;
    private static readonly IReadOnlyList<string[]> Combinations = CreateCombinations();

    public static GeneratedCandidates GetOrGenerate(PhilosophyRunState runState, ulong randomValue)
    {
        ArgumentNullException.ThrowIfNull(runState);
        if (runState.GeneratedCandidates.TryGetValue(GenerationKey, out GeneratedCandidates? existing)
            && existing is not null && IsValid(existing))
        {
            return existing;
        }

        // Twenty unordered triples: one invitation per person, including Socrates.
        GeneratedCandidates generated = new()
        {
            GenerationKey = GenerationKey,
            CandidateIds = Combinations[(int)(randomValue % (ulong)Combinations.Count)].ToList(),
        };
        runState.GeneratedCandidates[GenerationKey] = generated;
        return generated;
    }

    public static bool CanChooseProblem(GeneratedCandidates candidates, string thinkerId, string problemId) =>
        IsValid(candidates)
        && candidates.CandidateIds.Contains(thinkerId, StringComparer.Ordinal)
        && WesternRouteCatalog.IsEntryProblem(thinkerId, problemId);

    private static bool IsValid(GeneratedCandidates candidates) =>
        candidates.GenerationKey == GenerationKey
        && candidates.CandidateIds is not null
        && candidates.CandidateIds.Count == CandidateCount
        && candidates.CandidateIds.Distinct(StringComparer.Ordinal).Count() == CandidateCount
        && candidates.CandidateIds.All(id => WesternRouteCatalog.EntryThinkerIds.Contains(id, StringComparer.Ordinal));

    private static IReadOnlyList<string[]> CreateCombinations()
    {
        IReadOnlyList<string> thinkers = WesternRouteCatalog.EntryThinkerIds;
        List<string[]> combinations = [];
        for (int first = 0; first < thinkers.Count - 2; first++)
        for (int second = first + 1; second < thinkers.Count - 1; second++)
        for (int third = second + 1; third < thinkers.Count; third++)
        {
            combinations.Add([thinkers[first], thinkers[second], thinkers[third]]);
        }
        return combinations;
    }
}
