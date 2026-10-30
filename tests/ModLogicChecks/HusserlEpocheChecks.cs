using STS2Philosophers;

internal static class HusserlEpocheChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    // A room record a save can no longer replay must not count as an open
    // question: it would suppress every other question room for the rest of the
    // run while never being able to open itself.
    private static void AnUnusableRecordIsNotAnOpenQuestion()
    {
        var shared = new PhilosophyRunState();
        HusserlEpocheState state = HusserlEpocheState.Create("HUSSERL_EPOCHE:test:1");
        state.Version = 0;
        shared.HusserlEpoche = state;
        Check(!state.IsUsable(), "A record from another version must be unusable.");
        Check(!WesternQuestionNodePolicy.HasOpenQuestion(shared),
            "An unusable record must not count as an open question.");
        state.Version = HusserlEpocheState.CurrentVersion;
        Check(WesternQuestionNodePolicy.HasOpenQuestion(shared),
            "A usable record that is still open must count as an open question.");
    }

    internal static void Run()
    {
        AnUnusableRecordIsNotAnOpenQuestion();
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:1");
        Check(HusserlEpocheFlow.Options(state.Stage).Count == 4,
            "Husserl must offer description, suspension, the embodied direction, or keeping Descartes.");
        Check(HusserlEpocheFlow.TryPrepare(state, HusserlEpocheAction.DescribeAppearance),
            "The description must be selectable.");
        var journey = graph.Start(HusserlEpocheState.SourceNodeId);
        journey.RouteRules = true;
        Check(HusserlEpocheFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(HusserlEpocheFlow.TryCommitPending(state, journey, graph),
            "The description must take the first-act fixed link.");
        Check(state.Standard == HusserlEpocheStandard.Appearance && state.EdgeCommitted
            && state.Stage == HusserlEpocheStage.ResultAppearance,
            "The description must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HusserlEpocheState.CompletionEdgeId)
            && journey.CurrentNodeId == HusserlEpocheState.AdoptedNodeId && journey.LastFixedAct == 2,
            "Adopting must move the journey to Husserl and consume act one.");

        var kept = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:2");
        HusserlEpocheFlow.TryPrepare(kept, HusserlEpocheAction.KeepDescartes);
        var declined = graph.Start(HusserlEpocheState.SourceNodeId);
        Check(HusserlEpocheFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(1)
            && declined.CurrentNodeId == HusserlEpocheState.SourceNodeId,
            "Declining must leave the player on Descartes' node and spend act one.");

        // Husserl stands on two lines. Describing the appearance names the
        // appearance line; the direction option names the embodied one, and the
        // run then walks that line's own handovers instead of dead-ending.
        Check(journey.RouteId == "APPEARANCE_AND_INTERPRETATION",
            "Describing the appearance must name the appearance line.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_164")
            && graph.TryApply(journey, "WESTERN_EDGE_161") && graph.IsComplete(journey),
            "The appearance line must reach its own closing figure.");

        var embodied = HusserlEpocheState.Create("HUSSERL_EPOCHE:TEST:4");
        Check(HusserlEpocheFlow.TryPrepare(embodied, HusserlEpocheAction.TurnEmbodied),
            "The embodied direction must be selectable.");
        var turning = graph.Start(HusserlEpocheState.SourceNodeId);
        turning.RouteRules = true;
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
