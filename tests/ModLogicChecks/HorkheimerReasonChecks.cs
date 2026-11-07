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
        RouteId = "SOCIAL_CRITIQUE",
        LastFixedAct = 2,
        CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_083" },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HorkheimerReasonState.CreateAt(
            "HORKHEIMER_REASON:TEST:1", 2, "WESTERN_EDGE_178");
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
        Check(journey.CompletedEdgeIds.Contains("WESTERN_EDGE_178")
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


        // The player's first-act Marx turn opens this room before act two's fixed
        // edge. It takes 084, the early-Marx handover that Habermas's fixed room
        // requires; the seed's 083 branch takes 178 and leads to Adorno instead.
        var early = new WesternJourneyState
        {
            CurrentNodeId = HorkheimerReasonState.SourceNodeId,
            RouteId = "SOCIAL_CRITIQUE",
            LastFixedAct = 1,
            CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_177" },
        };
        var earlyState = HorkheimerReasonState.CreateAt(
            "HORKHEIMER_REASON:TEST:E", 2, "WESTERN_EDGE_084");
        earlyState.RoutePlaced = true;
        earlyState.OpeningFixedAct = 1;
        Check(HorkheimerReasonFlow.IsRouteConsistent(earlyState, early),
            "The social-critique room must open after the first-act Marx turn.");
        Check(HorkheimerReasonFlow.TryPrepare(earlyState, HorkheimerReasonAction.InstrumentalReason)
            && HorkheimerReasonFlow.TryCommitPending(earlyState, early, graph),
            "The early room must take the second-act fixed edge.");
        Check(early.CompletedEdgeIds.Contains("WESTERN_EDGE_084")
            && early.CurrentNodeId == HorkheimerReasonState.AdoptedNodeId
            && early.LastFixedAct == 2,
            "The early room must take 084 and consume act two.");

        var tampered = HorkheimerReasonState.Create("HORKHEIMER_REASON:TEST:3");
        tampered.Standard = HorkheimerReasonStandard.Culture;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
