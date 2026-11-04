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

        var earlyJourney = new WesternJourneyState
        {
            CurrentNodeId = WittgensteinGameState.EarlySourceNodeId,
            RouteId = "ORDINARY_SPEECH",
            LastFixedAct = 2,
        };
        var early = WittgensteinGameState.CreateAt(
            "WITTGENSTEIN_GAME:TEST:E", 2, WittgensteinGameState.EarlyCompletionEdgeId, fixedMode: true);
        early.FixedSourceNodeId = WittgensteinGameState.EarlySourceNodeId;
        Check(WittgensteinGameFlow.IsRouteConsistent(early, earlyJourney),
            "Early Wittgenstein opens on the second fixed slot after Aristotle.");
        Check(WittgensteinGameFlow.TryPrepare(early, WittgensteinGameAction.LanguageGame),
            "Early Wittgenstein prepares 152.");
        Check(WittgensteinGameFlow.TryCommitPending(early, earlyJourney, graph),
            "Early Wittgenstein commits 152.");
        Check(earlyJourney.CompletedEdgeIds.Contains(WittgensteinGameState.EarlyCompletionEdgeId)
            && earlyJourney.CurrentNodeId == WittgensteinGameState.AdoptedNodeId
            && earlyJourney.LastFixedAct == 3 && earlyJourney.ResolvedQuestionActs.Count == 0,
            "Early Wittgenstein takes the second fixed slot without a question.");

        var lateJourney = new WesternJourneyState
        {
            CurrentNodeId = WittgensteinGameState.AdoptedNodeId,
            RouteId = "ORDINARY_SPEECH",
            LastFixedAct = 3,
            CompletedEdgeIds = new(StringComparer.Ordinal) { WittgensteinGameState.EarlyCompletionEdgeId },
        };
        var late = WittgensteinGameState.CreateAt("WITTGENSTEIN_GAME:TEST:L", 3, string.Empty, fixedMode: true);
        late.FixedSourceNodeId = WittgensteinGameState.AdoptedNodeId;
        Check(WittgensteinGameFlow.IsRouteConsistent(late, lateJourney),
            "Late Wittgenstein opens on the third fixed slot after early Wittgenstein.");
        Check(WittgensteinGameFlow.TryPrepare(late, WittgensteinGameAction.LanguageGame)
            && WittgensteinGameFlow.TryCommitPending(late, lateJourney, graph),
            "Late Wittgenstein adopts the same-node phase.");
        Check(lateJourney.CurrentNodeId == WittgensteinGameState.AdoptedNodeId
            && lateJourney.LastFixedAct == 3 && lateJourney.ResolvedQuestionActs.Count == 0,
            "Late Wittgenstein takes the third fixed slot without a question.");

        var formalJourney = new WesternJourneyState
        {
            CurrentNodeId = WittgensteinGameState.SourceNodeId,
            RouteId = "FORMAL_LANGUAGE",
            LastFixedAct = 3,
            CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_184" },
        };
        var formal = WittgensteinGameState.CreateAt(
            "WITTGENSTEIN_GAME:TEST:F", 3, WittgensteinGameState.LateFormalCompletionEdgeId, fixedMode: true);
        formal.FixedSourceNodeId = WittgensteinGameState.SourceNodeId;
        Check(WittgensteinGameFlow.TryPrepare(formal, WittgensteinGameAction.LanguageGame)
            && WittgensteinGameFlow.TryCommitPending(formal, formalJourney, graph),
            "Formal-language Wittgenstein adopts 185 after Russell.");
        Check(formalJourney.CompletedEdgeIds.Contains(WittgensteinGameState.LateFormalCompletionEdgeId)
            && formalJourney.CurrentNodeId == WittgensteinGameState.AdoptedNodeId,
            "Formal-language Wittgenstein takes the third fixed edge.");

        var tampered = WittgensteinGameState.Create("WITTGENSTEIN_GAME:TEST:3");
        tampered.Standard = WittgensteinGameStandard.Picture;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
