using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class DemocritusArrangementChecks
{
    public static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        AllPathsSurviveBothSaveBoundaries(graph);
        ExactPriorRouteCommitResumesWithoutGuessing(graph);
        InvalidStateCannotBecomeANewInvitation(graph);
        TriggerRequiresTheRealPredecessorAndSafeMap();
        LocalizationCoversEveryReachablePage();
        Console.WriteLine("Democritus arrangement checks passed: 12 complete paths, fixed-slot adoption or decline, exact replay, malformed-save isolation and safe entry.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey(WesternRouteGraph graph)
    {
        var journey = graph.Start("HERACLITUS__BEING_AND_CHANGE__CORE");
        Check(graph.TryApply(journey, DemocritusArrangementState.PrerequisiteEdgeId), "The fixture must really complete the first-act Parmenides question.");
        return journey;
    }

    private static PhilosophyRunState NewRun(WesternRouteGraph graph) => new()
    {
        WesternJourney = Journey(graph),
        DemocritusArrangement = DemocritusArrangementState.Create("INSTANCE"),
        CurrentDoctrine = new() { ThinkerId = "HERACLITUS", DoctrineId = "HERACLITUS_LYRE" },
    };

    private static PhilosophyRunState Reload(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static void Commit(PhilosophyRunState run, DemocritusArrangementAction action, WesternRouteGraph graph)
    {
        Check(DemocritusArrangementFlow.TryPrepare(run.DemocritusArrangement!, action)
            && DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement!, run.WesternJourney!, graph), $"Commit legal action {action}.");
    }

    private static List<DemocritusArrangementAction[]> CompletePaths()
    {
        var paths = new List<DemocritusArrangementAction[]>();
        void Walk(DemocritusArrangementStage stage, List<DemocritusArrangementAction> history)
        {
            foreach (var action in DemocritusArrangementFlow.Options(stage))
            {
                var next = history.Append(action).ToList();
                if (action == DemocritusArrangementAction.Leave) paths.Add(next.ToArray());
                else Walk(DemocritusArrangementFlow.Next(stage, action), next);
            }
        }
        Walk(DemocritusArrangementStage.Invitation, []);
        return paths;
    }

    private static void AllPathsSurviveBothSaveBoundaries(WesternRouteGraph graph)
    {
        var paths = CompletePaths();
        Check(paths.Count == 12, "Four model consequences each permit two conclusions; invitation and three stopping points add four paths.");
        var reached = new HashSet<DemocritusArrangementStage>();
        foreach (var path in paths)
        {
            var run = NewRun(graph);
            var questionActs = new HashSet<int>(run.WesternJourney!.ResolvedQuestionActs);
            foreach (var action in path)
            {
                var state = run.DemocritusArrangement!;
                reached.Add(state.Stage);
                Check(DemocritusArrangementFlow.IsJourneyConsistent(state, run.WesternJourney!, graph), "Every legal page remains bound to its route.");
                foreach (var illegal in Enum.GetValues<DemocritusArrangementAction>().Except(DemocritusArrangementFlow.Options(state.Stage)))
                {
                    Check(!DemocritusArrangementFlow.TryPrepare(state, illegal)
                        && DemocritusArrangementFlow.Next(state.Stage, illegal) == state.Stage,
                        "A question, operation or consequence cannot be replaced by a cross-page action.");
                }
                Check(DemocritusArrangementFlow.TryPrepare(state, action)
                    && !DemocritusArrangementFlow.TryPrepare(state, action), "A page may prepare only one action.");
                run = Reload(run);
                Check(run.DemocritusArrangement!.IsUsable()
                    && DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement, run.WesternJourney!, graph)
                    && !DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement, run.WesternJourney!, graph),
                    "A prepared save commits once and preserves the selected model and operation.");
                run = Reload(run);
                Check(run.WesternJourney!.ResolvedQuestionActs.SetEquals(questionActs), "No choice consumes or clears a question slot.");
                Check(run.CurrentDoctrine is { ThinkerId: "HERACLITUS", DoctrineId: "HERACLITUS_LYRE" }, "An encounter does not rewrite the existing doctrine record.");
            }
            bool adopted = path.Contains(DemocritusArrangementAction.AdoptAtoms);
            var final = run.DemocritusArrangement!;
            Check(final.Closed && final.IsUsable() && final.ConfirmedActions.SequenceEqual(path), "The full confirmed path survives every prepared and committed save.");
            Check(run.WesternJourney!.LastFixedAct == 2
                && run.WesternJourney.CurrentNodeId == (adopted ? DemocritusArrangementState.AdoptedNodeId : DemocritusArrangementState.RequiredNodeId)
                && run.WesternJourney.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId) == adopted
                && run.WesternJourney.DeclinedFixedActs.Contains(2) != adopted,
                "Only explicit adoption applies 005; retention, stopping and declining consume just the second fixed opportunity.");
            Check(DemocritusArrangementFlow.IsJourneyConsistent(final, run.WesternJourney, graph)
                && !DemocritusArrangementFlow.TryPrepare(final, DemocritusArrangementAction.Leave)
                && !DemocritusArrangementFlow.TryCommitPending(final, run.WesternJourney, graph),
                "A saved closed native event can finish recovery without reopening or consuming its route again.");
            var wrong = Journey(graph);
            Check(!DemocritusArrangementFlow.IsJourneyConsistent(final, wrong, graph), "A closed event does not claim an uncommitted route result.");
        }
        Check(reached.SetEquals(Enum.GetValues<DemocritusArrangementStage>()), "Every declared page is reachable.");
    }

    private static PhilosophyRunState PreparedResult(WesternRouteGraph graph, DemocritusArrangementAction terminal)
    {
        var run = NewRun(graph);
        if (terminal != DemocritusArrangementAction.DeclineInvitation)
        {
            Commit(run, DemocritusArrangementAction.AcceptInvitation, graph);
            if (terminal != DemocritusArrangementAction.StopInquiry)
            {
                Commit(run, DemocritusArrangementAction.StudyMotion, graph);
                Commit(run, DemocritusArrangementAction.LeaveGap, graph);
            }
        }
        Check(DemocritusArrangementFlow.TryPrepare(run.DemocritusArrangement!, terminal), "Prepare one explicit route result.");
        return Reload(run);
    }

    private static void AssertFrozen(PhilosophyRunState run, WesternRouteGraph graph, string message)
    {
        string before = PhilosophyRunStateCodec.Encode(run);
        Check(!DemocritusArrangementFlow.IsJourneyConsistent(run.DemocritusArrangement!, run.WesternJourney!, graph)
            && !DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement!, run.WesternJourney!, graph)
            && PhilosophyRunStateCodec.Encode(run) == before, message);
    }

    private static void ExactPriorRouteCommitResumesWithoutGuessing(WesternRouteGraph graph)
    {
        foreach (var terminal in new[]
        {
            DemocritusArrangementAction.AdoptAtoms, DemocritusArrangementAction.KeepParmenides,
            DemocritusArrangementAction.StopInquiry, DemocritusArrangementAction.DeclineInvitation,
        })
        {
            bool adopted = terminal == DemocritusArrangementAction.AdoptAtoms;
            var prepared = PreparedResult(graph, terminal);
            var mutations = new Action<WesternJourneyState>[]
            {
                journey => journey.CurrentNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
                journey => journey.CompletedEdgeIds.Remove(DemocritusArrangementState.PrerequisiteEdgeId),
                journey => journey.LastFixedAct = 2,
                journey => journey.RetainedEnding = true,
                journey => journey.DeclinedFixedActs.Add(2),
                journey => journey.CompletedEdgeIds.Add(DemocritusArrangementState.EdgeId),
            };
            foreach (var mutate in mutations)
            {
                var wrong = Reload(prepared);
                mutate(wrong.WesternJourney!);
                AssertFrozen(wrong, graph, "A contradictory predecessor preserves the pending result and route without guessing.");
            }
            var opposite = Reload(prepared);
            Check(adopted ? graph.TryDecline(opposite.WesternJourney!, "Fixed", 2)
                : graph.TryApply(opposite.WesternJourney!, DemocritusArrangementState.EdgeId), "Create the opposite committed route result.");
            AssertFrozen(opposite, graph, "An adoption cannot replay a refusal, and a refusal cannot replay an adoption.");

            var run = Reload(prepared);
            Check(adopted ? graph.TryApply(run.WesternJourney!, DemocritusArrangementState.EdgeId)
                : graph.TryDecline(run.WesternJourney!, "Fixed", 2), "Simulate a route commit before the event history was saved.");
            run = Reload(run);
            string routeBefore = JsonSerializer.Serialize(run.WesternJourney);
            Check(DemocritusArrangementFlow.IsJourneyConsistent(run.DemocritusArrangement!, run.WesternJourney!, graph)
                && DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement!, run.WesternJourney!, graph)
                && JsonSerializer.Serialize(run.WesternJourney) == routeBefore, "An exact already-committed route updates only the pending event history.");
            run = Reload(run);
            Check(DemocritusArrangementFlow.TryPrepare(run.DemocritusArrangement!, DemocritusArrangementAction.Leave), "A saved result still offers its exit.");
            foreach (var mutate in new Action<WesternJourneyState>[]
            {
                journey => journey.RetainedEnding = true,
                journey => journey.LastFixedAct = 3,
                journey => journey.CompletedEdgeIds.Remove(DemocritusArrangementState.PrerequisiteEdgeId),
                journey => { journey.CompletedEdgeIds.Add(DemocritusArrangementState.EdgeId); journey.DeclinedFixedActs.Add(2); },
            })
            {
                var wrong = Reload(run);
                mutate(wrong.WesternJourney!);
                AssertFrozen(wrong, graph, "A saved result exit cannot conceal contradictory route facts.");
            }
            Check(DemocritusArrangementFlow.TryCommitPending(run.DemocritusArrangement!, run.WesternJourney!, graph), "An exact saved result can close.");
            run = Reload(run);
            Check(run.DemocritusArrangement!.Closed
                && DemocritusArrangementFlow.IsJourneyConsistent(run.DemocritusArrangement, run.WesternJourney!, graph), "The complete shared save remains recoverably closed.");
        }
        var incomplete = NewRun(graph);
        Check(DemocritusArrangementFlow.TryPrepare(incomplete.DemocritusArrangement!, DemocritusArrangementAction.AcceptInvitation)
            && graph.TryApply(incomplete.WesternJourney!, DemocritusArrangementState.EdgeId), "Create an unrelated route resolution while an invitation is pending.");
        AssertFrozen(incomplete, graph, "A nonterminal pending action cannot infer an adopted result.");
    }

    private static string EncodeJson(JsonObject root) => Convert.ToHexString(Encoding.UTF8.GetBytes(root.ToJsonString()));

    private static void InvalidStateCannotBecomeANewInvitation(WesternRouteGraph graph)
    {
        Check(Reload(new()).DemocritusArrangement is null, "Saves made before this encounter keep it absent.");
        void CheckInvalid(JsonObject root, string message)
        {
            var run = PhilosophyRunStateCodec.Decode(EncodeJson(root));
            Check(run.DemocritusArrangement is not null && !run.DemocritusArrangement.IsUsable(), message);
            run = Reload(run);
            Check(run.DemocritusArrangement is not null && !run.DemocritusArrangement.IsUsable()
                && !DemocritusArrangementFlow.TryPrepare(run.DemocritusArrangement, DemocritusArrangementAction.AcceptInvitation),
                "Re-saving a bad payload preserves its unusable instance instead of recreating an invitation.");
        }
        foreach (var field in new[] { "Version", "InstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            root[nameof(PhilosophyRunState.DemocritusArrangement)]!.AsObject().Remove(field);
            CheckInvalid(root, $"Missing {field} is not a new invitation.");
        }
        foreach (var field in new[] { "Version", "Stage", "PendingAction" })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            root[nameof(PhilosophyRunState.DemocritusArrangement)]![field] = 99;
            CheckInvalid(root, $"Unknown {field} remains unusable.");
        }
        foreach (var mutate in new Action<JsonObject>[]
        {
            state => state["InstanceId"] = " ",
            state => state["ConfirmedActions"] = null,
            state => state["ConfirmedActions"] = new JsonArray(99),
            state => state["Closed"] = true,
            state => state["Stage"] = (int)DemocritusArrangementStage.ResultAdopted,
            state => state["PendingAction"] = (int)DemocritusArrangementAction.AdoptAtoms,
        })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            mutate(root[nameof(PhilosophyRunState.DemocritusArrangement)]!.AsObject());
            CheckInvalid(root, "Forged pages, histories and pending actions cannot bypass the chosen model.");
        }
        var forged = PreparedResult(graph, DemocritusArrangementAction.AdoptAtoms);
        forged.DemocritusArrangement!.Stage = DemocritusArrangementStage.QualityModel;
        Check(!forged.DemocritusArrangement.IsUsable(), "Changing only the current page cannot replace the confirmed motion inquiry.");
    }

    private static void TriggerRequiresTheRealPredecessorAndSafeMap()
    {
        var eligible = new DemocritusArrangementTriggerContext(true, true, true, 1, 1,
            DemocritusArrangementState.RequiredNodeId, 1, true, true, false, true, false, false, false, true);
        Check(DemocritusArrangementTriggerPolicy.ShouldInsert(eligible), "A real Parmenides route with 001 and its retained lyre may enter after second-act combat.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 },
            eligible with { LastFixedAct = 2 }, eligible with { CurrentNodeId = DemocritusArrangementState.AdoptedNodeId },
            eligible with { CurrentNodeId = null }, eligible with { CurrentNodeId = "HERACLITUS__BEING_AND_CHANGE__CORE" },
            eligible with { HasRequiredRelic = false }, eligible with { HasPrerequisite = false },
            eligible with { RetainedEnding = true }, eligible with { IsSafeRoomStack = false },
            eligible with { CompletedCombatCount = 0 }, eligible with { RunInProgress = false },
            eligible with { IsSingleplayer = false }, eligible with { CurrentRoomIsMap = false },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!DemocritusArrangementTriggerPolicy.ShouldInsert(denied), "Wrong predecessor, consumed opportunity or unsafe map cannot create an instance.");
        foreach (int lastFixedAct in new[] { 1, 2 })
        foreach (string node in new[] { DemocritusArrangementState.RequiredNodeId, DemocritusArrangementState.AdoptedNodeId })
        {
            var saved = eligible with
            {
                HasSavedInstance = true, SavedInstanceUsable = true, LastFixedAct = lastFixedAct,
                CurrentNodeId = node, HasRequiredRelic = false, CompletedCombatCount = 0,
            };
            Check(DemocritusArrangementTriggerPolicy.ShouldInsert(saved), "A usable open save restores its pending or confirmed page without recreating the first-entry relic gate.");
            foreach (var denied in new[]
            {
                saved with { HasPrerequisite = false }, saved with { RetainedEnding = true },
                saved with { IsSafeRoomStack = false }, saved with { SavedInstanceUsable = false },
                saved with { SavedInstanceClosed = true }, saved with { LastFixedAct = 3 },
                saved with { CurrentNodeId = "OTHER" }, saved with { CurrentActIndex = 2 },
            }) Check(!DemocritusArrangementTriggerPolicy.ShouldInsert(denied), "Saved state does not waive route, act, safety or closure checks.");
        }
    }

    private static void LocalizationCoversEveryReachablePage()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using var text = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<DemocritusArrangementStage>())
            {
                string page = $"DEMOCRITUS_ARRANGEMENT.pages.{DemocritusArrangementFlow.StageKey(stage)}";
                Check(text.RootElement.TryGetProperty(page + ".description", out var description)
                    && !string.IsNullOrWhiteSpace(description.GetString()), $"Every {language} model and result page has text: {stage}.");
                foreach (var action in DemocritusArrangementFlow.Options(stage))
                foreach (var suffix in new[] { "title", "description" })
                {
                    string key = $"{page}.options.{DemocritusArrangementFlow.ActionKey(action)}.{suffix}";
                    Check(text.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), $"Every reachable {language} choice has text: {key}.");
                }
            }
        }
    }
}
