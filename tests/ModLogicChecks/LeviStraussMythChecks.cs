using STS2Philosophers;

internal static class LeviStraussMythChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = LeviStraussMythState.SourceNodeId,
        LastFixedAct = 2,
        CompletedEdgeIds = ["WESTERN_EDGE_080"],
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:1");
        Check(LeviStraussMythFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(LeviStraussMythFlow.TryPrepare(state, LeviStraussMythAction.StructuralReading),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(LeviStraussMythFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(LeviStraussMythFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == LeviStraussMythStandard.Structure && state.EdgeCommitted
            && state.Stage == LeviStraussMythStage.ResultStructure,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(LeviStraussMythState.CompletionEdgeId)
            && journey.CurrentNodeId == LeviStraussMythState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:2");
        LeviStraussMythFlow.TryPrepare(kept, LeviStraussMythAction.KeepSaussure);
        var declined = Journey();
        Check(LeviStraussMythFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == LeviStraussMythState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:3");
        tampered.Standard = LeviStraussMythStandard.History;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
