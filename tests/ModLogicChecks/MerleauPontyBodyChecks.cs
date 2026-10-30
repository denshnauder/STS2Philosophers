using STS2Philosophers;

internal static class MerleauPontyBodyChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = MerleauPontyBodyState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = MerleauPontyBodyState.Create("MERLEAU_PONTY_BODY:TEST:1");
        Check(MerleauPontyBodyFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(MerleauPontyBodyFlow.TryPrepare(state, MerleauPontyBodyAction.BodyAsSubject),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(MerleauPontyBodyFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(MerleauPontyBodyFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == MerleauPontyBodyStandard.Body && state.EdgeCommitted
            && state.Stage == MerleauPontyBodyStage.ResultBody,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(MerleauPontyBodyState.CompletionEdgeId)
            && journey.CurrentNodeId == MerleauPontyBodyState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = MerleauPontyBodyState.Create("MERLEAU_PONTY_BODY:TEST:2");
        MerleauPontyBodyFlow.TryPrepare(kept, MerleauPontyBodyAction.KeepHusserl);
        var declined = Journey();
        Check(MerleauPontyBodyFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == MerleauPontyBodyState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = MerleauPontyBodyState.Create("MERLEAU_PONTY_BODY:TEST:3");
        tampered.Standard = MerleauPontyBodyStandard.Objective;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
