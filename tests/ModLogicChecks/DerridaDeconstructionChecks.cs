using STS2Philosophers;

internal static class DerridaDeconstructionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = DerridaDeconstructionState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { DerridaDeconstructionState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = DerridaDeconstructionState.Create("DERRIDA_DECONSTRUCTION:TEST:1");
        Check(DerridaDeconstructionFlow.Options(state.Stage).Count == 3,
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(DerridaDeconstructionFlow.TryPrepare(state, DerridaDeconstructionAction.BinaryHierarchy),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(DerridaDeconstructionFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(DerridaDeconstructionFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == DerridaDeconstructionStandard.Binary && state.EdgeCommitted
            && state.Stage == DerridaDeconstructionStage.ResultBinary,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(DerridaDeconstructionState.CompletionEdgeId)
            && journey.CurrentNodeId == DerridaDeconstructionState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = DerridaDeconstructionState.Create("DERRIDA_DECONSTRUCTION:TEST:2");
        DerridaDeconstructionFlow.TryPrepare(kept, DerridaDeconstructionAction.KeepLeviStrauss);
        var declined = Journey();
        Check(DerridaDeconstructionFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == DerridaDeconstructionState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");

        var tampered = DerridaDeconstructionState.Create("DERRIDA_DECONSTRUCTION:TEST:3");
        tampered.Standard = DerridaDeconstructionStandard.Differance;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
