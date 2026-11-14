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
                GenerationKey = PhilosophersGazeActOneCandidatePolicy.GenerationKey,
                CandidateIds = new[] { problem.EntryThinkerId }.Concat(
                    PhilosophersGazeActOneCandidatePolicy.RegisteredThinkerIds
                        .Where(id => id != problem.EntryThinkerId).Take(2)).ToList(),
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
        // An entry shared by several lines must let the player choose the default
        // mainline before act one; that choice spends neither a question nor a
        // fixed slot.
        WesternRouteDefinition sharedRoute = WesternRouteTable.Instance.Routes
            .First(route => route.EntryNodeId == "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"
                && route.RouteId == "SYMPATHY_AND_PUBLIC_ACTION");
        PhilosophyRunState sharedEntry = new();
        GeneratedCandidates sharedCandidates = new()
        {
            GenerationKey = PhilosophersGazeActOneCandidatePolicy.GenerationKey,
            CandidateIds = new[] { "PLATO" }.Concat(
                PhilosophersGazeActOneCandidatePolicy.RegisteredThinkerIds
                    .Where(id => id != "PLATO").Take(2)).ToList(),
        };
        Check(WesternEntryPolicy.RecordObtained(sharedEntry, sharedCandidates, "PLATO",
                "FREEDOM_AND_INSTITUTIONS", WesternEntryPolicy.RelicIdFor("FREEDOM_AND_INSTITUTIONS"),
                sharedRoute.RouteId)
            && sharedEntry.WesternJourney?.RouteId == sharedRoute.RouteId,
            "The entry's default-line choice is saved before act one.");
        Check(sharedEntry.WesternJourney is { LastFixedAct: 1, RouteRules: true }
            && sharedEntry.WesternJourney.ResolvedQuestionActs.Count == 0,
            "Choosing a default line spends no fixed event or question point.");
        PhilosophyRunState socialEntry = new();
        GeneratedCandidates socialCandidates = new()
        {
            GenerationKey = PhilosophersGazeActOneCandidatePolicy.GenerationKey,
            CandidateIds = new[] { "ROUSSEAU" }.Concat(
                PhilosophersGazeActOneCandidatePolicy.RegisteredThinkerIds
                    .Where(id => id != "ROUSSEAU").Take(2)).ToList(),
        };
        Check(WesternEntryPolicy.RecordObtained(socialEntry, socialCandidates, "ROUSSEAU",
                "HISTORY_AND_POWER", WesternEntryPolicy.RelicIdFor("HISTORY_AND_POWER"),
                "SOCIAL_CRITIQUE")
            && socialEntry.WesternJourney is { RouteId: "SOCIAL_CRITIQUE", LastFixedAct: 1, RouteRules: true },
            "Rousseau's entry must save SOCIAL_CRITIQUE as the no-question default line.");
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            var keys = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            Check(keys.Count == keys.Distinct().Count(), "Localization keys cannot shadow each other.");
            foreach (WesternProblem problem in WesternRouteCatalog.Problems)
            {
                foreach (string suffix in new[] { $"WESTERN_{problem.EntryThinkerId}.description", $"WESTERN_{problem.ProblemId}.result", $"WESTERN.options.{problem.ProblemId}.title", $"WESTERN.options.{problem.ProblemId}.description" })
                    Check(document.RootElement.TryGetProperty($"PHILOSOPHERS_GAZE.pages.{suffix}", out var text) && !string.IsNullOrWhiteSpace(text.GetString()), "Every entry needs localized question, effect and result text.");
                foreach (WesternRouteDefinition route in WesternRouteTable.Instance.Routes.Where(route => route.EntryNodeId == $"{problem.EntryThinkerId}__{problem.ProblemId}__CORE"))
                foreach (string suffix in new[] { "title", "description" })
                    Check(document.RootElement.TryGetProperty($"PHILOSOPHERS_GAZE.pages.WESTERN.options.{problem.ProblemId}.{route.RouteId}.{suffix}", out var routeText) && !string.IsNullOrWhiteSpace(routeText.GetString()), "Every default line needs localized choice text.");
            }
        }
        Console.WriteLine("Western entry checks passed: seven problems, single doctrine, save/repeat guards and bilingual pages.");
    }
}
