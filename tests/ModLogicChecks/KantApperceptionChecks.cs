using STS2Philosophers;

internal static class KantApperceptionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(LockeCargoIdentityState.SourceNodeId);
        Check(graph.TryApply(journey, LockeCargoIdentityState.EdgeId), "Locke 026 must be completed first.");
        Check(graph.TryApply(journey, HumeInductionState.SecondActEdgeId), "Hume must be reached in the second act first.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            KantApperception = KantApperceptionState.Create("KANT_APPERCEPTION:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        KantApperceptionAction action)
    {
        Check(KantApperceptionFlow.TryPrepare(shared.KantApperception!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(KantApperceptionFlow.TryCommitPending(shared.KantApperception!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!KantApperceptionFlow.TryCommitPending(shared.KantApperception!, shared.WesternJourney!, graph),
            "A confirmed Kant action cannot replay.");
        shared = Reload(shared);
        Check(KantApperceptionFlow.IsRouteConsistent(shared.KantApperception!, shared.WesternJourney!),
            "Kant survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(KantApperceptionFlow.IsRouteConsistent(shared.KantApperception!, shared.WesternJourney!),
            "Kant opens at Hume's self node after 027.");
        shared = Commit(shared, graph, KantApperceptionAction.InspectLedger);
        Check(shared.KantApperception!.EvidenceChecked, "The testimony is inspected before judgment.");
        shared = Commit(shared, graph, KantApperceptionAction.Constitutive);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(KantApperceptionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == KantApperceptionState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.KantApperception!.Judgment == KantUnityJudgment.Constitutive,
            "Constitutive unity completes 028 and enters Kant.");
        shared = Commit(shared, graph, KantApperceptionAction.Leave);
        Check(shared.KantApperception!.Closed, "Kant closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, KantApperceptionAction.KeepHume);
        Check(shared.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(KantApperceptionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == KantApperceptionState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3,
            "Declining Kant spends only the act-three fixed slot.");
        shared = Commit(shared, graph, KantApperceptionAction.Leave);

        shared = new PhilosophyRunState
        {
            WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
            KantApperception = KantApperceptionState.Create("KANT_APPERCEPTION:TEST:3"),
        };
        Check(!KantApperceptionFlow.IsRouteConsistent(shared.KantApperception!, shared.WesternJourney!),
            "Kant cannot open without 027.");

        KantApperceptionTriggerContext context = new(true, true, true, 2, true, 1,
            KantApperceptionState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(KantApperceptionTriggerPolicy.ShouldInsert(context),
            "Kant is offered after a real act-three combat.");
        Check(!KantApperceptionTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Kant is not an act-two event.");
        Check(!KantApperceptionTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Kant requires 027.");
        Check(!KantApperceptionTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Kant requires an eligible 028.");
    }
}
