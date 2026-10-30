using STS2Philosophers;

internal static class FreudUnconsciousChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = FreudUnconsciousState.Create("FREUD_UNCONSCIOUS:TEST:1");
        Check(FreudUnconsciousFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer description, the universal, or keeping Descartes.");
        Check(FreudUnconsciousFlow.TryPrepare(state, FreudUnconsciousAction.Unconscious),
            "The description must be selectable.");
        // The route places this room in the second act now, so the journey that
        // meets it stands in that act's slot and on the unconscious line.
        var journey = graph.Start(FreudUnconsciousState.SourceNodeId);
        journey.LastFixedAct = 2;
        journey.RouteId = "UNCONSCIOUS_AND_NORM";
        journey.LastFixedAct = 2;
        state = FreudUnconsciousState.CreateAt("FREUD_UNCONSCIOUS:TEST:1", 2,
            FreudUnconsciousState.CompletionEdgeId);
        FreudUnconsciousFlow.TryPrepare(state, FreudUnconsciousAction.Unconscious);
        Check(FreudUnconsciousFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(FreudUnconsciousFlow.TryCommitPending(state, journey, graph),
            "The description must take the first-act fixed link.");
        Check(state.Standard == FreudUnconsciousStandard.Unconscious && state.EdgeCommitted
            && state.Stage == FreudUnconsciousStage.ResultUnconscious,
            "The description must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(FreudUnconsciousState.CompletionEdgeId)
            && journey.CurrentNodeId == FreudUnconsciousState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Freud and fill the second act's slot.");

        var kept = FreudUnconsciousState.Create("FREUD_UNCONSCIOUS:TEST:2");
        FreudUnconsciousFlow.TryPrepare(kept, FreudUnconsciousAction.KeepDescartes);
        var declined = graph.Start(FreudUnconsciousState.SourceNodeId);
        declined.LastFixedAct = 2;
        declined.RouteId = "UNCONSCIOUS_AND_NORM";
        declined.LastFixedAct = 2;
        kept = FreudUnconsciousState.CreateAt("FREUD_UNCONSCIOUS:TEST:2", 2,
            FreudUnconsciousState.CompletionEdgeId);
        FreudUnconsciousFlow.TryPrepare(kept, FreudUnconsciousAction.KeepDescartes);
        Check(FreudUnconsciousFlow.TryCommitPending(kept, declined, graph),
            "Keeping Descartes must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == FreudUnconsciousState.SourceNodeId,
            "Declining must leave the player on Descartes' node.");

        var tampered = FreudUnconsciousState.Create("FREUD_UNCONSCIOUS:TEST:3");
        tampered.Standard = FreudUnconsciousStandard.Dream;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
