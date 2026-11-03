using STS2Philosophers;

internal static class WollstonecraftAdmissionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph) => new()
    {
        WesternJourney = graph.Start(WollstonecraftAdmissionState.SourceNodeId),
        WollstonecraftAdmission = WollstonecraftAdmissionState.Create("WOLLSTONECRAFT_ADMISSION:TEST:2"),
    };

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        WollstonecraftAdmissionAction action)
    {
        Check(WollstonecraftAdmissionFlow.TryPrepare(shared.WollstonecraftAdmission!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(WollstonecraftAdmissionFlow.TryCommitPending(shared.WollstonecraftAdmission!,
            shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!WollstonecraftAdmissionFlow.TryCommitPending(shared.WollstonecraftAdmission!,
            shared.WesternJourney!, graph), "A confirmed admission action cannot replay.");
        shared = Reload(shared);
        Check(WollstonecraftAdmissionFlow.IsRouteConsistent(shared.WollstonecraftAdmission!,
            shared.WesternJourney!), "Wollstonecraft survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(WollstonecraftAdmissionFlow.IsRouteConsistent(shared.WollstonecraftAdmission!, shared.WesternJourney!),
            "Wollstonecraft opens at Rousseau's history node.");
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.CompareRecords);
        Check(shared.WollstonecraftAdmission!.EvidenceChecked
            && shared.WollstonecraftAdmission.Clause == WollstonecraftAdmissionState.OriginalClause,
            "The old clause is recorded before judgment.");
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.UnifyRules);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == WollstonecraftAdmissionState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.WollstonecraftAdmission!.Loss == WollstonecraftLoss.RecommendationPriority,
            "Unifying the rule completes 040 and records the player's loss.");
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.Leave);
        Check(shared.WollstonecraftAdmission!.Closed, "Wollstonecraft closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.CompareRecords);
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.KeepPractice);
        Check(shared.WollstonecraftAdmission!.FixedDeclined
            && !shared.WollstonecraftAdmission.EdgeCommitted
            && !shared.WesternJourney!.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.WesternJourney.DeclinedFixedActs.Contains(2),
            "Keeping the unequal practice declines 040 instead of entering Wollstonecraft.");
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.Leave);

        shared = Fixture(graph);
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.KeepRousseau);
        Check(shared.WesternJourney!.DeclinedFixedActs.Contains(2)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 2,
            "Declining Wollstonecraft spends only the act-two fixed slot.");
        shared = Commit(shared, graph, WollstonecraftAdmissionAction.Leave);

        shared = Fixture(graph);
        shared.WesternJourney!.CompletedEdgeIds.Add(WollstonecraftAdmissionState.RivalEdgeId);
        Check(!WollstonecraftAdmissionFlow.IsRouteConsistent(shared.WollstonecraftAdmission!, shared.WesternJourney!),
            "Wollstonecraft cannot coexist with the rival 046 route.");

        WollstonecraftAdmissionTriggerContext context = new(true, true, true, 1, true, 1,
            WollstonecraftAdmissionState.SourceNodeId, 1, true, false, false, false, false, false, true);
        Check(WollstonecraftAdmissionTriggerPolicy.ShouldInsert(context),
            "Wollstonecraft is offered after a real act-two combat.");
        Check(!WollstonecraftAdmissionTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 0 }),
            "Wollstonecraft is not an act-one event.");
        Check(!WollstonecraftAdmissionTriggerPolicy.ShouldInsert(context with { RivalEdgeCompleted = true }),
            "Wollstonecraft is suppressed by the rival 046 route.");
        Check(!WollstonecraftAdmissionTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Wollstonecraft requires an eligible 040.");
        // The early turn (192) already grants her in act one. The act-two fixed slot
        // must then stand down instead of granting the same figure a second time,
        // and the line must still be able to move on.
        WesternRouteGraph earlyGraph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState early = earlyGraph.Start(WollstonecraftAdmissionState.SourceNodeId);
        early.RouteId = "EDUCATION_AND_SITUATION"; early.RouteRules = true;
        Check(earlyGraph.TryApply(early, "WESTERN_EDGE_192")
            && early.SeenThinkerIds.Contains("MARY_WOLLSTONECRAFT"),
            "The early turn hands Wollstonecraft out in act one.");
        Check(!earlyGraph.GetEligibleEdges(early, "Fixed").Any(edge => edge.EdgeId == WollstonecraftAdmissionState.CompletionEdgeId),
            "After the early turn the fixed slot must not grant Wollstonecraft again.");
        // Declining the fixed room is a different outcome from the rival 046 line
        // taking the slot; it must not be read as "046 was completed".
        var declined = WollstonecraftAdmissionState.Create("WOLLSTONECRAFT_ADMISSION:TEST:2");
        declined.FixedDeclined = true;
        Check(!WollstonecraftAdmissionFlow.IsRouteConsistent(declined, earlyGraph.Start(WollstonecraftAdmissionState.SourceNodeId)),
            "A declined record cannot claim a route that never reached her node.");
    }
}
