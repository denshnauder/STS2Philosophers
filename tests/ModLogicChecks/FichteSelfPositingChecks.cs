using STS2Philosophers;

internal static class FichteSelfPositingChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture()
    {
        var journey = new WesternJourneyState
        {
            CurrentNodeId = FichteSelfPositingState.SourceNodeId,
            LastFixedAct = 3,
        };
        journey.CompletedEdgeIds.Add(FichteSelfPositingState.RequiredEdgeId);
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            FichteSelfPositing = FichteSelfPositingState.Create("FICHTE_SELF_POSITING:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        FichteSelfPositingAction action)
    {
        Check(FichteSelfPositingFlow.TryPrepare(shared.FichteSelfPositing!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(FichteSelfPositingFlow.TryCommitPending(shared.FichteSelfPositing!,
            shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!FichteSelfPositingFlow.TryCommitPending(shared.FichteSelfPositing!,
            shared.WesternJourney!, graph), "A confirmed Fichte action cannot replay.");
        shared = Reload(shared);
        Check(FichteSelfPositingFlow.IsRouteConsistent(shared.FichteSelfPositing!,
            shared.WesternJourney!), "Fichte survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture();
        Check(FichteSelfPositingFlow.IsRouteConsistent(shared.FichteSelfPositing!,
            shared.WesternJourney!), "Fichte opens at Kant after 022.");
        Check(graph.GetEligibleEdges(shared.WesternJourney!, "Question")
            .Any(edge => edge.EdgeId == FichteSelfPositingState.CompletionEdgeId),
            "037 stays eligible at position three because it is act two's question.");
        Check(!graph.GetEligibleEdges(shared.WesternJourney!, "Fixed")
            .Any(edge => edge.EdgeId == "WESTERN_EDGE_038"),
            "038 remains blocked because 022 already spent Fixed3.");

        shared = Commit(shared, graph, FichteSelfPositingAction.ReviseAsSelfPosited);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(FichteSelfPositingState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == FichteSelfPositingState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2)
            && shared.FichteSelfPositing!.SelfPosited
            && shared.FichteSelfPositing.EdgeCommitted,
            "Revising completes 037, keeps Kant, and records self-positing.");
        Check(!graph.GetEligibleEdges(shared.WesternJourney!, "Fixed")
            .Any(edge => edge.EdgeId == "WESTERN_EDGE_038"),
            "Completing 037 still does not release Fixed3 for 038.");
        shared = Commit(shared, graph, FichteSelfPositingAction.Leave);
        Check(shared.FichteSelfPositing!.Closed, "Fichte closes after Leave.");

        shared = Fixture();
        shared = Commit(shared, graph, FichteSelfPositingAction.RequireCheck);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(FichteSelfPositingState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == FichteSelfPositingState.SourceNodeId
            && shared.FichteSelfPositing!.CheckRequired
            && !shared.FichteSelfPositing.SelfPosited
            && shared.FichteSelfPositing.EdgeCommitted,
            "Requiring a check completes 037 without granting a new node.");
        shared = Commit(shared, graph, FichteSelfPositingAction.Leave);

        shared = Fixture();
        shared = Commit(shared, graph, FichteSelfPositingAction.KeepKant);
        Check(shared.FichteSelfPositing!.QuestionDeclined
            && !shared.WesternJourney!.CompletedEdgeIds.Contains(FichteSelfPositingState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == FichteSelfPositingState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2),
            "Declining Fichte spends only the act-three question.");
        shared = Commit(shared, graph, FichteSelfPositingAction.Leave);

        shared = Fixture();
        shared.WesternJourney!.CompletedEdgeIds.Remove(FichteSelfPositingState.RequiredEdgeId);
        Check(!FichteSelfPositingFlow.IsRouteConsistent(shared.FichteSelfPositing!,
            shared.WesternJourney!), "Fichte cannot open without 022.");

        FichteSelfPositingTriggerContext context = new(true, true, true, 2, true, 1,
            FichteSelfPositingState.SourceNodeId, 3, true, true, false, false, false, false, true);
        Check(FichteSelfPositingTriggerPolicy.ShouldInsert(context),
            "Fichte is offered after a real act-three combat.");
        Check(!FichteSelfPositingTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Fichte is not an act-two event.");
        Check(!FichteSelfPositingTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Fichte requires 022.");
        Check(!FichteSelfPositingTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Fichte requires an eligible 037.");
        Check(!FichteSelfPositingTriggerPolicy.ShouldInsert(context with { LastFixedAct = 1 }),
            "Fichte requires the act-three fixed slot to be settled.");
    }
}
