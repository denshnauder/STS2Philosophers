using STS2Philosophers;

internal static class ComtePositivismChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = ComtePositivismState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { ComtePositivismState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:1");
        Check(ComtePositivismFlow.Options(state.Stage).Count == 3,
            "Marx must offer the three stages, keeping the anomaly, or keeping Bacon.");
        Check(ComtePositivismFlow.TryPrepare(state, ComtePositivismAction.ThreeStages),
            "Keeping the stages must be selectable.");
        var journey = Journey();
        Check(ComtePositivismFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Bacon.");
        Check(ComtePositivismFlow.TryCommitPending(state, journey, graph),
            "Keeping the stages must take the third-act question.");
        Check(state.Standard == ComtePositivismStandard.Stages && state.EdgeCommitted
            && state.Stage == ComtePositivismStage.ResultStages,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(ComtePositivismState.CompletionEdgeId)
            && journey.CurrentNodeId == ComtePositivismState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:2");
        ComtePositivismFlow.TryPrepare(kept, ComtePositivismAction.KeepBacon);
        var declined = Journey();
        Check(ComtePositivismFlow.TryCommitPending(kept, declined, graph),
            "Keeping Bacon must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == ComtePositivismState.SourceNodeId,
            "Declining must leave the player on Bacon's node.");

        var tampered = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:3");
        tampered.Standard = ComtePositivismStandard.Anomaly;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
