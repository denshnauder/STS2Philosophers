using STS2Philosophers;

internal static class BeauvoirOtherChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(WollstonecraftAdmissionState.SourceNodeId);
        Check(graph.TryApply(journey, WollstonecraftAdmissionState.CompletionEdgeId), "040 must be completed first.");
        Check(graph.TryApply(journey, MillHarmState.CompletionEdgeId), "041 must be revised first.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            BeauvoirOther = BeauvoirOtherState.Create("BEAUVOIR_OTHER:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        BeauvoirOtherAction action)
    {
        Check(BeauvoirOtherFlow.TryPrepare(shared.BeauvoirOther!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(BeauvoirOtherFlow.TryCommitPending(shared.BeauvoirOther!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!BeauvoirOtherFlow.TryCommitPending(shared.BeauvoirOther!, shared.WesternJourney!, graph),
            "A confirmed Beauvoir action cannot replay.");
        shared = Reload(shared);
        Check(BeauvoirOtherFlow.IsRouteConsistent(shared.BeauvoirOther!, shared.WesternJourney!),
            "Beauvoir survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(BeauvoirOtherFlow.IsRouteConsistent(shared.BeauvoirOther!, shared.WesternJourney!),
            "Beauvoir opens at Wollstonecraft after 041.");
        shared = Commit(shared, graph, BeauvoirOtherAction.HearTestimony);
        shared = Commit(shared, graph, BeauvoirOtherAction.RecognizeSubject);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(BeauvoirOtherState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == BeauvoirOtherState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.BeauvoirOther!.Judgment == BeauvoirJudgment.Subject,
            "Recognizing the subject completes 042 and enters Beauvoir.");
        shared = Commit(shared, graph, BeauvoirOtherAction.Leave);
        Check(shared.BeauvoirOther!.Closed, "Beauvoir closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, BeauvoirOtherAction.KeepWollstonecraft);
        Check(shared.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(BeauvoirOtherState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == BeauvoirOtherState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3,
            "Declining Beauvoir spends only the act-three fixed slot.");
        shared = Commit(shared, graph, BeauvoirOtherAction.Leave);

        shared = Fixture(graph);
        shared.WesternJourney!.CompletedEdgeIds.Remove(BeauvoirOtherState.RequiredEdgeId);
        Check(!BeauvoirOtherFlow.IsRouteConsistent(shared.BeauvoirOther!, shared.WesternJourney!),
            "Beauvoir cannot open without 041.");

        var fixedJourney = new WesternJourneyState
        {
            CurrentNodeId = BeauvoirOtherState.SourceNodeId,
            RouteId = "EDUCATION_AND_SITUATION",
            LastFixedAct = 2,
        };
        var fixedState = BeauvoirOtherState.CreateAt(
            "BEAUVOIR_OTHER:TEST:F", 3, BeauvoirOtherState.CompletionEdgeId, fixedMode: true);
        Check(BeauvoirOtherFlow.IsRouteConsistent(fixedState, fixedJourney),
            "Beauvoir's default fixed room opens after Wollstonecraft without 041.");
        Check(BeauvoirOtherFlow.TryPrepare(fixedState, BeauvoirOtherAction.HearTestimony)
            && BeauvoirOtherFlow.TryCommitPending(fixedState, fixedJourney, graph)
            && BeauvoirOtherFlow.TryPrepare(fixedState, BeauvoirOtherAction.RecognizeSubject)
            && BeauvoirOtherFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "Beauvoir's fixed room adopts 042.");
        Check(fixedJourney.CompletedEdgeIds.Contains(BeauvoirOtherState.CompletionEdgeId)
            && fixedJourney.CurrentNodeId == BeauvoirOtherState.AdoptedNodeId
            && fixedJourney.ResolvedQuestionActs.Count == 0,
            "Beauvoir's default fixed room does not spend a question slot.");

        BeauvoirOtherTriggerContext context = new(true, true, true, 2, true, 1,
            BeauvoirOtherState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(BeauvoirOtherTriggerPolicy.ShouldInsert(context),
            "Beauvoir is offered after a real act-three combat.");
        Check(!BeauvoirOtherTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Beauvoir is not an act-two event.");
        Check(!BeauvoirOtherTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Beauvoir requires 041.");
    }
}
