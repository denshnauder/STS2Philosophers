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
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(GadamerFusionFlow.TryPrepare(state, GadamerFusionAction.HorizonFusion),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(GadamerFusionFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(GadamerFusionFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == GadamerFusionStandard.Fusion && state.EdgeCommitted
            && state.Stage == GadamerFusionStage.ResultFusion,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(GadamerFusionState.CompletionEdgeId)
            && journey.CurrentNodeId == GadamerFusionState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = GadamerFusionState.Create("GADAMER_FUSION:TEST:2");
        GadamerFusionFlow.TryPrepare(kept, GadamerFusionAction.KeepHeidegger);
        var declined = Journey();
        Check(GadamerFusionFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == GadamerFusionState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");

        var tampered = GadamerFusionState.Create("GADAMER_FUSION:TEST:3");
        tampered.Standard = GadamerFusionStandard.History;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
