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
        Dictionary<string, int> appearances = PhilosophersGazeActOneCandidatePolicy.RegisteredThinkerIds
            .ToDictionary(id => id, _ => 0);
        for (ulong draw = 0; draw < 84; draw++)
        {
            PhilosophyRunState state = new();
            GeneratedCandidates candidates = PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(state, draw);
            Check(candidates.CandidateIds.Count == 3 && candidates.CandidateIds.Distinct().Count() == 3,
                "Unified invitations must contain three distinct people.");
            Check(triples.Add(string.Join(',', candidates.CandidateIds)),
                "Each draw in the eighty-four-value range must select a different unified triple.");
            foreach (string id in candidates.CandidateIds) appearances[id]++;
            Check(ReferenceEquals(candidates, PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(state, draw + 1)),
                "A repeat visit must reuse unified candidates.");
            PhilosophyRunState loaded = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
            Check(PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(loaded, 99).CandidateIds.SequenceEqual(candidates.CandidateIds),
                "Saving and loading must not reroll unified invitations.");
            foreach (WesternProblem problem in WesternRouteCatalog.Problems)
            {
                Check(PhilosophersGazeActOneCandidatePolicy.CanChooseProblem(candidates, problem.EntryThinkerId, problem.ProblemId)
                    == candidates.CandidateIds.Contains(problem.EntryThinkerId),
                    "Only an invited person may offer their approved problem.");
            }
            Check(!PhilosophersGazeActOneCandidatePolicy.CanChooseProblem(candidates, "SOCRATES", "HISTORY_AND_POWER"),
                "A person must not unlock every western problem.");
        }
        Check(triples.Count == 84 && appearances.Values.All(count => count == 28),
            "All nine people must have equal coverage across the eighty-four unified triples.");

        PhilosophyRunState damaged = new();
        damaged.GeneratedCandidates[PhilosophersGazeActOneCandidatePolicy.GenerationKey] = new()
        {
            GenerationKey = PhilosophersGazeActOneCandidatePolicy.GenerationKey,
            CandidateIds = ["SOCRATES", "SOCRATES", "PLATO"],
        };
        Check(PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(damaged, 0).CandidateIds.Distinct().Count() == 3,
            "Malformed repeated-person candidates must be regenerated.");
        foreach (string invalid in new[] { "null", "[null]", "[]" })
        {
            bool rejected = false;
            try { WesternRouteCatalog.ParseJson(invalid); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "An invalid catalog must fail explicitly.");
        }
        Console.WriteLine("Unified act-one invitations, western catalog and problem authorization checks passed.");
    }
}
