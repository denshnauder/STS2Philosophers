using STS2Philosophers;

internal static class HorkheimerReasonChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = HorkheimerReasonState.SourceNodeId,
        LastFixedAct = 2,
        CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_083" },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HorkheimerReasonState.Create("HORKHEIMER_REASON:TEST:1");
        Check(HorkheimerReasonFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(HorkheimerReasonFlow.TryPrepare(state, HorkheimerReasonAction.InstrumentalReason),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(HorkheimerReasonFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(HorkheimerReasonFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == HorkheimerReasonStandard.Reason && state.EdgeCommitted
            && state.Stage == HorkheimerReasonStage.ResultReason,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HorkheimerReasonState.CompletionEdgeId)
            && journey.CurrentNodeId == HorkheimerReasonState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = HorkheimerReasonState.Create("HORKHEIMER_REASON:TEST:2");
        HorkheimerReasonFlow.TryPrepare(kept, HorkheimerReasonAction.KeepMarx);
        var declined = Journey();
        Check(HorkheimerReasonFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == HorkheimerReasonState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = HorkheimerReasonState.Create("HORKHEIMER_REASON:TEST:3");
        tampered.Standard = HorkheimerReasonStandard.Culture;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
