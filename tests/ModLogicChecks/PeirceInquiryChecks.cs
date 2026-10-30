using STS2Philosophers;

internal static class PeirceInquiryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = PeirceInquiryState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:1");
        Check(PeirceInquiryFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.TestByConsequences),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(PeirceInquiryFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(PeirceInquiryFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == PeirceInquiryStandard.Consequences && state.EdgeCommitted
            && state.Stage == PeirceInquiryStage.ResultTest,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(PeirceInquiryState.CompletionEdgeId)
            && journey.CurrentNodeId == PeirceInquiryState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:2");
        PeirceInquiryFlow.TryPrepare(kept, PeirceInquiryAction.KeepHume);
        var declined = Journey();
        Check(PeirceInquiryFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == PeirceInquiryState.SourceNodeId,
            "Declining must leave the player on Hume's node.");

        var tampered = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:3");
        tampered.Standard = PeirceInquiryStandard.Community;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
