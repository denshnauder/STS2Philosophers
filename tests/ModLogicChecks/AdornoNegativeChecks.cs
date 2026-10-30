using STS2Philosophers;

internal static class AdornoNegativeChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = AdornoNegativeState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { AdornoNegativeState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = AdornoNegativeState.Create("ADORNO_NEGATIVE:TEST:1");
        Check(AdornoNegativeFlow.Options(state.Stage).Count == 3,
            "Gadamer must offer horizon fusion, effective history, or keeping Heidegger.");
        Check(AdornoNegativeFlow.TryPrepare(state, AdornoNegativeAction.NegativeDialectics),
            "Horizon fusion must be selectable.");
        var journey = Journey();
        Check(AdornoNegativeFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Heidegger.");
        Check(AdornoNegativeFlow.TryCommitPending(state, journey, graph),
            "Horizon fusion must take the third-act question.");
        Check(state.Standard == AdornoNegativeStandard.Negative && state.EdgeCommitted
            && state.Stage == AdornoNegativeStage.ResultNegative,
            "Fusion must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(AdornoNegativeState.CompletionEdgeId)
            && journey.CurrentNodeId == AdornoNegativeState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Gadamer and resolve act three's question.");

        var kept = AdornoNegativeState.Create("ADORNO_NEGATIVE:TEST:2");
        AdornoNegativeFlow.TryPrepare(kept, AdornoNegativeAction.KeepHorkheimer);
        var declined = Journey();
        Check(AdornoNegativeFlow.TryCommitPending(kept, declined, graph),
            "Keeping Heidegger must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == AdornoNegativeState.SourceNodeId,
            "Declining must leave the player on Heidegger's node.");

        var tampered = AdornoNegativeState.Create("ADORNO_NEGATIVE:TEST:3");
        tampered.Standard = AdornoNegativeStandard.Critique;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
