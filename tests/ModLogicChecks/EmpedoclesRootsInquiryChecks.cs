using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class EmpedoclesRootsInquiryChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var paths = CompletePaths();
        Check(paths.Count == 10, "The inquiry must expose the ten designed complete choices.");
        foreach (EmpedoclesRootsInquiryAction[] path in paths)
        {
            PhilosophyRunState run = NewRun(graph);
            foreach (EmpedoclesRootsInquiryAction action in path)
            {
                EmpedoclesRootsInquiryState state = run.EmpedoclesRootsInquiry!;
                Check(EmpedoclesRootsInquiryFlow.TryPrepare(state, action)
                    && !EmpedoclesRootsInquiryFlow.TryPrepare(state, action), "Only one choice can be prepared.");
                run = Reload(run);
                state = run.EmpedoclesRootsInquiry!;
                Check(state.IsUsable() && EmpedoclesRootsInquiryFlow.IsJourneyConsistent(state, run.WesternJourney!, graph)
                    && EmpedoclesRootsInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph)
                    && !EmpedoclesRootsInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph),
                    "A saved pending choice commits once.");
                run = Reload(run);
            }
            bool recorded = path.Contains(EmpedoclesRootsInquiryAction.RecordComparison);
            WesternJourneyState journey = run.WesternJourney!;
            Check(run.EmpedoclesRootsInquiry is { Closed: true }
                && run.EmpedoclesRootsInquiry.IsUsable()
                && run.EmpedoclesRootsInquiry.ConfirmedActions.SequenceEqual(path)
                && EmpedoclesRootsInquiryFlow.IsJourneyConsistent(run.EmpedoclesRootsInquiry, journey, graph)
                && journey.CurrentNodeId == EmpedoclesRootsInquiryState.RequiredNodeId
                && journey.CompletedEdgeIds.Contains(EmpedoclesRootsInquiryState.EdgeId) == recorded
                && journey.SeenThinkerIds.Contains("EMPEDOCLES") == recorded
                && journey.ResolvedQuestionActs.Contains(2),
                "Only explicit comparison records 006 without changing the current atomist route.");
            Check(!EmpedoclesRootsInquiryFlow.TryPrepare(run.EmpedoclesRootsInquiry!, EmpedoclesRootsInquiryAction.Leave),
                "Closed state cannot reopen.");
        }

        PhilosophyRunState replay = NewRun(graph);
        foreach (var action in new[]
        {
            EmpedoclesRootsInquiryAction.AcceptInvitation,
            EmpedoclesRootsInquiryAction.StudyComposition,
            EmpedoclesRootsInquiryAction.CombineDifferent,
        }) Commit(replay, action, graph);
        Check(EmpedoclesRootsInquiryFlow.TryPrepare(replay.EmpedoclesRootsInquiry!,
            EmpedoclesRootsInquiryAction.RecordComparison), "Result can be prepared.");
        WesternJourneyState wrong = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!EmpedoclesRootsInquiryFlow.TryCommitPending(replay.EmpedoclesRootsInquiry!, wrong, graph)
            && replay.EmpedoclesRootsInquiry!.PendingAction == EmpedoclesRootsInquiryAction.RecordComparison,
            "Wrong route preserves pending choice.");
        Check(graph.TryCompleteReservedQuestion(replay.WesternJourney!, EmpedoclesRootsInquiryState.EdgeId)
            && EmpedoclesRootsInquiryFlow.TryCommitPending(replay.EmpedoclesRootsInquiry!, replay.WesternJourney!, graph)
            && replay.WesternJourney!.CompletedEdgeIds.Count == 3,
            "An already committed 006 resumes exactly once.");
        replay.EmpedoclesRootsInquiry!.Stage = EmpedoclesRootsInquiryStage.Invitation;
        Check(!replay.EmpedoclesRootsInquiry.IsUsable(), "Forged page cannot rewind confirmed actions.");
        Check(Reload(new()).EmpedoclesRootsInquiry is null, "Old saves do not invent the event.");
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(EmpedoclesRootsInquiryState.Create("BAD"))!;
            node[field] = 99;
            Check(!JsonSerializer.Deserialize<EmpedoclesRootsInquiryState>(node)!.IsUsable(),
                "Unknown state values are unusable.");
        }
        foreach (string field in new[] { "Version", "InstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(EmpedoclesRootsInquiryState.Create("BAD"))!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<EmpedoclesRootsInquiryState>(node)!.IsUsable(),
                "A missing saved field cannot become a fresh invitation.");
        }

        var eligible = new EmpedoclesRootsInquiryTriggerContext(true, true, true, 1,
            EmpedoclesRootsInquiryState.RequiredNodeId, 2, true, true, true, true,
            false, false, false, true);
        Check(EmpedoclesRootsInquiryTriggerPolicy.ShouldInsert(eligible), "Adopted Democritus may offer a second-act question.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentNodeId = "OTHER" },
            eligible with { LastFixedAct = 3 }, eligible with { DemocritusAdopted = false },
            eligible with { PriorEventClosed = false }, eligible with { QuestionUnresolved = false },
            eligible with { EdgeEligible = false }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { ModelAvailable = false },
            eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!EmpedoclesRootsInquiryTriggerPolicy.ShouldInsert(denied),
            "Missing prerequisites and spent question cannot create a new event.");
        Check(EmpedoclesRootsInquiryTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, QuestionUnresolved = false, EdgeEligible = false }),
            "A valid open event restores after the question has been reserved.");

        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<EmpedoclesRootsInquiryStage>())
            {
                string page = $"EMPEDOCLES_ROOTS_INQUIRY.pages.{EmpedoclesRootsInquiryFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _), "Every page has bilingual copy.");
                foreach (var action in EmpedoclesRootsInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{EmpedoclesRootsInquiryFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option has bilingual copy.");
            }
        }
        Console.WriteLine("Empedocles inquiry checks passed: ten complete paths, saved pending choices, exact 006 recovery, route eligibility, old saves and bilingual text.");
    }

    private static PhilosophyRunState NewRun(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start("HERACLITUS__BEING_AND_CHANGE__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_001")
            && graph.TryApply(journey, DemocritusArrangementState.EdgeId)
            && graph.GetEligibleEdges(journey, "Question")
                .Any(edge => edge.EdgeId == EmpedoclesRootsInquiryState.EdgeId)
            && graph.TryReserveQuestion(journey, 2), "Fixture requires the real Parmenides and Democritus route.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            EmpedoclesRootsInquiry = EmpedoclesRootsInquiryState.Create("EMPEDOCLES_ROOTS_INQUIRY:FIXTURE:2"),
        };
    }

    private static PhilosophyRunState Reload(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static void Commit(PhilosophyRunState run, EmpedoclesRootsInquiryAction action, WesternRouteGraph graph)
    {
        Check(EmpedoclesRootsInquiryFlow.TryPrepare(run.EmpedoclesRootsInquiry!, action)
            && EmpedoclesRootsInquiryFlow.TryCommitPending(run.EmpedoclesRootsInquiry!, run.WesternJourney!, graph),
            $"Commit {action}.");
    }

    private static List<EmpedoclesRootsInquiryAction[]> CompletePaths()
    {
        List<EmpedoclesRootsInquiryAction[]> paths = [];
        void Walk(EmpedoclesRootsInquiryStage stage, List<EmpedoclesRootsInquiryAction> history)
        {
            foreach (var action in EmpedoclesRootsInquiryFlow.Options(stage))
            {
                List<EmpedoclesRootsInquiryAction> next = history.Append(action).ToList();
                if (action == EmpedoclesRootsInquiryAction.Leave) paths.Add(next.ToArray());
                else Walk(EmpedoclesRootsInquiryFlow.Next(stage, action), next);
            }
        }
        Walk(EmpedoclesRootsInquiryStage.Invitation, []);
        return paths;
    }
}
