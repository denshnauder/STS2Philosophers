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
        Check(NietzscheRevaluationFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(NietzscheRevaluationFlow.TryCommitPending(state, journey, graph),
            "The revaluation must take the first-act fixed link.");
        Check(state.Standard == NietzscheRevaluationStandard.Revaluation && state.EdgeCommitted
            && state.Stage == NietzscheRevaluationStage.ResultRevalue,
            "The revaluation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(NietzscheRevaluationState.CompletionEdgeId)
            && journey.CurrentNodeId == NietzscheRevaluationState.AdoptedNodeId && journey.LastFixedAct == 2,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = NietzscheRevaluationState.Create("NIETZSCHE_REVALUATION:TEST:2");
        NietzscheRevaluationFlow.TryPrepare(kept, NietzscheRevaluationAction.KeepRousseau);
        var declined = graph.Start(NietzscheRevaluationState.SourceNodeId);
        Check(NietzscheRevaluationFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == NietzscheRevaluationState.SourceNodeId,
            "Declining must leave the player on Rousseau' node.");

        var tampered = NietzscheRevaluationState.Create("NIETZSCHE_REVALUATION:TEST:3");
        tampered.Standard = NietzscheRevaluationStandard.Genealogy;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
