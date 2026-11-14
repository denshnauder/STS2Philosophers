using STS2Philosophers;

internal static class MillHarmChecks
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
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            MillHarm = MillHarmState.Create("MILL_HARM:TEST:2"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph, MillHarmAction action)
    {
        Check(MillHarmFlow.TryPrepare(shared.MillHarm!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(MillHarmFlow.TryCommitPending(shared.MillHarm!, shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!MillHarmFlow.TryCommitPending(shared.MillHarm!, shared.WesternJourney!, graph),
            "A confirmed Mill action cannot replay.");
        shared = Reload(shared);
        Check(MillHarmFlow.IsRouteConsistent(shared.MillHarm!, shared.WesternJourney!), "Mill survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(MillHarmFlow.IsRouteConsistent(shared.MillHarm!, shared.WesternJourney!),
            "Mill opens at Wollstonecraft after 040.");
        shared = Commit(shared, graph, MillHarmAction.ApplyHarmTest);
        shared = Commit(shared, graph, MillHarmAction.ProtectConduct);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(MillHarmState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == MillHarmState.SourceNodeId
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2)
            && shared.MillHarm!.Standard == MillHarmStandard.Conduct,
            "Protecting conduct revises 041 without switching nodes.");
        shared = Commit(shared, graph, MillHarmAction.Leave);
        Check(shared.MillHarm!.Closed, "Mill closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, MillHarmAction.KeepWollstonecraft);
        Check(shared.WesternJourney!.ResolvedQuestionActs.Contains(2)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(MillHarmState.CompletionEdgeId),
            "Declining Mill spends only the act-two question slot.");
        shared = Commit(shared, graph, MillHarmAction.Leave);

        shared = Fixture(graph);
        shared.WesternJourney!.CompletedEdgeIds.Remove(MillHarmState.RequiredEdgeId);
        Check(!MillHarmFlow.IsRouteConsistent(shared.MillHarm!, shared.WesternJourney!),
            "Mill cannot open without 040.");

        var fixedJourney = new WesternJourneyState
        {
            CurrentNodeId = MillHarmState.BenthamSourceNodeId,
            RouteId = "CONSEQUENCE_AND_FREEDOM",
            LastFixedAct = 3,
        };
        var fixedState = MillHarmState.CreateAt(
            "MILL_HARM:TEST:F", 3, "WESTERN_EDGE_131", fixedMode: true);
        Check(MillHarmFlow.IsRouteConsistent(fixedState, fixedJourney),
            "Mill's fixed room opens on Bentham's third act without a question.");
        Check(MillHarmFlow.TryPrepare(fixedState, MillHarmAction.ApplyHarmTest)
            && MillHarmFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "Mill's fixed room accepts the harm test.");
        Check(MillHarmFlow.TryPrepare(fixedState, MillHarmAction.ProtectConduct)
            && MillHarmFlow.TryCommitPending(fixedState, fixedJourney, graph)
            && fixedJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_131")
            && fixedJourney.CurrentNodeId == "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE"
            && fixedJourney.ResolvedQuestionActs.Count == 0,
            "Mill's fixed room adopts 131 without spending a question slot.");

        var earlyJourney = new WesternJourneyState
        {
            CurrentNodeId = MillHarmState.BenthamSourceNodeId,
            RouteId = "CONSEQUENCE_AND_FREEDOM",
            LastFixedAct = 2,
        };
        var earlyDecline = MillHarmState.CreateAt(
            "MILL_HARM:TEST:EARLY-DECLINE", 2, "WESTERN_EDGE_066", fixedMode: true);
        Check(MillHarmFlow.IsRouteConsistent(earlyDecline, earlyJourney),
            "Mill's early room opens at act two before a decline.");
        Check(MillHarmFlow.TryPrepare(earlyDecline, MillHarmAction.KeepWollstonecraft)
            && MillHarmFlow.TryCommitPending(earlyDecline, earlyJourney, graph),
            "Mill's early decline must not be treated as an act-three advance.");
        Check(earlyJourney.LastFixedAct == 2
            && earlyJourney.DeclinedFixedActs.Contains(2)
            && !earlyJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_066")
            && MillHarmFlow.IsRouteConsistent(earlyDecline, earlyJourney),
            "Declining Mill at act two spends only that fixed slot and keeps 066 incomplete.");

        MillHarmTriggerContext context = new(true, true, true, 1, true, 1,
            MillHarmState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(MillHarmTriggerPolicy.ShouldInsert(context), "Mill is offered after a real act-two combat.");
        Check(!MillHarmTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 0 }),
            "Mill is not an act-one event.");
        Check(!MillHarmTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Mill requires 040.");
    }
}
