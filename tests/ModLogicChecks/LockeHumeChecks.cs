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
            HumeInduction = HumeInductionState.Create("HUME_INDUCTION:TEST:2"),
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

    private static void CheckFixedRelease(WesternRouteGraph graph)
    {
        // Use production graph transitions: no completed-edge or question prefill.
        foreach (LockeCargoAction evidence in new[] { LockeCargoAction.CheckMemory, LockeCargoAction.CheckSignature, LockeCargoAction.CheckMark })
        foreach (LockeCargoAction judgment in new[] { LockeCargoAction.ReturnByMemory, LockeCargoAction.SealAndWitness, LockeCargoAction.ConfirmDutyOnly })
        {
            if (judgment == LockeCargoAction.ReturnByMemory && evidence != LockeCargoAction.CheckMemory) continue;
            var shared = new PhilosophyRunState
            {
                WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
                LockeCargoIdentity = LockeCargoIdentityState.CreateAt("LOCKE_CARGO_IDENTITY:FIXED:2", 2),
            };
            shared = CommitLocke(shared, graph, evidence);
            shared = CommitLocke(shared, graph, judgment);
            shared = CommitLocke(shared, graph, LockeCargoAction.Leave);
            Check(shared.WesternJourney!.LastFixedAct == 2
                && shared.WesternJourney.CompletedEdgeIds.Contains(LockeCargoIdentityState.SecondActEdgeId)
                && shared.WesternJourney.ResolvedQuestionActs.Count == 0, "Locke's fixed handover uses no question and records act two.");
            foreach (HumeInductionAction result in new[] { HumeInductionAction.RepairFirst, HumeInductionAction.KeepPrediction, HumeInductionAction.RetractNecessity })
            {
                var branch = Reload(shared);
                branch.HumeInduction = HumeInductionState.CreateAt("HUME_INDUCTION:FIXED:3", 3);
                branch = CommitHume(branch, graph, HumeInductionAction.TestOnce);
                branch = CommitHume(branch, graph, result);
                branch = CommitHume(branch, graph, HumeInductionAction.Leave);
                Check(branch.WesternJourney!.LastFixedAct == 3 && branch.HumeInduction!.Closed
                    && branch.WesternJourney.CompletedEdgeIds.Contains(HumeInductionState.ActThreeEdgeId)
                    && branch.WesternJourney.ResolvedQuestionActs.Count == 0, "The default line closes without an extra question.");
            }
            var refusal = Reload(shared);
            refusal.HumeInduction = HumeInductionState.CreateAt("HUME_INDUCTION:FIXED:3", 3);
            refusal = CommitHume(refusal, graph, HumeInductionAction.KeepLocke);
            refusal = CommitHume(refusal, graph, HumeInductionAction.Leave);
            Check(refusal.WesternJourney!.DeclinedFixedActs.Contains(3)
                && refusal.WesternJourney.ResolvedQuestionActs.Count == 0, "Hume's refusal consumes only fixed act three.");
        }
        var declined = new PhilosophyRunState
        {
            WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
            LockeCargoIdentity = LockeCargoIdentityState.CreateAt("LOCKE_CARGO_IDENTITY:REFUSE:2", 2),
        };
        declined = CommitLocke(declined, graph, LockeCargoAction.KeepDescartes);
        declined = CommitLocke(declined, graph, LockeCargoAction.Leave);
        Check(declined.WesternJourney!.DeclinedFixedActs.Contains(2)
            && declined.WesternJourney.ResolvedQuestionActs.Count == 0
            && declined.WesternJourney.CurrentNodeId == LockeCargoIdentityState.SourceNodeId,
            "Locke refusal preserves Descartes and cannot take a question slot.");
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        CheckFixedRelease(graph);
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
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(HumeInductionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == HumeInductionState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.HumeInduction!.Basis == HumeBasis.Tested,
            "Repairing first completes 027 and enters Hume.");
        shared = CommitHume(shared, graph, HumeInductionAction.Leave);
        Check(shared.HumeInduction!.Closed, "Hume closes after Leave.");

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
            HumeInduction = HumeInductionState.Create("HUME_INDUCTION:TEST:2"),
        };
        Check(!HumeInductionFlow.IsRouteConsistent(shared.HumeInduction!, shared.WesternJourney!),
            "Hume cannot open without 026.");

        LockeCargoIdentityTriggerContext locke = new(true, true, true, 0, true, 1,
            LockeCargoIdentityState.SourceNodeId, 1, true, false, false, false, false, true);
        Check(LockeCargoIdentityTriggerPolicy.ShouldInsert(locke), "Locke is offered after a real act-one combat.");
        Check(!LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { CurrentActIndex = 1 }),
            "Locke is not an act-two event.");
        Check(!LockeCargoIdentityTriggerPolicy.ShouldInsert(locke with { EdgeEligible = false }),
            "Locke requires an eligible 026.");

        HumeInductionTriggerContext hume = new(true, true, true, 1, true, 1,
            HumeInductionState.SourceNodeId, 1, true, true, false, false, false, false, true);
        Check(HumeInductionTriggerPolicy.ShouldInsert(hume), "Hume is offered after a real act-two combat.");
        Check(!HumeInductionTriggerPolicy.ShouldInsert(hume with { RequiredEdgeCompleted = false }),
            "Hume requires 026.");
        Check(!HumeInductionTriggerPolicy.ShouldInsert(hume with { CompletionEdgeEligible = false }),
            "Hume requires an eligible 027.");
    }
}
