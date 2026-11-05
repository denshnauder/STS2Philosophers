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

        var fixedJourney = new WesternJourneyState
        {
            CurrentNodeId = DerridaDeconstructionState.SourceNodeId,
            LastFixedAct = 3,
            RouteId = "STRUCTURE_AND_EXCLUSION",
            CompletedEdgeIds = new(StringComparer.Ordinal) { DerridaDeconstructionState.FixedRequiredEdgeId },
        };
        var fixedState = DerridaDeconstructionState.CreateAt(
            "DERRIDA_DECONSTRUCTION:TEST:3", 3, DerridaDeconstructionState.FixedCompletionEdgeId, fixedMode: true);
        Check(DerridaDeconstructionFlow.IsRouteConsistent(fixedState, fixedJourney),
            "The routed third-act fixed room must open after Levi-Strauss took 187.");
        Check(DerridaDeconstructionFlow.TryPrepare(fixedState, DerridaDeconstructionAction.BinaryHierarchy)
            && DerridaDeconstructionFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "The fixed room must commit through the fixed 188 edge.");
        Check(fixedJourney.LastFixedAct == 3
            && fixedJourney.CompletedEdgeIds.Contains(DerridaDeconstructionState.FixedCompletionEdgeId)
            && fixedJourney.CurrentNodeId == DerridaDeconstructionState.AdoptedNodeId
            && !fixedJourney.ResolvedQuestionActs.Contains(3)
            && !fixedJourney.DeclinedFixedActs.Contains(3),
            "The fixed room must adopt Derrida without consuming act three's question.");
        Check(fixedState.IsUsable() && fixedState.FixedMode && fixedState.EdgeCommitted,
            "The fixed mode and its edge must survive replay.");

        var fixedKept = DerridaDeconstructionState.CreateAt(
            "DERRIDA_DECONSTRUCTION:TEST:4", 3, DerridaDeconstructionState.FixedCompletionEdgeId, fixedMode: true);
        var fixedDeclined = new WesternJourneyState
        {
            CurrentNodeId = DerridaDeconstructionState.SourceNodeId,
            LastFixedAct = 3,
            RouteId = "STRUCTURE_AND_EXCLUSION",
            CompletedEdgeIds = new(StringComparer.Ordinal) { DerridaDeconstructionState.FixedRequiredEdgeId },
        };
        Check(DerridaDeconstructionFlow.TryPrepare(fixedKept, DerridaDeconstructionAction.KeepLeviStrauss)
            && DerridaDeconstructionFlow.TryCommitPending(fixedKept, fixedDeclined, graph),
            "Keeping Levi-Strauss must decline the fixed slot.");
        Check(fixedDeclined.DeclinedFixedActs.Contains(3)
            && !fixedDeclined.ResolvedQuestionActs.Contains(3)
            && fixedDeclined.CurrentNodeId == DerridaDeconstructionState.SourceNodeId,
            "Declining the fixed room must not resolve the act's question.");

        var tampered = DerridaDeconstructionState.Create("DERRIDA_DECONSTRUCTION:TEST:5");
        tampered.Standard = DerridaDeconstructionStandard.Differance;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
        Check(!tampered.FixedMode && tampered.EdgeId == DerridaDeconstructionState.CompletionEdgeId,
            "Legacy question states must keep the question edge by default.");
    }
}
