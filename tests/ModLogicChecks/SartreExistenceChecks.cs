using STS2Philosophers;

internal static class SartreExistenceChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = SartreExistenceState.Create("SARTRE_EXISTENCE:TEST:1");
        Check(SartreExistenceFlow.Options(state.Stage).Count == 3,
            "Sartre must offer existence, freedom in situation, or keeping Kierkegaard.");
        Check(SartreExistenceFlow.TryPrepare(state, SartreExistenceAction.ExistencePrecedesEssence),
            "Existence must be selectable.");
        var journey = new WesternJourneyState
        {
            CurrentNodeId = SartreExistenceState.SourceNodeId,
            LastFixedAct = 2,
        };
        Check(SartreExistenceFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(SartreExistenceFlow.TryCommitPending(state, journey, graph),
            "The leap must take the first-act fixed link.");
        Check(state.Standard == SartreExistenceStandard.Existence && state.EdgeCommitted
            && state.Stage == SartreExistenceStage.ResultExistence,
            "The choice must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(SartreExistenceState.CompletionEdgeId)
            && journey.CurrentNodeId == SartreExistenceState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Sartre and consume act two.");

        var kept = SartreExistenceState.Create("SARTRE_EXISTENCE:TEST:2");
        SartreExistenceFlow.TryPrepare(kept, SartreExistenceAction.KeepKierkegaard);
        var declined = new WesternJourneyState
        {
            CurrentNodeId = SartreExistenceState.SourceNodeId,
            LastFixedAct = 2,
        };
        Check(SartreExistenceFlow.TryCommitPending(kept, declined, graph),
            "Keeping Kierkegaard must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == SartreExistenceState.SourceNodeId,
            "Declining must leave the player on Kierkegaard's node.");

        var tampered = SartreExistenceState.Create("SARTRE_EXISTENCE:TEST:3");
        tampered.Standard = SartreExistenceStandard.Freedom;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
