using STS2Philosophers;

internal static class SmithSympathyChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:1");
        Check(SmithSympathyFlow.Options(state.Stage).Count == 3,
            "Smith must offer sympathy, exchange, or keeping Plato's arrangement.");
        Check(SmithSympathyFlow.TryPrepare(state, SmithSympathyAction.SympathyOrder),
            "Sympathy must be selectable.");
        var journey = graph.Start(SmithSympathyState.SourceNodeId);
        Check(SmithSympathyFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(SmithSympathyFlow.TryCommitPending(state, journey, graph),
            "Sympathy must take the first-act fixed link.");
        Check(state.Standard == SmithSympathyStandard.Sympathy && state.EdgeCommitted
            && state.Stage == SmithSympathyStage.ResultSympathy,
            "Sympathy must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(SmithSympathyState.CompletionEdgeId)
            && journey.CurrentNodeId == SmithSympathyState.AdoptedNodeId && journey.LastFixedAct == 2,
            "Adopting must move the journey to Smith and consume act one.");
        Check(SmithSympathyFlow.TryPrepare(state, SmithSympathyAction.Leave), "A finished room must close.");
        SmithSympathyFlow.Apply(state, SmithSympathyAction.Leave);
        Check(state.Closed && state.IsUsable(), "A closed room must stay replayable.");

        var kept = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:2");
        SmithSympathyFlow.TryPrepare(kept, SmithSympathyAction.PreserveArrangement);
        var declined = graph.Start(SmithSympathyState.SourceNodeId);
        Check(SmithSympathyFlow.TryCommitPending(kept, declined, graph),
            "Keeping Plato's arrangement must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == SmithSympathyState.SourceNodeId,
            "Declining must leave the player on Plato's institutions node.");

        var tampered = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:3");
        tampered.Standard = SmithSympathyStandard.Exchange;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
        tampered.Version = SmithSympathyState.CurrentVersion + 1;
        Check(!tampered.IsUsable(), "An unknown version must be rejected.");
    }
}
