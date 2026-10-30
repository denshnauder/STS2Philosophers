using STS2Philosophers;

internal static class KierkegaardFaithChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = KierkegaardFaithState.Create("KIERKEGAARD_FAITH:TEST:1");
        Check(KierkegaardFaithFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer the leap, the universal, or keeping Socrates.");
        Check(KierkegaardFaithFlow.TryPrepare(state, KierkegaardFaithAction.FaithLeap),
            "The leap must be selectable.");
        var journey = graph.Start(KierkegaardFaithState.SourceNodeId);
        journey.LastFixedAct = 2;
        Check(KierkegaardFaithFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(KierkegaardFaithFlow.TryCommitPending(state, journey, graph),
            "The leap must take the first-act fixed link.");
        Check(state.Standard == KierkegaardFaithStandard.Faith && state.EdgeCommitted
            && state.Stage == KierkegaardFaithStage.ResultFaith,
            "The leap must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(KierkegaardFaithState.CompletionEdgeId)
            && journey.CurrentNodeId == KierkegaardFaithState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = KierkegaardFaithState.Create("KIERKEGAARD_FAITH:TEST:2");
        KierkegaardFaithFlow.TryPrepare(kept, KierkegaardFaithAction.KeepSocrates);
        var declined = graph.Start(KierkegaardFaithState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(KierkegaardFaithFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == KierkegaardFaithState.SourceNodeId,
            "Declining must leave the player on Socrates' node.");

        var tampered = KierkegaardFaithState.Create("KIERKEGAARD_FAITH:TEST:3");
        tampered.Standard = KierkegaardFaithStandard.Ethical;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
