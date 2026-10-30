using STS2Philosophers;

internal static class GadamerFusionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = GadamerFusionState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { GadamerFusionState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = GadamerFusionState.Create("GADAMER_FUSION:TEST:1");
        Check(GadamerFusionFlow.Options(state.Stage).Count == 3,
            "Gadamer must offer horizon fusion, effective history, or keeping Heidegger.");
        Check(GadamerFusionFlow.TryPrepare(state, GadamerFusionAction.HorizonFusion),
            "Horizon fusion must be selectable.");
        var journey = Journey();
        Check(GadamerFusionFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Heidegger.");
        Check(GadamerFusionFlow.TryCommitPending(state, journey, graph),
            "Horizon fusion must take the third-act question.");
        Check(state.Standard == GadamerFusionStandard.Fusion && state.EdgeCommitted
            && state.Stage == GadamerFusionStage.ResultFusion,
            "Fusion must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(GadamerFusionState.CompletionEdgeId)
            && journey.CurrentNodeId == GadamerFusionState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Gadamer and resolve act three's question.");

        var kept = GadamerFusionState.Create("GADAMER_FUSION:TEST:2");
        GadamerFusionFlow.TryPrepare(kept, GadamerFusionAction.KeepHeidegger);
        var declined = Journey();
        Check(GadamerFusionFlow.TryCommitPending(kept, declined, graph),
            "Keeping Heidegger must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == GadamerFusionState.SourceNodeId,
            "Declining must leave the player on Heidegger's node.");

        var echo = GadamerFusionState.Create("GADAMER_FUSION:TEST:4");
        GadamerFusionFlow.TryPrepare(echo, GadamerFusionAction.HorizonFusion);
        WesternJourneyState echoJourney = Journey();
        GadamerFusionFlow.TryCommitPending(echo, echoJourney, graph);
        Check(GadamerFusionFlow.Options(echo.Stage).Contains(GadamerFusionAction.HearEcho),
            "The result page must offer Ricœur's echo.");
        Check(GadamerFusionFlow.TryPrepare(echo, GadamerFusionAction.HearEcho)
            && GadamerFusionFlow.TryCommitPending(echo, echoJourney, graph)
            && echo.Stage == GadamerFusionStage.Echo,
            "Hearing the echo must open the closing page without touching the route.");
        Check(GadamerFusionFlow.TryPrepare(echo, GadamerFusionAction.Leave)
            && GadamerFusionFlow.TryCommitPending(echo, echoJourney, graph) && echo.Closed,
            "The echo page must close the room.");

        var tampered = GadamerFusionState.Create("GADAMER_FUSION:TEST:3");
        tampered.Standard = GadamerFusionStandard.History;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
