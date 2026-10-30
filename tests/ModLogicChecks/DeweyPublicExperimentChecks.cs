using STS2Philosophers;

internal static class DeweyPublicExperimentChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = DeweyPublicExperimentState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { DeweyPublicExperimentState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:1");
        Check(DeweyPublicExperimentFlow.Options(state.Stage).Count == 3,
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(DeweyPublicExperimentFlow.TryPrepare(state, DeweyPublicExperimentAction.PublicExperiment),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(DeweyPublicExperimentFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(DeweyPublicExperimentFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == DeweyPublicExperimentStandard.Experiment && state.EdgeCommitted
            && state.Stage == DeweyPublicExperimentStage.ResultExperiment,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(DeweyPublicExperimentState.CompletionEdgeId)
            && journey.CurrentNodeId == DeweyPublicExperimentState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:2");
        DeweyPublicExperimentFlow.TryPrepare(kept, DeweyPublicExperimentAction.KeepPeirce);
        var declined = Journey();
        Check(DeweyPublicExperimentFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == DeweyPublicExperimentState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");

        var tampered = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:3");
        tampered.Standard = DeweyPublicExperimentStandard.Community;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
