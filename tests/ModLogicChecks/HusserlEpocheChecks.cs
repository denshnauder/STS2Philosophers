using STS2Philosophers;

internal static class HusserlEpocheChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:1");
        Check(HusserlEpocheFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer description, the universal, or keeping Descartes.");
        Check(HusserlEpocheFlow.TryPrepare(state, HusserlEpocheAction.DescribeAppearance),
            "The description must be selectable.");
        var journey = graph.Start(HusserlEpocheState.SourceNodeId);
        Check(HusserlEpocheFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(HusserlEpocheFlow.TryCommitPending(state, journey, graph),
            "The description must take the first-act fixed link.");
        Check(state.Standard == HusserlEpocheStandard.Appearance && state.EdgeCommitted
            && state.Stage == HusserlEpocheStage.ResultAppearance,
            "The description must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HusserlEpocheState.CompletionEdgeId)
            && journey.CurrentNodeId == HusserlEpocheState.AdoptedNodeId && journey.LastFixedAct == 2,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:2");
        HusserlEpocheFlow.TryPrepare(kept, HusserlEpocheAction.KeepDescartes);
        var declined = graph.Start(HusserlEpocheState.SourceNodeId);
        Check(HusserlEpocheFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == HusserlEpocheState.SourceNodeId,
            "Declining must leave the player on Descartes' node.");

        var tampered = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:3");
        tampered.Standard = HusserlEpocheStandard.Suspension;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
