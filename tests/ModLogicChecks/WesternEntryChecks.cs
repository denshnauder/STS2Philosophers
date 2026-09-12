using System.Text.Json;
using STS2Philosophers;

internal static class WesternEntryChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        foreach (WesternProblem problem in WesternRouteCatalog.Problems)
        {
            PhilosophyRunState state = new();
            GeneratedCandidates candidates = new()
            {
                GenerationKey = WesternActOneCandidatePolicy.GenerationKey,
                CandidateIds = new[] { problem.EntryThinkerId }.Concat(
                    WesternRouteCatalog.EntryThinkerIds.Where(id => id != problem.EntryThinkerId).Take(2)).ToList(),
            };
            Check(WesternEntryPolicy.CanChoose(state, candidates, problem.EntryThinkerId, problem.ProblemId, 0, false, false), "Each invited entry can establish its approved problem.");
            Check(!WesternEntryPolicy.CanChoose(state, candidates, problem.EntryThinkerId, problem.ProblemId, 1, false, false)
                && !WesternEntryPolicy.CanChoose(state, candidates, problem.EntryThinkerId, problem.ProblemId, 0, true, false)
                && !WesternEntryPolicy.CanChoose(state, candidates, problem.EntryThinkerId, problem.ProblemId, 0, false, true), "Later acts, finished events and existing doctrine relics reject entry.");
            state.RecordCurrentDoctrine("KONGZI", "KONGZI_MUDUO");
            Check(!WesternEntryPolicy.CanChoose(state, candidates, problem.EntryThinkerId, problem.ProblemId, 0, false, false), "A saved eastern doctrine blocks western entry even if its relic is missing.");
            state = new();
            Check(!WesternEntryPolicy.RecordObtained(state, candidates, problem.EntryThinkerId, problem.ProblemId, "WRONG_RELIC"), "Wrong relic identity cannot commit a journey.");
            string relicId = WesternEntryPolicy.RelicIdFor(problem.ProblemId);
            Check(WesternEntryPolicy.RecordObtained(state, candidates, problem.EntryThinkerId, problem.ProblemId, relicId), "Verified acquisition records one journey and doctrine.");
            state = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
            Check(state.WesternJourney?.LastFixedAct == 1 && state.ThoughtImprints.Count == 1
                && state.ThoughtImprints[0].RouteTags.Contains("WESTERN")
                && state.CurrentDoctrine?.ThinkerId == problem.EntryThinkerId, "Shared save preserves the current doctrine, western tag, history and journey.");
            Check(!WesternEntryPolicy.RecordObtained(state, candidates, problem.EntryThinkerId, problem.ProblemId, relicId), "Repeated acquisition cannot add a second journey or imprint.");
            if (problem.EntryThinkerId == "SOCRATES")
            {
                string other = problem.ProblemId == "KNOWLEDGE_AND_DOUBT" ? "VIRTUE_AND_HAPPINESS" : "KNOWLEDGE_AND_DOUBT";
                Check(!WesternEntryPolicy.CanChoose(state, candidates, "SOCRATES", other, 0, false, false), "Socrates cannot bind both problems in one entry.");
            }
        }
        Check(!PhilosophersGazeRelicGrantPolicy.CanGrant(new(false, false, false, false, HasWesternPractice: true)),
            "Western ownership must reject a second eastern relic.");
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            var keys = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            Check(keys.Count == keys.Distinct().Count(), "Localization keys cannot shadow each other.");
            foreach (WesternProblem problem in WesternRouteCatalog.Problems)
            {
                foreach (string suffix in new[] { $"WESTERN_{problem.EntryThinkerId}.description", $"WESTERN_{problem.ProblemId}.result", $"WESTERN.options.{problem.ProblemId}.title", $"WESTERN.options.{problem.ProblemId}.description" })
                    Check(document.RootElement.TryGetProperty($"PHILOSOPHERS_GAZE.pages.{suffix}", out var text) && !string.IsNullOrWhiteSpace(text.GetString()), "Every entry needs localized question, effect and result text.");
            }
        }
        Console.WriteLine("Western entry checks passed: seven problems, single doctrine, save/repeat guards and bilingual pages.");
    }
}
