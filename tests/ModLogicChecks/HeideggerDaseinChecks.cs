using STS2Philosophers;

internal static class HeideggerDaseinChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = HeideggerDaseinState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HeideggerDaseinState.Create("HEIDEGGER_DASEIN:TEST:1");
        Check(HeideggerDaseinFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(HeideggerDaseinFlow.TryPrepare(state, HeideggerDaseinAction.BeingInSituation),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(HeideggerDaseinFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(HeideggerDaseinFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == HeideggerDaseinStandard.Situation && state.EdgeCommitted
            && state.Stage == HeideggerDaseinStage.ResultSituation,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HeideggerDaseinState.CompletionEdgeId)
            && journey.CurrentNodeId == HeideggerDaseinState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = HeideggerDaseinState.Create("HEIDEGGER_DASEIN:TEST:2");
        HeideggerDaseinFlow.TryPrepare(kept, HeideggerDaseinAction.KeepHusserl);
        var declined = Journey();
        Check(HeideggerDaseinFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == HeideggerDaseinState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = HeideggerDaseinState.Create("HEIDEGGER_DASEIN:TEST:3");
        tampered.Standard = HeideggerDaseinStandard.Dasein;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
