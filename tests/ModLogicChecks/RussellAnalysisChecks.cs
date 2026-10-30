using STS2Philosophers;

internal static class RussellAnalysisChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = RussellAnalysisState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = RussellAnalysisState.Create("RUSSELL_ANALYSIS:TEST:1");
        Check(RussellAnalysisFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(RussellAnalysisFlow.TryPrepare(state, RussellAnalysisAction.DefiniteDescription),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(RussellAnalysisFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(RussellAnalysisFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == RussellAnalysisStandard.Description && state.EdgeCommitted
            && state.Stage == RussellAnalysisStage.ResultDescription,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(RussellAnalysisState.CompletionEdgeId)
            && journey.CurrentNodeId == RussellAnalysisState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = RussellAnalysisState.Create("RUSSELL_ANALYSIS:TEST:2");
        RussellAnalysisFlow.TryPrepare(kept, RussellAnalysisAction.KeepFrege);
        var declined = Journey();
        Check(RussellAnalysisFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == RussellAnalysisState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = RussellAnalysisState.Create("RUSSELL_ANALYSIS:TEST:3");
        tampered.Standard = RussellAnalysisStandard.Atomism;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
