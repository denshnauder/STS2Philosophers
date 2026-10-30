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
        Check(HusserlEpocheFlow.Options(state.Stage).Count == 4,
            "Husserl must offer description, suspension, the embodied direction, or keeping Descartes.");
        Check(HusserlEpocheFlow.TryPrepare(state, HusserlEpocheAction.DescribeAppearance),
            "The description must be selectable.");
        var journey = graph.Start(HusserlEpocheState.SourceNodeId);
        journey.RouteRules = true;
        journey.LastFixedAct = 2;
        Check(HusserlEpocheFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(HusserlEpocheFlow.TryCommitPending(state, journey, graph),
            "The description must take the first-act fixed link.");
        Check(state.Standard == HusserlEpocheStandard.Appearance && state.EdgeCommitted
            && state.Stage == HusserlEpocheStage.ResultAppearance,
            "The description must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HusserlEpocheState.CompletionEdgeId)
            && journey.CurrentNodeId == HusserlEpocheState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:2");
        HusserlEpocheFlow.TryPrepare(kept, HusserlEpocheAction.KeepDescartes);
        var declined = graph.Start(HusserlEpocheState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(HusserlEpocheFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == HusserlEpocheState.SourceNodeId,
            "Declining must leave the player on Descartes' node.");

        // Husserl stands on two lines. Describing the appearance names the
        // appearance line; the direction option names the embodied one, and the
        // run then walks that line's own handovers instead of dead-ending.
        Check(journey.RouteId == "APPEARANCE_AND_INTERPRETATION",
            "Describing the appearance must name the appearance line.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_164")
            && graph.TryApply(journey, "WESTERN_EDGE_161") && graph.IsComplete(journey),
            "The appearance line must reach its own closing figure.");

        var embodied = HusserlEpocheState.CreateAt("HUSSERL_EPOCHE:TEST:4", 2, HusserlEpocheState.CompletionEdgeId);
        Check(HusserlEpocheFlow.TryPrepare(embodied, HusserlEpocheAction.TurnEmbodied),
            "The embodied direction must be selectable.");
        var turning = graph.Start(HusserlEpocheState.SourceNodeId);
        turning.RouteRules = true;
        turning.LastFixedAct = 2;
        Check(HusserlEpocheFlow.TryCommitPending(embodied, turning, graph),
            "The embodied direction must take the first-act link.");
        Check(turning.RouteId == "EMBODIED_PERCEPTION"
            && embodied.Stage == HusserlEpocheStage.ResultEmbodied,
            "The embodied direction must name the embodied line.");
        Check(graph.TryApply(turning, "WESTERN_EDGE_195")
            && graph.TryApply(turning, "WESTERN_EDGE_196") && graph.IsComplete(turning),
            "The embodied line must reach its own closing figure.");

        var tampered = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:3");
        tampered.Standard = HusserlEpocheStandard.Suspension;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
