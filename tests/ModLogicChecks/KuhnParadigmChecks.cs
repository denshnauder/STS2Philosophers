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

        var journey = NewtonJourney(graph);
        var state = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:1");
        Check(KuhnParadigmFlow.Options(state.Stage).Count == 2,
            "Kuhn must first offer the old-paradigm check or the incompatible classification.");
        Check(!KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            "Kuhn cannot commit before a first action is prepared.");
        KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.StudyNewClassification);
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            "Studying the new classification must reach the decision page.");
        Check(state.Stage == KuhnParadigmStage.Decision && state.IncomparableDimensions == 2
            && state.AnomalyCount == 1 && !state.EdgeCommitted && state.Cost == 0m,
            "The first step records the anomaly and the incomparable dimensions only.");
        KuhnParadigmFlow.TryPrepare(state, KuhnParadigmAction.TrainTeam);
        Check(KuhnParadigmFlow.TryCommitPending(state, journey, graph),
            "Training on the new scale must take the act-three question edge.");
        Check(state.Standard == KuhnParadigmStandard.Trained && state.EdgeCommitted
            && state.Cost == KuhnParadigmState.TrainTeamGoldCost
            && journey.CompletedEdgeIds.Contains(KuhnParadigmState.CompletionEdgeId)
            && journey.CurrentNodeId == KuhnParadigmState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Training adopts 090, enters Kuhn and spends the act's question slot.");

        var kept = NewtonJourney(graph);
        var keptState = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:2");
        KuhnParadigmFlow.TryPrepare(keptState, KuhnParadigmAction.CheckInstruments);
        Check(KuhnParadigmFlow.TryCommitPending(keptState, kept, graph),
            "The instrument check must also reach the decision page.");
        KuhnParadigmFlow.TryPrepare(keptState, KuhnParadigmAction.KeepOldMethod);
        Check(KuhnParadigmFlow.TryCommitPending(keptState, kept, graph),
            "Keeping the old method must decline the act's question.");
        Check(keptState.Standard == KuhnParadigmStandard.OldMethod && keptState.QuestionDeclined
            && keptState.Cost == 0m && kept.ResolvedQuestionActs.Contains(3)
            && kept.CurrentNodeId == KuhnParadigmState.SourceNodeId,
            "Keeping the old method costs nothing and leaves the run on Newton.");

        var parallel = NewtonJourney(graph);
        var parallelState = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:3");
        KuhnParadigmFlow.TryPrepare(parallelState, KuhnParadigmAction.CheckInstruments);
        KuhnParadigmFlow.TryCommitPending(parallelState, parallel, graph);
        KuhnParadigmFlow.TryPrepare(parallelState, KuhnParadigmAction.ObserveInParallel);
        Check(KuhnParadigmFlow.TryCommitPending(parallelState, parallel, graph),
            "Parallel observation must also decline the act's question.");
        Check(parallelState.Standard == KuhnParadigmStandard.Parallel && parallelState.QuestionDeclined
            && !parallel.CompletedEdgeIds.Contains(KuhnParadigmState.CompletionEdgeId),
            "Parallel observation must not enter Kuhn's node.");

        var tampered = KuhnParadigmState.Create("KUHN_PARADIGM:TEST:4");
        tampered.Standard = KuhnParadigmStandard.Trained;
        Check(!tampered.IsUsable(), "A standard without its recorded actions must be rejected.");
        var blank = KuhnParadigmState.Create(" ");
        Check(!blank.IsUsable(), "Kuhn must reject a blank instance id.");
        var stale = NewtonJourney(graph);
        stale.CurrentNodeId = "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE";
        Check(!KuhnParadigmFlow.IsRouteConsistent(KuhnParadigmState.Create("KUHN_PARADIGM:TEST:5"), stale),
            "Kuhn must not open away from Newton's node.");
        var noPredecessor = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        noPredecessor.LastFixedAct = 3;
        Check(!KuhnParadigmFlow.IsRouteConsistent(KuhnParadigmState.Create("KUHN_PARADIGM:TEST:6"), noPredecessor),
            "Kuhn must not open before Newton's edge is adopted.");

        KuhnParadigmTriggerContext context = new(true, true, true, 2, true, 1,
            KuhnParadigmState.SourceNodeId, 3, true, true, false, false, false, false, true);
        Check(KuhnParadigmTriggerPolicy.ShouldInsert(context), "Kuhn is offered after a third-act combat on Newton.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Kuhn is not a second-act event.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { CurrentNodeId = KuhnParadigmState.AdoptedNodeId }),
            "Kuhn cannot reopen from its own node.");
        Check(!KuhnParadigmTriggerPolicy.ShouldInsert(context with { LastFixedAct = 1 }),
            "Kuhn needs the third act's Fixed slot already taken.");
    }

    private static WesternJourneyState NewtonJourney(WesternRouteGraph graph)
    {
        var journey = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(graph.TryReserveQuestion(journey, 1) && graph.TryDecline(journey, "Fixed", 2),
            "The Newton source node is reached through real first- and second-act events.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_049"), "Newton's third-act edge must be adoptable.");
        Check(journey.CurrentNodeId == KuhnParadigmState.SourceNodeId && journey.LastFixedAct == 3,
            "The knowledge line must reach Newton with act three open.");
        return journey;
    }
}
