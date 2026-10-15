namespace STS2Philosophers;

internal static class PhilosophersGazeActOneCandidatePolicy
{
    public const string GenerationKey = "ACT_ONE_THINKERS";
    public const int CandidateCount = 3;
    public const string Kongzi = "KONGZI";
    public const string Mozi = "MOZI";
    public const string Laozi = "LAOZI";

    public static IReadOnlyList<string> RegisteredThinkerIds { get; } =
    [
        Kongzi,
        Mozi,
        Laozi,
        .. WesternRouteCatalog.EntryThinkerIds,
    ];

    private static readonly IReadOnlyList<string[]> Combinations = CreateCombinations();

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

        GeneratedCandidates generated = new()
        {
            GenerationKey = GenerationKey,
            CandidateIds = Combinations[(int)(randomValue % (ulong)Combinations.Count)].ToList(),
        };
        runState.GeneratedCandidates[GenerationKey] = generated;
        return generated;
    }

    public static bool Contains(GeneratedCandidates candidates, string thinkerId)
    {
        return IsValid(candidates)
            && candidates.CandidateIds.Contains(thinkerId, StringComparer.Ordinal);
    }

    public static bool CanChooseProblem(
        GeneratedCandidates candidates,
        string thinkerId,
        string problemId) =>
        Contains(candidates, thinkerId)
        && WesternRouteCatalog.IsEntryProblem(thinkerId, problemId);

    private static bool IsValid(GeneratedCandidates candidates)
    {
        return string.Equals(candidates.GenerationKey, GenerationKey, StringComparison.Ordinal)
            && candidates.CandidateIds.Count == CandidateCount
            && candidates.CandidateIds.Distinct(StringComparer.Ordinal).Count() == CandidateCount
            && candidates.CandidateIds.All(candidate =>
                RegisteredThinkerIds.Contains(candidate, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string[]> CreateCombinations()
    {
        List<string[]> combinations = [];
        for (int first = 0; first < RegisteredThinkerIds.Count - 2; first++)
        for (int second = first + 1; second < RegisteredThinkerIds.Count - 1; second++)
        for (int third = second + 1; third < RegisteredThinkerIds.Count; third++)
        {
            combinations.Add(
            [
                RegisteredThinkerIds[first],
                RegisteredThinkerIds[second],
                RegisteredThinkerIds[third],
            ]);
        }

        return combinations;
    }
}
