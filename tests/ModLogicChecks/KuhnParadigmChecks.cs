using STS2Philosophers;

internal static class KuhnParadigmChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();

        CheckFixedAdoption(graph, KuhnParadigmState.NewtonCompletionEdgeId,
            KuhnParadigmState.NewtonRequiredEdgeId, KuhnParadigmState.ScientificRouteId,
            KuhnParadigmState.NewtonSourceNodeId, NewtonJourney);
        CheckFixedAdoption(graph, KuhnParadigmState.ComteCompletionEdgeId,
            KuhnParadigmState.ComteRequiredEdgeId, KuhnParadigmState.SocialRouteId,
            KuhnParadigmState.ComteSourceNodeId, ComteJourney);
        CheckFixedDecline(graph, KuhnParadigmState.NewtonCompletionEdgeId,
            KuhnParadigmState.NewtonRequiredEdgeId, KuhnParadigmState.ScientificRouteId,
            KuhnParadigmState.NewtonSourceNodeId, NewtonJourney, KuhnParadigmAction.KeepOldMethod,
            KuhnParadigmStandard.OldMethod);
        CheckFixedDecline(graph, KuhnParadigmState.ComteCompletionEdgeId,
            KuhnParadigmState.ComteRequiredEdgeId, KuhnParadigmState.SocialRouteId,
            KuhnParadigmState.ComteSourceNodeId, ComteJourney, KuhnParadigmAction.ObserveInParallel,
            KuhnParadigmStandard.Parallel);
        CheckRejectedRecords(graph);
        CheckRouteConsistency(graph);
        CheckTriggerPolicy();
        CheckLegacyQuestionMode(graph);
    }

    private static void CheckFixedAdoption(WesternRouteGraph graph, string edgeId, string requiredEdgeId,
        string routeId, string sourceNodeId, Func<WesternRouteGraph, WesternJourneyState> makeJourney)
    {
        var journey = makeJourney(graph);
        var state = KuhnParadigmState.CreateAt($"KUHN_PARADIGM:TEST:{edgeId}", 3, edgeId);
        state.RoutePlaced = true;
        Check(state.FixedMode && state.ActIndex == 3 && state.EdgeId == edgeId,
            $"{edgeId}: a fixed record must name act three and its route edge.");
        Check(KuhnParadigmFlow.IsRouteConsistent(state, journey),
            $"{edgeId}: the room must open on the turned route after {requiredEdgeId}.");
        Check(KuhnParadigmFlow.Options(state.Stage).Count == 2,
            $"{edgeId}: the room must first offer the old-paradigm check or the incompatible classification.");
        Check(!KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: the room cannot commit before a first action is prepared.");

        Check(KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.StudyNewClassification),
            $"{edgeId}: the incompatible classification must be selectable.");
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: studying the new classification must reach the decision page.");
        Check(state.Stage == KuhnParadigmStage.Decision && state.AnomalyCount == 1
            && state.IncomparableDimensions == 2 && !state.EdgeCommitted && state.Cost == 0m,
            $"{edgeId}: the first step records accumulated anomalies only and takes no fixed slot.");
        Check(KuhnParadigmFlow.CanAdopt(state),
            $"{edgeId}: incompatible dimensions must make the paradigm change selectable.");
        Check(!journey.CompletedEdgeIds.Contains(edgeId) && !journey.DeclinedFixedActs.Contains(3),
            $"{edgeId}: recording an anomaly must not take or refuse the fixed slot.");

        Check(KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.TrainTeam),
            $"{edgeId}: training on the new scale must be selectable.");
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: training must take this act's fixed handover.");
        Check(state.Standard == KuhnParadigmStandard.Trained && state.EdgeCommitted
            && state.Cost == KuhnParadigmState.TrainTeamGoldCost
            && journey.CompletedEdgeIds.Contains(edgeId)
            && journey.CurrentNodeId == KuhnParadigmState.AdoptedNodeId
            && journey.LastFixedAct == 3
            && !journey.ResolvedQuestionActs.Contains(3)
            && !journey.DeclinedFixedActs.Contains(3),
            $"{edgeId}: adoption must enter Kuhn and consume the fixed slot, not a question.");
        Check(journey.RouteId == routeId && journey.CompletedEdgeIds.Contains(requiredEdgeId),
            $"{edgeId}: adoption must keep the route that handed over the room.");
        Check(KuhnParadigmFlow.IsRouteConsistent(state, journey),
            $"{edgeId}: the adopted record must stay consistent on reload.");
        Check(!KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: a committed record cannot commit a second choice.");

        var wrongRoute = makeJourney(graph);
        wrongRoute.RouteId = routeId == KuhnParadigmState.ScientificRouteId
            ? KuhnParadigmState.SocialRouteId : KuhnParadigmState.ScientificRouteId;
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt($"KUHN_PARADIGM:TEST:{edgeId}:WRONG", 3, edgeId), wrongRoute),
            $"{edgeId}: the room must reject the other line's route.");
        Check(sourceNodeId == KuhnParadigmState.SourceNodeFor(edgeId),
            $"{edgeId}: the route source must match the graph handover.");
    }

    private static void CheckFixedDecline(WesternRouteGraph graph, string edgeId, string requiredEdgeId,
        string routeId, string sourceNodeId, Func<WesternRouteGraph, WesternJourneyState> makeJourney,
        KuhnParadigmAction decline, KuhnParadigmStandard standard)
    {
        var journey = makeJourney(graph);
        var state = KuhnParadigmState.CreateAt($"KUHN_PARADIGM:TEST:{edgeId}:DECLINE", 3, edgeId);
        state.RoutePlaced = true;
        Check(KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.CheckInstruments),
            $"{edgeId}: the old-paradigm check must be selectable.");
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: the old-paradigm check must reach the decision page.");
        Check(!KuhnParadigmFlow.CanAdopt(state)
            && !KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.TrainTeam),
            $"{edgeId}: one anomaly without incompatible dimensions must not permit a paradigm change.");
        Check(KuhnParadigmFlow.TryPrepare(state, decline),
            $"{edgeId}: {decline} must be selectable on the decision page.");
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            $"{edgeId}: {decline} must refuse this act's fixed handover.");
        Check(state.Standard == standard && state.QuestionDeclined && state.Cost == 0m,
            $"{edgeId}: {decline} must record the refused standard without charging gold.");
        Check(journey.DeclinedFixedActs.Contains(3) && journey.LastFixedAct == 3
            && journey.CurrentNodeId == sourceNodeId
            && !journey.CompletedEdgeIds.Contains(edgeId)
            && !journey.ResolvedQuestionActs.Contains(3),
            $"{edgeId}: {decline} must stay on {sourceNodeId} and consume the fixed slot only.");
        Check(journey.RouteId == routeId && journey.CompletedEdgeIds.Contains(requiredEdgeId),
            $"{edgeId}: declining must not rewrite the route.");
        Check(KuhnParadigmFlow.IsRouteConsistent(state, journey),
            $"{edgeId}: the declined record must stay consistent on reload.");
    }

    private static void CheckRejectedRecords(WesternRouteGraph graph)
    {
        var tampered = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:1", 3,
            KuhnParadigmState.NewtonCompletionEdgeId);
        tampered.Standard = KuhnParadigmStandard.Trained;
        Check(!tampered.IsUsable(), "A standard without its recorded actions must be rejected.");

        var wrongAct = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:2", 2,
            KuhnParadigmState.NewtonCompletionEdgeId);
        Check(!wrongAct.IsUsable(), "A fixed record outside act three must be rejected.");

        var wrongEdge = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:3", 3, "WESTERN_EDGE_090");
        Check(!wrongEdge.IsUsable(), "A fixed record must not reuse the legacy question edge.");

        var legacyWithFixedEdge = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:BAD:4");
        legacyWithFixedEdge.EdgeId = KuhnParadigmState.ComteCompletionEdgeId;
        Check(!legacyWithFixedEdge.IsUsable(), "A legacy record must not claim a fixed route edge.");

        var badActions = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:5", 3,
            KuhnParadigmState.ComteCompletionEdgeId);
        badActions.ConfirmedActions = [KuhnParadigmAction.TrainTeam];
        Check(!badActions.IsUsable(), "A training result without the measurement page must be rejected.");

        var singleAnomalyAdoption = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:5B", 3,
            KuhnParadigmState.NewtonCompletionEdgeId);
        singleAnomalyAdoption.ConfirmedActions =
            [KuhnParadigmAction.CheckInstruments, KuhnParadigmAction.TrainTeam];
        singleAnomalyAdoption.Stage = KuhnParadigmStage.ResultTrained;
        singleAnomalyAdoption.Standard = KuhnParadigmStandard.Trained;
        singleAnomalyAdoption.EdgeCommitted = true;
        singleAnomalyAdoption.AnomalyCount = 1;
        Check(!singleAnomalyAdoption.IsUsable(),
            "A fixed record must reject adoption after a single anomaly with no incompatible dimensions.");

        var pendingSingleAnomaly = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:5C", 3,
            KuhnParadigmState.ComteCompletionEdgeId);
        pendingSingleAnomaly.ConfirmedActions = [KuhnParadigmAction.CheckInstruments];
        pendingSingleAnomaly.Stage = KuhnParadigmStage.Decision;
        pendingSingleAnomaly.AnomalyCount = 1;
        pendingSingleAnomaly.PendingAction = KuhnParadigmAction.TrainTeam;
        Check(!pendingSingleAnomaly.IsUsable(),
            "A fixed record must reject a pending adoption that skipped the incompatible dimensions.");

        var badAnomaly = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:6", 3,
            KuhnParadigmState.NewtonCompletionEdgeId);
        badAnomaly.AnomalyCount = 1;
        Check(!badAnomaly.IsUsable(), "An anomaly count without its recorded action must be rejected.");

        var blank = KuhnParadigmState.Create(" ");
        Check(!blank.IsUsable(), "Kuhn must reject a blank instance id.");

        var missingRequired = NewtonJourney(graph);
        missingRequired.CompletedEdgeIds.Remove(KuhnParadigmState.NewtonRequiredEdgeId);
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:7", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), missingRequired),
            "A fixed record must reject a journey that never took the route's required edge.");

        var wrongSource = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:BAD:8", 3,
            KuhnParadigmState.NewtonCompletionEdgeId);
        wrongSource.RouteSourceNodeId = KuhnParadigmState.ComteSourceNodeId;
        Check(!KuhnParadigmFlow.IsRouteConsistent(wrongSource, NewtonJourney(graph)),
            "A fixed record must reject a source node that does not belong to its edge.");
    }

    private static void CheckRouteConsistency(WesternRouteGraph graph)
    {
        var newton = NewtonJourney(graph);
        var comte = ComteJourney(graph);
        Check(KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:1", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), newton),
            "Newton's route must open Kuhn's fixed room.");
        Check(KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:2", 3,
                    KuhnParadigmState.ComteCompletionEdgeId), comte),
            "Comte's route must open Kuhn's fixed room.");
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:3", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), comte),
            "Newton's edge must not open on Comte's node.");
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:4", 3,
                    KuhnParadigmState.ComteCompletionEdgeId), newton),
            "Comte's edge must not open on Newton's node.");

        var wrongNode = NewtonJourney(graph);
        wrongNode.CurrentNodeId = "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE";
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:5", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), wrongNode),
            "Kuhn must not open away from the route's source node.");

        var alreadyDeclined = NewtonJourney(graph);
        alreadyDeclined.DeclinedFixedActs.Add(3);
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:6", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), alreadyDeclined),
            "Kuhn must not reopen after the act's fixed slot was refused.");

        var retained = NewtonJourney(graph);
        retained.RetainedEnding = true;
        Check(!KuhnParadigmFlow.IsRouteConsistent(
                KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:7", 3,
                    KuhnParadigmState.NewtonCompletionEdgeId), retained),
            "Kuhn must not open after a retained ending.");

        var adopted = NewtonJourney(graph);
        var adoptedState = KuhnParadigmState.CreateAt("KUHN_PARADIGM:TEST:ROUTE:8", 3,
            KuhnParadigmState.NewtonCompletionEdgeId);
        KuhnParadigmFlow.TryPrepare(adoptedState, KuhnParadigmAction.StudyNewClassification);
        KuhnParadigmFlow.TryCommitPending(adoptedState, adopted, graph);
        KuhnParadigmFlow.TryPrepare(adoptedState, KuhnParadigmAction.TrainTeam);
        KuhnParadigmFlow.TryCommitPending(adoptedState, adopted, graph);
        Check(KuhnParadigmFlow.IsRouteConsistent(adoptedState, adopted),
            "An adopted fixed record must remain consistent with its own journey.");
        adopted.CurrentNodeId = KuhnParadigmState.NewtonSourceNodeId;
        Check(!KuhnParadigmFlow.IsRouteConsistent(adoptedState, adopted),
            "An adopted fixed record must reject a journey that did not enter Kuhn.");
    }

    private static void CheckTriggerPolicy()
    {
        KuhnParadigmTriggerContext context = new(
            true, true, true, 2, true, 1, KuhnParadigmState.NewtonSourceNodeId, 3, true, true,
            false, false, false, false, true, 3);
        Check(KuhnParadigmTriggerPolicy.ShouldInsert(context),
            "Kuhn is offered at the third-act boundary on Newton's route.");
        Check(KuhnParadigmTriggerPolicy.ShouldInsert(context with
            { CurrentNodeId = KuhnParadigmState.ComteSourceNodeId }),
            "Kuhn is offered at the third-act boundary on Comte's route.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Kuhn is not a second-act event.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { RouteAct = 2 }),
            "Kuhn's fixed handover is third-act only.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with
            { CurrentNodeId = KuhnParadigmState.AdoptedNodeId }),
            "Kuhn cannot reopen from its own node.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { LastFixedAct = 2 }),
            "Kuhn needs the third act's fixed slot already taken.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Kuhn needs the route's required second-act edge.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Kuhn needs its own fixed edge to be eligible.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with
            { HasSavedInstance = true, SavedInstanceUsable = false, SavedRouteConsistent = false }),
            "An unusable saved record must not reopen.");
        Check(KuhnParadigmTriggerPolicy.ShouldInsert(context with
            { HasSavedInstance = true, SavedInstanceUsable = true, SavedRouteConsistent = true }),
            "A consistent saved record must reopen at its boundary.");
    }

    private static void CheckLegacyQuestionMode(WesternRouteGraph graph)
    {
        var state = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:LEGACY");
        Check(!state.FixedMode && state.ActIndex == 3
            && state.EdgeId == KuhnParadigmState.LegacyCompletionEdgeId && state.IsUsable(),
            "Create must keep the old question record readable.");

        var adopted = LegacyJourney(graph);
        Check(KuhnParadigmFlow.IsRouteConsistent(state, adopted),
            "The legacy record must open on Newton's old question slot.");
        KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.StudyNewClassification);
        Check(KuhnParadigmFlow.TryCommitPending(state, adopted, graph),
            "The legacy record must reach its decision page.");
        KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.TrainTeam);
        Check(KuhnParadigmFlow.TryCommitPending(state, adopted, graph),
            "The legacy record must still take the old 090 question edge.");
        Check(state.EdgeCommitted && adopted.CompletedEdgeIds.Contains(KuhnParadigmState.LegacyCompletionEdgeId)
            && adopted.CurrentNodeId == KuhnParadigmState.AdoptedNodeId
            && adopted.ResolvedQuestionActs.Contains(3),
            "The legacy adoption must keep its question slot and enter Kuhn.");

        var declinedState = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:LEGACY:2");
        var declined = LegacyJourney(graph);
        KuhnParadigmFlow.TryPrepare(declinedState, KuhnParadigmAction.CheckInstruments);
        KuhnParadigmFlow.TryCommitPending(declinedState, declined, graph);
        KuhnParadigmFlow.TryPrepare(declinedState, KuhnParadigmAction.KeepOldMethod);
        Check(KuhnParadigmFlow.TryCommitPending(declinedState, declined, graph),
            "The legacy record must still be able to decline its question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == KuhnParadigmState.NewtonSourceNodeId
            && !declined.CompletedEdgeIds.Contains(KuhnParadigmState.LegacyCompletionEdgeId),
            "The legacy refusal must stay on Newton and consume only the question slot.");
    }

    private static WesternJourneyState NewtonJourney(WesternRouteGraph graph)
    {
        var journey = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_165"),
            "The early first-act question must turn the knowledge line toward Bacon.");
        Check(graph.TryApply(journey, KuhnParadigmState.NewtonRequiredEdgeId),
            "Bacon's second-act fixed handover must reach Newton.");
        Check(journey.CurrentNodeId == KuhnParadigmState.NewtonSourceNodeId
            && journey.RouteId == KuhnParadigmState.ScientificRouteId
            && journey.CompletedEdgeIds.Contains(KuhnParadigmState.NewtonRequiredEdgeId),
            "The scientific route must reach Newton with 166 completed.");
        // The act boundary moves the position on before asking the room.
        journey.LastFixedAct = 3;
        return journey;
    }

    private static WesternJourneyState ComteJourney(WesternRouteGraph graph)
    {
        var journey = graph.Start("ROUSSEAU__HISTORY_AND_POWER__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_197"),
            "The early first-act question must turn the social line toward Bacon.");
        Check(graph.TryApply(journey, KuhnParadigmState.ComteRequiredEdgeId),
            "Bacon's second-act fixed handover must reach Comte.");
        Check(journey.CurrentNodeId == KuhnParadigmState.ComteSourceNodeId
            && journey.RouteId == KuhnParadigmState.SocialRouteId
            && journey.CompletedEdgeIds.Contains(KuhnParadigmState.ComteRequiredEdgeId),
            "The social route must reach Comte with 198 completed.");
        journey.LastFixedAct = 3;
        return journey;
    }

    private static WesternJourneyState LegacyJourney(WesternRouteGraph graph)
    {
        var journey = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(graph.TryReserveQuestion(journey, 1) && graph.TryDecline(journey, "Fixed", 2),
            "The old Newton source is reached through real first- and second-act events.");
        Check(graph.TryApply(journey, KuhnParadigmState.LegacyRequiredEdgeId),
            "Newton's old third-act fixed edge must be adoptable.");
        Check(journey.CurrentNodeId == KuhnParadigmState.NewtonSourceNodeId && journey.LastFixedAct == 3,
            "The old knowledge line must reach Newton with act three open.");
        return journey;
    }
}
