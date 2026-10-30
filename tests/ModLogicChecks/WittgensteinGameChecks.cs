using STS2Philosophers;

internal static class WittgensteinGameChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = WittgensteinGameState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { WittgensteinGameState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = WittgensteinGameState.Create("WITTGENSTEIN_GAME:TEST:1");
        Check(WittgensteinGameFlow.Options(state.Stage).Count == 3,
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(WittgensteinGameFlow.TryPrepare(state, WittgensteinGameAction.LanguageGame),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(WittgensteinGameFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(WittgensteinGameFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == WittgensteinGameStandard.Game && state.EdgeCommitted
            && state.Stage == WittgensteinGameStage.ResultGame,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(WittgensteinGameState.CompletionEdgeId)
            && journey.CurrentNodeId == WittgensteinGameState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = WittgensteinGameState.Create("WITTGENSTEIN_GAME:TEST:2");
        WittgensteinGameFlow.TryPrepare(kept, WittgensteinGameAction.KeepRussell);
        var declined = Journey();
        Check(WittgensteinGameFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == WittgensteinGameState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");

        var tampered = WittgensteinGameState.Create("WITTGENSTEIN_GAME:TEST:3");
        tampered.Standard = WittgensteinGameStandard.Picture;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
