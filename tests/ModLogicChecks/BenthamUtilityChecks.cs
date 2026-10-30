using STS2Philosophers;

internal static class BenthamUtilityChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = BenthamUtilityState.Create("BENTHAM_UTILITY:TEST:1");
        Check(BenthamUtilityFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer utility calculation, the universal, or staying with Socrates.");
        Check(BenthamUtilityFlow.TryPrepare(state, BenthamUtilityAction.AggregateUtility),
            "The utility calculation must be selectable.");
        var journey = graph.Start(BenthamUtilityState.SourceNodeId);
        Check(BenthamUtilityFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(BenthamUtilityFlow.TryCommitPending(state, journey, graph),
            "The utility calculation must take the first-act fixed link.");
        Check(state.Standard == BenthamUtilityStandard.Utility && state.EdgeCommitted
            && state.Stage == BenthamUtilityStage.ResultUtility,
            "The utility calculation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(BenthamUtilityState.CompletionEdgeId)
            && journey.CurrentNodeId == BenthamUtilityState.AdoptedNodeId && journey.LastFixedAct == 2,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = BenthamUtilityState.Create("BENTHAM_UTILITY:TEST:2");
        BenthamUtilityFlow.TryPrepare(kept, BenthamUtilityAction.KeepSocrates);
        var declined = graph.Start(BenthamUtilityState.SourceNodeId);
        Check(BenthamUtilityFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == BenthamUtilityState.SourceNodeId,
            "Declining must leave the player on Socrates' node.");

        var tampered = BenthamUtilityState.Create("BENTHAM_UTILITY:TEST:3");
        tampered.Standard = BenthamUtilityStandard.Boundary;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
