using STS2Philosophers;

internal static class FoucaultPowerChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = FoucaultPowerState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:1");
        Check(FoucaultPowerFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(FoucaultPowerFlow.TryPrepare(state, FoucaultPowerAction.PowerKnowledge),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(FoucaultPowerFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(FoucaultPowerFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == FoucaultPowerStandard.Power && state.EdgeCommitted
            && state.Stage == FoucaultPowerStage.ResultPower,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(FoucaultPowerState.CompletionEdgeId)
            && journey.CurrentNodeId == FoucaultPowerState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:2");
        FoucaultPowerFlow.TryPrepare(kept, FoucaultPowerAction.KeepNietzsche);
        var declined = Journey();
        Check(FoucaultPowerFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == FoucaultPowerState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var benjamin = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:4");
        FoucaultPowerFlow.TryPrepare(benjamin, FoucaultPowerAction.PowerKnowledge);
        WesternJourneyState benjaminJourney = Journey();
        FoucaultPowerFlow.TryCommitPending(benjamin, benjaminJourney, graph);
        Check(FoucaultPowerFlow.Options(benjamin.Stage).Contains(FoucaultPowerAction.BenjaminHistory),
            "The result page must offer Benjamin's historical perspective.");
        Check(FoucaultPowerFlow.TryPrepare(benjamin, FoucaultPowerAction.BenjaminHistory)
            && FoucaultPowerFlow.TryCommitPending(benjamin, benjaminJourney, graph)
            && benjamin.Stage == FoucaultPowerStage.Benjamin,
            "Benjamin's perspective must open without touching the route.");
        Check(FoucaultPowerFlow.TryPrepare(benjamin, FoucaultPowerAction.Leave)
            && FoucaultPowerFlow.TryCommitPending(benjamin, benjaminJourney, graph) && benjamin.Closed,
            "The perspective page must close the room.");

        var tampered = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:3");
        tampered.Standard = FoucaultPowerStandard.Discipline;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
