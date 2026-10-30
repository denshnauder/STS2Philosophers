using STS2Philosophers;

internal static class SchellingNatureChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = SchellingNatureState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = SchellingNatureState.Create("SCHELLING_NATURE:TEST:1");
        Check(SchellingNatureFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Spinoza.");
        Check(SchellingNatureFlow.TryPrepare(state, SchellingNatureAction.NatureAsSpirit),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(SchellingNatureFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(SchellingNatureFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == SchellingNatureStandard.Spirit && state.EdgeCommitted
            && state.Stage == SchellingNatureStage.ResultSpirit,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(SchellingNatureState.CompletionEdgeId)
            && journey.CurrentNodeId == SchellingNatureState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = SchellingNatureState.Create("SCHELLING_NATURE:TEST:2");
        SchellingNatureFlow.TryPrepare(kept, SchellingNatureAction.KeepSpinoza);
        var declined = Journey();
        Check(SchellingNatureFlow.TryCommitPending(kept, declined, graph),
            "Keeping Spinoza must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == SchellingNatureState.SourceNodeId,
            "Declining must leave the player on Spinoza's node.");

        var tampered = SchellingNatureState.Create("SCHELLING_NATURE:TEST:3");
        tampered.Standard = SchellingNatureStandard.Mechanism;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
