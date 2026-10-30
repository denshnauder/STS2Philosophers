using STS2Philosophers;

internal static class DeleuzeDifferenceChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = DeleuzeDifferenceState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { DeleuzeDifferenceState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = DeleuzeDifferenceState.Create("DELEUZE_DIFFERENCE:TEST:1");
        Check(DeleuzeDifferenceFlow.Options(state.Stage).Count == 3,
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(DeleuzeDifferenceFlow.TryPrepare(state, DeleuzeDifferenceAction.DifferenceRepetition),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(DeleuzeDifferenceFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(DeleuzeDifferenceFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == DeleuzeDifferenceStandard.Difference && state.EdgeCommitted
            && state.Stage == DeleuzeDifferenceStage.ResultDifference,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(DeleuzeDifferenceState.CompletionEdgeId)
            && journey.CurrentNodeId == DeleuzeDifferenceState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = DeleuzeDifferenceState.Create("DELEUZE_DIFFERENCE:TEST:2");
        DeleuzeDifferenceFlow.TryPrepare(kept, DeleuzeDifferenceAction.KeepSchelling);
        var declined = Journey();
        Check(DeleuzeDifferenceFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");

        var tampered = DeleuzeDifferenceState.Create("DELEUZE_DIFFERENCE:TEST:3");
        tampered.Standard = DeleuzeDifferenceStandard.Rhizome;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
