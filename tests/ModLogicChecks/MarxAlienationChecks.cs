using STS2Philosophers;

internal static class MarxAlienationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = MarxAlienationState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { MarxAlienationState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = MarxAlienationState.Create("MARX_ALIENATION:TEST:1");
        Check(MarxAlienationFlow.Options(state.Stage).Count == 3,
            "Marx must offer alienated labor, historical progress, or keeping Hegel.");
        Check(MarxAlienationFlow.TryPrepare(state, MarxAlienationAction.AlienatedLabor),
            "Alienated labor must be selectable.");
        var journey = Journey();
        Check(MarxAlienationFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Hegel.");
        Check(MarxAlienationFlow.TryCommitPending(state, journey, graph),
            "Alienated labor must take the third-act question.");
        Check(state.Standard == MarxAlienationStandard.Alienation && state.EdgeCommitted
            && state.Stage == MarxAlienationStage.ResultAlienation,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(MarxAlienationState.CompletionEdgeId)
            && journey.CurrentNodeId == MarxAlienationState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = MarxAlienationState.Create("MARX_ALIENATION:TEST:2");
        MarxAlienationFlow.TryPrepare(kept, MarxAlienationAction.KeepHegel);
        var declined = Journey();
        Check(MarxAlienationFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hegel must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == MarxAlienationState.SourceNodeId,
            "Declining must leave the player on Hegel's node.");

        var tampered = MarxAlienationState.Create("MARX_ALIENATION:TEST:3");
        tampered.Standard = MarxAlienationStandard.Progress;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
