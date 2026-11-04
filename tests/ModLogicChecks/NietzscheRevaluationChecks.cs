using STS2Philosophers;

internal static class NietzscheRevaluationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = NietzscheRevaluationState.Create("NIETZSCHE_REVALUATION:TEST:1");
        Check(NietzscheRevaluationFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer revaluation, the universal, or keeping Rousseau.");
        Check(NietzscheRevaluationFlow.TryPrepare(state, NietzscheRevaluationAction.RevalueAllValues),
            "The revaluation must be selectable.");
        var journey = graph.Start(NietzscheRevaluationState.SourceNodeId);
        journey.LastFixedAct = 2;
        Check(NietzscheRevaluationFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(NietzscheRevaluationFlow.TryCommitPending(state, journey, graph),
            "The revaluation must take the first-act fixed link.");
        Check(state.Standard == NietzscheRevaluationStandard.Revaluation && state.EdgeCommitted
            && state.Stage == NietzscheRevaluationStage.ResultRevalue,
            "The revaluation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(NietzscheRevaluationState.CompletionEdgeId)
            && journey.CurrentNodeId == NietzscheRevaluationState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = NietzscheRevaluationState.Create("NIETZSCHE_REVALUATION:TEST:2");
        NietzscheRevaluationFlow.TryPrepare(kept, NietzscheRevaluationAction.KeepRousseau);
        var declined = graph.Start(NietzscheRevaluationState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(NietzscheRevaluationFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == NietzscheRevaluationState.SourceNodeId,
            "Declining must leave the player on Rousseau' node.");


        // The two default fixed entries: the genealogy line places Nietzsche in
        // the second act, the will-and-value line in the third.
        var genealogy = NietzscheRevaluationState.CreateAt(
            "NIETZSCHE_REVALUATION:TEST:2", 2, "WESTERN_EDGE_120");
        var genealogyJourney = graph.Start(NietzscheRevaluationState.SourceNodeId);
        genealogyJourney.RouteId = "GENEALOGY_AND_POWER";
        genealogyJourney.LastFixedAct = 2;
        Check(NietzscheRevaluationFlow.IsRouteConsistent(genealogy, genealogyJourney),
            "The genealogy room must sit on the second-act fixed slot.");
        NietzscheRevaluationFlow.TryPrepare(genealogy, NietzscheRevaluationAction.GenealogyExposure);
        Check(NietzscheRevaluationFlow.TryCommitPending(genealogy, genealogyJourney, graph),
            "The genealogy choice must take the second-act fixed link.");
        Check(genealogyJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_120")
            && genealogyJourney.CurrentNodeId == NietzscheRevaluationState.AdoptedNodeId
            && genealogyJourney.LastFixedAct == 3,
            "Adopting on the genealogy line must move to Nietzsche and consume the slot.");

        var will = NietzscheRevaluationState.CreateAt(
            "NIETZSCHE_REVALUATION:TEST:3", 3, "WESTERN_EDGE_151");
        var willJourney = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        willJourney.CurrentNodeId = "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE";
        will.RouteSourceNodeId = "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE";
        willJourney.RouteId = "WILL_AND_VALUE";
        willJourney.LastFixedAct = 3;
        willJourney.CompletedEdgeIds.Add("WESTERN_EDGE_150");
        Check(NietzscheRevaluationFlow.IsRouteConsistent(will, willJourney),
            "The will-and-value room must sit on the third-act fixed slot.");
        NietzscheRevaluationFlow.TryPrepare(will, NietzscheRevaluationAction.RevalueAllValues);
        Check(NietzscheRevaluationFlow.TryCommitPending(will, willJourney, graph),
            "The revaluation choice must take the third-act fixed link.");
        Check(willJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_151")
            && willJourney.CurrentNodeId == NietzscheRevaluationState.AdoptedNodeId
            && willJourney.LastFixedAct == 3,
            "Adopting on the will-and-value line must move to Nietzsche.");

        Check(NietzscheRevaluationTriggerPolicy.ShouldInsert(new(
                true, true, true, 1, true, 1, NietzscheRevaluationState.SourceNodeId, 2, true,
                false, false, false, false, true, 2)),
            "The genealogy fixed entry must trigger at the second-act boundary.");
        Check(NietzscheRevaluationTriggerPolicy.ShouldInsert(new(
                true, true, true, 2, true, 1, "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE", 3, true,
                false, false, false, false, true, 3)),
            "The will-and-value fixed entry must trigger at the third-act boundary.");
        Check(!NietzscheRevaluationTriggerPolicy.ShouldInsert(new(
                true, true, true, 0, true, 1, NietzscheRevaluationState.SourceNodeId, 2, true,
                false, false, false, false, true, 2)),
            "The genealogy fixed entry must not trigger in the first act.");

        var tampered = NietzscheRevaluationState.Create("NIETZSCHE_REVALUATION:TEST:4");
        tampered.Standard = NietzscheRevaluationStandard.Genealogy;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
