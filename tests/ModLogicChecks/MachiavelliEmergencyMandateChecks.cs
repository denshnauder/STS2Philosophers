using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class MachiavelliEmergencyMandateChecks
{
    private const string ThinkerId = "NICCOLO_MACHIAVELLI";

    public static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        EveryPathSurvivesBothSaveBoundaries(graph);
        OnlyExactCompletionCanReplay(graph);
        InvalidPayloadCannotReinvite(graph);
        EntryRequiresReservedSecondActOpportunity(graph);
        LocalizationCoversAllChoices();
        Console.WriteLine("Machiavelli emergency mandate checks passed: 12 paths, preserved decisions, exact question completion, malformed-save isolation and second-act entry guards.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey(WesternRouteGraph graph, bool reserve = true)
    {
        var journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, MachiavelliEmergencyMandateState.PrerequisiteEdgeId), "The source must really adopt the Aristotle civic inquiry.");
        if (reserve) Check(graph.TryReserveQuestion(journey, 2), "Entry reserves the second-act question exactly once.");
        return journey;
    }

    private static PhilosophyRunState NewRun(WesternRouteGraph graph, bool reserve = true) => new()
    {
        WesternJourney = Journey(graph, reserve),
        MachiavelliEmergencyMandate = MachiavelliEmergencyMandateState.Create("INSTANCE"),
        CurrentDoctrine = new() { ThinkerId = "PLATO", DoctrineId = "PLATO_CIVIC_SEAL" },
    };

    private static PhilosophyRunState Reload(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static List<MachiavelliEmergencyMandateAction[]> Paths()
    {
        var paths = new List<MachiavelliEmergencyMandateAction[]>();
        void Walk(MachiavelliEmergencyMandateStage stage, List<MachiavelliEmergencyMandateAction> history)
        {
            foreach (var action in MachiavelliEmergencyMandateFlow.Options(stage))
            {
                var next = history.Append(action).ToList();
                if (action == MachiavelliEmergencyMandateAction.Leave) paths.Add(next.ToArray());
                else Walk(MachiavelliEmergencyMandateFlow.Next(stage, action), next);
            }
        }
        Walk(MachiavelliEmergencyMandateStage.Invitation, []);
        return paths;
    }

    private static void Commit(PhilosophyRunState run, MachiavelliEmergencyMandateAction action, WesternRouteGraph graph)
    {
        Check(MachiavelliEmergencyMandateFlow.TryPrepare(run.MachiavelliEmergencyMandate!, action)
            && MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate!, run.WesternJourney!, graph), $"Commit legal action {action}.");
    }

    private static void EveryPathSurvivesBothSaveBoundaries(WesternRouteGraph graph)
    {
        var paths = Paths();
        Check(paths.Count == 12 && paths.Max(path => path.Length) == 5, "Four consequences allow completion or stopping; declining and three earlier stops add four paths.");
        var reached = new HashSet<MachiavelliEmergencyMandateStage>();
        foreach (var path in paths)
        {
            var run = NewRun(graph);
            foreach (var action in path)
            {
                var state = run.MachiavelliEmergencyMandate!;
                reached.Add(state.Stage);
                Check(MachiavelliEmergencyMandateFlow.IsJourneyConsistent(state, run.WesternJourney!, graph), "Every page remains bound to the reserved Aristotle question.");
                foreach (var illegal in Enum.GetValues<MachiavelliEmergencyMandateAction>().Except(MachiavelliEmergencyMandateFlow.Options(state.Stage)))
                {
                    Check(!MachiavelliEmergencyMandateFlow.TryPrepare(state, illegal)
                        && MachiavelliEmergencyMandateFlow.Next(state.Stage, illegal) == state.Stage,
                        "A previous procedure, water change or surrendered opportunity cannot be replaced by a cross-page action.");
                }
                Check(MachiavelliEmergencyMandateFlow.TryPrepare(state, action)
                    && !MachiavelliEmergencyMandateFlow.TryPrepare(state, action), "A page prepares just one action.");
                run = Reload(run);
                Check(run.MachiavelliEmergencyMandate!.IsUsable()
                    && MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate, run.WesternJourney!, graph)
                    && !MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate, run.WesternJourney!, graph),
                    "The prepared save commits once without changing an earlier decision.");
                run = Reload(run);
                Check(run.WesternJourney!.CurrentNodeId == MachiavelliEmergencyMandateState.RequiredNodeId
                    && run.WesternJourney.LastFixedAct == 2 && run.WesternJourney.DeclinedFixedActs.Count == 0
                    && run.WesternJourney.ResolvedQuestionActs.SetEquals([2]),
                    "Every action preserves the Aristotle node and fixed slots, consuming only the entry-reserved second-act question.");
                Check(run.CurrentDoctrine is { ThinkerId: "PLATO", DoctrineId: "PLATO_CIVIC_SEAL" }, "The encounter does not rewrite the existing doctrine or carrier.");
            }
            bool completed = path.Contains(MachiavelliEmergencyMandateAction.CompleteChallenge);
            var final = run.MachiavelliEmergencyMandate!;
            Check(final.Closed && final.IsUsable() && final.ConfirmedActions.SequenceEqual(path), "Both save boundaries preserve the complete decision history.");
            Check(run.WesternJourney!.CompletedEdgeIds.Contains(MachiavelliEmergencyMandateState.EdgeId) == completed
                && run.WesternJourney.SeenThinkerIds.Contains(ThinkerId) == completed,
                "Only explicit completion records both 017 and the encountered thinker; declining and stopping cannot fabricate either fact.");
            Check(MachiavelliEmergencyMandateFlow.IsJourneyConsistent(final, run.WesternJourney, graph)
                && !MachiavelliEmergencyMandateFlow.TryPrepare(final, MachiavelliEmergencyMandateAction.Leave)
                && !MachiavelliEmergencyMandateFlow.TryCommitPending(final, run.WesternJourney, graph),
                "A closed native event remains recoverable without replaying its question or reopening a decision.");
        }
        Check(reached.SetEquals(Enum.GetValues<MachiavelliEmergencyMandateStage>()), "Every declared page is reachable.");
    }

    private static PhilosophyRunState PreparedResult(WesternRouteGraph graph, MachiavelliEmergencyMandateAction terminal)
    {
        var run = NewRun(graph);
        if (terminal != MachiavelliEmergencyMandateAction.DeclineInvitation)
        {
            Commit(run, MachiavelliEmergencyMandateAction.AcceptInvitation, graph);
            Commit(run, MachiavelliEmergencyMandateAction.ChooseCouncil, graph);
            Commit(run, MachiavelliEmergencyMandateAction.ReopenObjections, graph);
        }
        Check(MachiavelliEmergencyMandateFlow.TryPrepare(run.MachiavelliEmergencyMandate!, terminal), "Prepare an explicit result.");
        return Reload(run);
    }

    private static void Frozen(PhilosophyRunState run, WesternRouteGraph graph, string message)
    {
        string before = PhilosophyRunStateCodec.Encode(run);
        Check(!MachiavelliEmergencyMandateFlow.IsJourneyConsistent(run.MachiavelliEmergencyMandate!, run.WesternJourney!, graph)
            && !MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate!, run.WesternJourney!, graph)
            && PhilosophyRunStateCodec.Encode(run) == before, message);
    }

    private static void OnlyExactCompletionCanReplay(WesternRouteGraph graph)
    {
        foreach (var terminal in new[]
        {
            MachiavelliEmergencyMandateAction.CompleteChallenge,
            MachiavelliEmergencyMandateAction.StopInquiry,
            MachiavelliEmergencyMandateAction.DeclineInvitation,
        })
        {
            var prepared = PreparedResult(graph, terminal);
            foreach (var mutate in new Action<WesternJourneyState>[]
            {
                journey => journey.CurrentNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
                journey => journey.LastFixedAct = 1,
                journey => journey.LastFixedAct = 3,
                journey => journey.CompletedEdgeIds.Remove(MachiavelliEmergencyMandateState.PrerequisiteEdgeId),
                journey => journey.ResolvedQuestionActs.Remove(2),
                journey => journey.RetainedEnding = true,
                journey => journey.DeclinedFixedActs.Add(2),
                journey => journey.CompletedEdgeIds.Add(MachiavelliEmergencyMandateState.EdgeId),
                journey => journey.SeenThinkerIds.Add(ThinkerId),
            })
            {
                var wrong = Reload(prepared);
                mutate(wrong.WesternJourney!);
                Frozen(wrong, graph, "Contradictory source, reservation or edge/seen facts freeze the exact pending result.");
            }

            var already = Reload(prepared);
            Check(graph.TryCompleteReservedQuestion(already.WesternJourney!, MachiavelliEmergencyMandateState.EdgeId), "Simulate graph completion before the action history save.");
            already = Reload(already);
            if (terminal == MachiavelliEmergencyMandateAction.CompleteChallenge)
            {
                string before = JsonSerializer.Serialize(already.WesternJourney);
                Check(MachiavelliEmergencyMandateFlow.IsJourneyConsistent(already.MachiavelliEmergencyMandate!, already.WesternJourney!, graph)
                    && MachiavelliEmergencyMandateFlow.TryCommitPending(already.MachiavelliEmergencyMandate!, already.WesternJourney!, graph)
                    && JsonSerializer.Serialize(already.WesternJourney) == before,
                    "Exact prior 017 completion resumes its pending result without applying the edge again.");
            }
            else Frozen(already, graph, "A stop or refusal cannot reinterpret an already-completed challenge.");

            var run = Reload(prepared);
            Check(MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate!, run.WesternJourney!, graph), "An explicit result commits from its original reserved route.");
            run = Reload(run);
            Check(MachiavelliEmergencyMandateFlow.TryPrepare(run.MachiavelliEmergencyMandate!, MachiavelliEmergencyMandateAction.Leave), "The result remains closable.");
            foreach (var mutate in new Action<WesternJourneyState>[]
            {
                journey => journey.ResolvedQuestionActs.Remove(2),
                journey => journey.CompletedEdgeIds.Remove(MachiavelliEmergencyMandateState.PrerequisiteEdgeId),
                journey => journey.RetainedEnding = true,
                journey =>
                {
                    if (!journey.CompletedEdgeIds.Remove(MachiavelliEmergencyMandateState.EdgeId))
                        journey.CompletedEdgeIds.Add(MachiavelliEmergencyMandateState.EdgeId);
                },
                journey =>
                {
                    if (!journey.SeenThinkerIds.Remove(ThinkerId)) journey.SeenThinkerIds.Add(ThinkerId);
                },
            })
            {
                var wrong = Reload(run);
                mutate(wrong.WesternJourney!);
                Frozen(wrong, graph, "A saved result exit requires the same exact route result.");
            }
            string routeBefore = JsonSerializer.Serialize(run.WesternJourney);
            Check(MachiavelliEmergencyMandateFlow.TryCommitPending(run.MachiavelliEmergencyMandate!, run.WesternJourney!, graph)
                && JsonSerializer.Serialize(run.WesternJourney) == routeBefore, "Leaving a stopped or completed result consumes nothing else.");
            run = Reload(run);
            Check(run.MachiavelliEmergencyMandate!.Closed
                && MachiavelliEmergencyMandateFlow.IsJourneyConsistent(run.MachiavelliEmergencyMandate, run.WesternJourney!, graph), "Closed shared state can finish its native recovery.");
        }
        var nonterminal = NewRun(graph);
        Check(MachiavelliEmergencyMandateFlow.TryPrepare(nonterminal.MachiavelliEmergencyMandate!, MachiavelliEmergencyMandateAction.AcceptInvitation)
            && graph.TryCompleteReservedQuestion(nonterminal.WesternJourney!, MachiavelliEmergencyMandateState.EdgeId), "Prepare an invitation with an unrelated completed route fact.");
        Frozen(nonterminal, graph, "An early pending action cannot guess that the player completed the challenge.");
        var preserved = NewRun(graph);
        preserved.WesternJourney!.ResolvedQuestionActs.Add(1);
        Commit(preserved, MachiavelliEmergencyMandateAction.DeclineInvitation, graph);
        Check(preserved.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2]), "Earlier question facts remain unchanged when declining this question.");
    }

    private static void InvalidPayloadCannotReinvite(WesternRouteGraph graph)
    {
        Check(Reload(new()).MachiavelliEmergencyMandate is null, "Old saves keep the encounter absent.");
        void Invalid(JsonObject root)
        {
            var run = PhilosophyRunStateCodec.Decode(Convert.ToHexString(Encoding.UTF8.GetBytes(root.ToJsonString())));
            Check(run.MachiavelliEmergencyMandate is not null && !run.MachiavelliEmergencyMandate.IsUsable(), "Missing or forged state is unusable.");
            run = Reload(run);
            Check(run.MachiavelliEmergencyMandate is not null && !run.MachiavelliEmergencyMandate.IsUsable()
                && !MachiavelliEmergencyMandateFlow.TryPrepare(run.MachiavelliEmergencyMandate, MachiavelliEmergencyMandateAction.AcceptInvitation),
                "Shared re-saving cannot repair malformed data into a fresh invitation.");
        }
        foreach (string field in new[] { "Version", "InstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            root[nameof(PhilosophyRunState.MachiavelliEmergencyMandate)]!.AsObject().Remove(field);
            Invalid(root);
        }
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            root[nameof(PhilosophyRunState.MachiavelliEmergencyMandate)]![field] = 99;
            Invalid(root);
        }
        foreach (var mutate in new Action<JsonObject>[]
        {
            state => state["InstanceId"] = " ",
            state => state["ConfirmedActions"] = null,
            state => state["ConfirmedActions"] = new JsonArray(99),
            state => state["Closed"] = true,
            state => state["Stage"] = (int)MachiavelliEmergencyMandateStage.ResultCompleted,
            state => state["PendingAction"] = (int)MachiavelliEmergencyMandateAction.CompleteChallenge,
        })
        {
            var root = JsonSerializer.SerializeToNode(NewRun(graph))!.AsObject();
            mutate(root[nameof(PhilosophyRunState.MachiavelliEmergencyMandate)]!.AsObject());
            Invalid(root);
        }
        var forged = PreparedResult(graph, MachiavelliEmergencyMandateAction.CompleteChallenge);
        forged.MachiavelliEmergencyMandate!.Stage = MachiavelliEmergencyMandateStage.ExtendedMandate;
        Check(!forged.MachiavelliEmergencyMandate.IsUsable(), "Forging a page cannot replace a recorded reopened objection with an extended mandate.");
    }

    private static void EntryRequiresReservedSecondActOpportunity(WesternRouteGraph graph)
    {
        var unreserved = NewRun(graph, reserve: false);
        Check(MachiavelliEmergencyMandateFlow.TryPrepare(unreserved.MachiavelliEmergencyMandate!, MachiavelliEmergencyMandateAction.AcceptInvitation), "A prepared page still requires route verification at commit.");
        Frozen(unreserved, graph, "No action can commit before entry has reserved the second-act question.");
        var eligible = new MachiavelliEmergencyMandateTriggerContext(true, true, true, 1, 1,
            MachiavelliEmergencyMandateState.RequiredNodeId, 2, true, true, false, false, false, false, false, true);
        Check(MachiavelliEmergencyMandateTriggerPolicy.ShouldInsert(eligible), "The real Aristotle route may enter at a safe second-act boundary before its question is used.");
        foreach (var denied in new[]
        {
            eligible with { RunInProgress = false }, eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false }, eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 },
            eligible with { CompletedCombatCount = 0 }, eligible with { CurrentNodeId = null },
            eligible with { CurrentNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE" },
            eligible with { LastFixedAct = 1 }, eligible with { LastFixedAct = 3 }, eligible with { HasRequiredRelic = false },
            eligible with { HasPrerequisite = false }, eligible with { QuestionAlreadyResolved = true },
            eligible with { RetainedEnding = true }, eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!MachiavelliEmergencyMandateTriggerPolicy.ShouldInsert(denied), "The first invitation requires its exact source, act, carrier, unused question and safe boundary.");
        var saved = eligible with
        {
            HasSavedInstance = true, SavedInstanceUsable = true, QuestionAlreadyResolved = true,
            HasRequiredRelic = false, CompletedCombatCount = 0,
        };
        Check(MachiavelliEmergencyMandateTriggerPolicy.ShouldInsert(saved), "A usable saved event resumes an already-reserved question without requiring the initial relic gate again.");
        foreach (var denied in new[]
        {
            saved with { SavedInstanceClosed = true }, saved with { SavedInstanceUsable = false },
            saved with { HasPrerequisite = false }, saved with { RetainedEnding = true },
            saved with { AtSafeMapBoundary = false }, saved with { LastFixedAct = 3 },
            saved with { CurrentNodeId = "OTHER" }, saved with { CurrentActIndex = 2 },
        }) Check(!MachiavelliEmergencyMandateTriggerPolicy.ShouldInsert(denied), "Saved recovery keeps the act, route, safe boundary and closure guards.");
    }

    private static void LocalizationCoversAllChoices()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using var text = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<MachiavelliEmergencyMandateStage>())
            {
                string page = $"MACHIAVELLI_EMERGENCY_MANDATE.pages.{MachiavelliEmergencyMandateFlow.StageKey(stage)}";
                Check(text.RootElement.TryGetProperty(page + ".description", out var description)
                    && !string.IsNullOrWhiteSpace(description.GetString()), $"Every {language} consequence has text: {stage}.");
                foreach (var action in MachiavelliEmergencyMandateFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                {
                    string key = $"{page}.options.{MachiavelliEmergencyMandateFlow.ActionKey(action)}.{suffix}";
                    Check(text.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), $"Every reachable {language} choice has text: {key}.");
                }
            }
        }
    }
}
