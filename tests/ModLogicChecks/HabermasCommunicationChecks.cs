using STS2Philosophers;

internal static class HabermasCommunicationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = HabermasCommunicationState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { HabermasCommunicationState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HabermasCommunicationState.Create("HABERMAS_COMMUNICATION:TEST:1");
        Check(HabermasCommunicationFlow.Options(state.Stage).Count == 3,
            "Gadamer must offer horizon fusion, effective history, or keeping Heidegger.");
        Check(HabermasCommunicationFlow.TryPrepare(state, HabermasCommunicationAction.CommunicativeAction),
            "Horizon fusion must be selectable.");
        var journey = Journey();
        Check(HabermasCommunicationFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Heidegger.");
        Check(HabermasCommunicationFlow.TryCommitPending(state, journey, graph),
            "Horizon fusion must take the third-act question.");
        Check(state.Standard == HabermasCommunicationStandard.Action && state.EdgeCommitted
            && state.Stage == HabermasCommunicationStage.ResultAction,
            "Fusion must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HabermasCommunicationState.CompletionEdgeId)
            && journey.CurrentNodeId == HabermasCommunicationState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Gadamer and resolve act three's question.");

        var kept = HabermasCommunicationState.Create("HABERMAS_COMMUNICATION:TEST:2");
        HabermasCommunicationFlow.TryPrepare(kept, HabermasCommunicationAction.KeepHorkheimer);
        var declined = Journey();
        Check(HabermasCommunicationFlow.TryCommitPending(kept, declined, graph),
            "Keeping Heidegger must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == HabermasCommunicationState.SourceNodeId,
            "Declining must leave the player on Heidegger's node.");

        var tampered = HabermasCommunicationState.Create("HABERMAS_COMMUNICATION:TEST:3");
        tampered.Standard = HabermasCommunicationStandard.Ethics;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
