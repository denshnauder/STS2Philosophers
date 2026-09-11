using STS2Philosophers;

internal static class WesternRouteChecks
{
    public static void Run()
    {
        static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
        }

        string[] expectedProblems = ["BEING_AND_CHANGE", "KNOWLEDGE_AND_DOUBT", "VIRTUE_AND_HAPPINESS", "FREEDOM_AND_INSTITUTIONS", "SELF_AND_OTHER", "LANGUAGE_AND_MEANING", "HISTORY_AND_POWER"];
        string[] expectedEntries = ["HERACLITUS", "SOCRATES", "SOCRATES", "PLATO", "DESCARTES", "ARISTOTLE", "ROUSSEAU"];
        Check(WesternRouteCatalog.Problems.Select(problem => problem.ProblemId).SequenceEqual(expectedProblems)
            && WesternRouteCatalog.Problems.Select(problem => problem.EntryThinkerId).SequenceEqual(expectedEntries),
            "The seven approved problem-to-entry mappings must not drift.");

        HashSet<string> triples = [];
        Dictionary<string, int> appearances = WesternRouteCatalog.EntryThinkerIds.ToDictionary(id => id, _ => 0);
        for (ulong draw = 0; draw < 20; draw++)
        {
            PhilosophyRunState state = new();
            GeneratedCandidates eastern = PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(state, 0);
            GeneratedCandidates western = WesternActOneCandidatePolicy.GetOrGenerate(state, draw);
            Check(western.CandidateIds.Count == 3 && western.CandidateIds.Distinct().Count() == 3,
                "Western invitations must contain three distinct people.");
            Check(triples.Add(string.Join(',', western.CandidateIds)), "Each draw in the twenty-value range must select a different triple.");
            foreach (string id in western.CandidateIds) appearances[id]++;
            Check(ReferenceEquals(western, WesternActOneCandidatePolicy.GetOrGenerate(state, draw + 1)), "A repeat visit must reuse western candidates.");
            PhilosophyRunState loaded = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
            Check(WesternActOneCandidatePolicy.GetOrGenerate(loaded, 99).CandidateIds.SequenceEqual(western.CandidateIds), "Saving and loading must not reroll invitations.");
            Check(loaded.GeneratedCandidates[PhilosophersGazeActOneCandidatePolicy.GenerationKey].CandidateIds.SequenceEqual(eastern.CandidateIds), "Western generation must preserve the eastern candidate namespace.");
            foreach (WesternProblem problem in WesternRouteCatalog.Problems)
            {
                Check(WesternActOneCandidatePolicy.CanChooseProblem(western, problem.EntryThinkerId, problem.ProblemId)
                    == western.CandidateIds.Contains(problem.EntryThinkerId), "Only an invited person may offer their approved problem.");
            }
            Check(!WesternActOneCandidatePolicy.CanChooseProblem(western, "SOCRATES", "HISTORY_AND_POWER"), "A person must not unlock every western problem.");
        }
        Check(triples.Count == 20 && appearances.Values.All(count => count == 10), "All six people must have equal coverage across the twenty possible triples.");

        PhilosophyRunState damaged = new();
        damaged.GeneratedCandidates[WesternActOneCandidatePolicy.GenerationKey] = new()
        {
            GenerationKey = WesternActOneCandidatePolicy.GenerationKey,
            CandidateIds = ["SOCRATES", "SOCRATES", "PLATO"],
        };
        Check(WesternActOneCandidatePolicy.GetOrGenerate(damaged, 0).CandidateIds.Distinct().Count() == 3,
            "Malformed repeated-person candidates must be regenerated.");
        foreach (string invalid in new[] { "null", "[null]", "[]" })
        {
            bool rejected = false;
            try { WesternRouteCatalog.ParseJson(invalid); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "An invalid catalog must fail explicitly.");
        }
        Console.WriteLine("Western route catalog, invitations, save isolation and problem authorization checks passed.");
    }
}
