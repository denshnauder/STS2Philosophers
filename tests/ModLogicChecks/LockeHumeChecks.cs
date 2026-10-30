using STS2Philosophers;

internal static class LockeHumeChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState LockeFixture(WesternRouteGraph graph) => new()
    {
        WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
        LockeCargoIdentity = LockeCargoIdentityState.Create("LOCKE_CARGO_IDENTITY:TEST:1"),
    };

    private static PhilosophyRunState HumeFixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(LockeCargoIdentityState.SourceNodeId);
        Check(graph.TryApply(journey, LockeCargoIdentityState.EdgeId), "Locke 026 must be completed first.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            HumeInduction = HumeInductionState.CreateAt("HUME_INDUCTION:TEST:2", 2),
        };
    }

    private static PhilosophyRunState CommitLocke(PhilosophyRunState shared, WesternRouteGraph graph, LockeCargoAction action)
    {
        Check(LockeCargoIdentityFlow.TryPrepare(shared.LockeCargoIdentity!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(LockeCargoIdentityFlow.TryCommitPending(shared.LockeCargoIdentity!, shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!LockeCargoIdentityFlow.TryCommitPending(shared.LockeCargoIdentity!, shared.WesternJourney!, graph), "A confirmed Locke action cannot replay.");
        shared = Reload(shared);
        Check(LockeCargoIdentityFlow.IsRouteConsistent(shared.LockeCargoIdentity!, shared.WesternJourney!), "Locke survives reload.");
        return shared;
    }

    private static PhilosophyRunState CommitHume(PhilosophyRunState shared, WesternRouteGraph graph, HumeInductionAction action)
    {
        Check(HumeInductionFlow.TryPrepare(shared.HumeInduction!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(HumeInductionFlow.TryCommitPending(shared.HumeInduction!, shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!HumeInductionFlow.TryCommitPending(shared.HumeInduction!, shared.WesternJourney!, graph), "A confirmed Hume action cannot replay.");
        shared = Reload(shared);
        Check(HumeInductionFlow.IsRouteConsistent(shared.HumeInduction!, shared.WesternJourney!), "Hume survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = LockeFixture(graph);
        Check(LockeCargoIdentityFlow.IsRouteConsistent(shared.LockeCargoIdentity!, shared.WesternJourney!), "Locke opens at Descartes' self node.");
        shared = CommitLocke(shared, graph, LockeCargoAction.CheckMemory);
        shared = CommitLocke(shared, graph, LockeCargoAction.ReturnByMemory);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(LockeCargoIdentityState.EdgeId)
            && shared.WesternJourney.CurrentNodeId == LockeCargoIdentityState.AdoptedNodeId
            && shared.WesternJourney.ResolvedQuestionActs.Contains(1)
            && shared.LockeCargoIdentity!.Evidence == LockeEvidence.Memory
            && shared.LockeCargoIdentity.Judgment == LockeJudgment.Identity,
            "Returning by memory completes 026 and enters Locke.");
        shared = CommitLocke(shared, graph, LockeCargoAction.Leave);
        Check(shared.LockeCargoIdentity!.Closed, "Locke closes after Leave.");

        shared = LockeFixture(graph);
        shared = CommitLocke(shared, graph, LockeCargoAction.KeepDescartes);
        Check(shared.WesternJourney!.ResolvedQuestionActs.Contains(1)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(LockeCargoIdentityState.EdgeId)
            && shared.WesternJourney.CurrentNodeId == LockeCargoIdentityState.SourceNodeId,
            "Declining Locke spends only the act-one question slot.");
        shared = CommitLocke(shared, graph, LockeCargoAction.Leave);

        shared = LockeFixture(graph);
        Check(LockeCargoIdentityFlow.TryPrepare(shared.LockeCargoIdentity!, LockeCargoAction.CheckSignature), "Signature evidence prepares.");
        shared = Reload(shared);
        Check(!LockeCargoIdentityFlow.TryPrepare(shared.LockeCargoIdentity!, LockeCargoAction.ReturnByMemory),
            "Returning by memory requires the memory channel.");

        shared = HumeFixture(graph);
        Check(HumeInductionFlow.IsRouteConsistent(shared.HumeInduction!, shared.WesternJourney!), "Hume opens at Locke's node after 026.");
        shared = CommitHume(shared, graph, HumeInductionAction.TestOnce);
        shared = CommitHume(shared, graph, HumeInductionAction.RepairFirst);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(HumeInductionState.SecondActEdgeId)
            && shared.WesternJourney.CurrentNodeId == HumeInductionState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.HumeInduction!.Basis == HumeBasis.Tested,
            "Repairing first completes 027 and enters Hume.");
        Check(shared.HumeInduction!.Cost == HumeInductionState.RepairFirstGoldCost
            && HumeInductionState.RepairFirstGoldCost == 75m,
            "Repairing first costs the promised 75 gold.");
        Check(HumeInductionState.Create("HUME_INDUCTION:TEST:3").Cost == 0m,
            "No other Hume outcome charges gold.");
        shared = CommitHume(shared, graph, HumeInductionAction.Leave);
        Check(shared.HumeInduction!.Closed, "Hume closes after Leave.");
        var keepPrediction = HumeFixture(graph);
        keepPrediction = CommitHume(keepPrediction, graph, HumeInductionAction.TestOnce);
        keepPrediction = CommitHume(keepPrediction, graph, HumeInductionAction.KeepPrediction);
        Check(keepPrediction.HumeInduction!.Cost == 0m,
            "Keeping the prediction charges nothing.");

        shared = HumeFixture(graph);
        shared = CommitHume(shared, graph, HumeInductionAction.KeepLocke);
        Check(shared.WesternJourney!.DeclinedFixedActs.Contains(2)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(HumeInductionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == HumeInductionState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 2,
            "Declining Hume spends the act-two fixed slot without entering Hume.");
        shared = CommitHume(shared, graph, HumeInductionAction.Leave);

        shared = new PhilosophyRunState
        {
            WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
            HumeInduction = HumeInductionState.CreateAt("HUME_INDUCTION:TEST:2", 2),
        };
        Check(!HumeInductionFlow.IsRouteConsistent(shared.HumeInduction!, shared.WesternJourney!),
            "Hume cannot open without 026.");

        LockeCargoIdentityTriggerContext locke = new(true, true, true, 0, true, 1,
            LockeCargoIdentityState.SourceNodeId, 1, true, false, false, false, false, true);
        Check(LockeCargoIdentityTriggerPolicy.ShouldInsert(locke), "Locke is offered after a real act-one combat.");
        Check(LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { CurrentActIndex = 1, LastFixedAct = 2, OnDefaultRoute = true }),
            "Locke is also the default line's act-two event.");
        Check(!LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { CurrentActIndex = 1, LastFixedAct = 2 }),
            "Another route does not get Locke's act-two handover.");
        Check(!LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { CurrentActIndex = 2 }),
            "Locke is never an act-three event.");
        Check(!LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { EdgeEligible = false }),
            "Locke requires an eligible 026.");

        HumeInductionTriggerContext hume = new(true, true, true, 1, true, 1,
            HumeInductionState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(HumeInductionTriggerPolicy.ShouldInsert(hume), "Hume is offered after a real act-two combat.");
        Check(HumeInductionTriggerPolicy.ShouldInsert(hume with { CurrentActIndex = 2, LastFixedAct = 3, OnDefaultRoute = true }),
            "Hume is also the default line's act-three event.");
        Check(!HumeInductionTriggerPolicy.ShouldInsert(hume with { CurrentActIndex = 2, LastFixedAct = 3 }),
            "Another route does not get Hume's act-three handover.");
        Check(!HumeInductionTriggerPolicy.ShouldInsert(hume with { RequiredEdgeCompleted = false }),
            "Hume requires 026.");
        Check(!HumeInductionTriggerPolicy.ShouldInsert(hume with { CompletionEdgeEligible = false }),
            "Hume requires an eligible 027.");
    }
}
