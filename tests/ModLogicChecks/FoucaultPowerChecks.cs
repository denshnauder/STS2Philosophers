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
        Check((int)FoucaultPowerAction.Leave == 4 && (int)FoucaultPowerAction.BenjaminHistory == 5,
            "The old numeric Leave value must remain 4 while Benjamin's new action appends after it.");
        Check(!FoucaultPowerFlow.VisibleOptions(benjamin).Contains(FoucaultPowerAction.BenjaminHistory)
            && !FoucaultPowerFlow.TryPrepare(benjamin, FoucaultPowerAction.BenjaminHistory),
            "A new run must not open Benjamin from Foucault's result page.");

        var legacy = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:5");
        legacy.Version = 1;
        legacy.NormalizeAfterLoad();
        FoucaultPowerFlow.TryPrepare(legacy, FoucaultPowerAction.PowerKnowledge);
        WesternJourneyState legacyJourney = Journey();
        FoucaultPowerFlow.TryCommitPending(legacy, legacyJourney, graph);
        Check(legacy.LegacyNestedBenjamin
            && FoucaultPowerFlow.VisibleOptions(legacy).Contains(FoucaultPowerAction.BenjaminHistory)
            && FoucaultPowerFlow.TryPrepare(legacy, FoucaultPowerAction.BenjaminHistory)
            && FoucaultPowerFlow.TryCommitPending(legacy, legacyJourney, graph)
            && legacy.Stage == FoucaultPowerStage.Benjamin,
            "An old save must keep the nested Benjamin page without touching the route.");
        Check(FoucaultPowerFlow.TryPrepare(legacy, FoucaultPowerAction.Leave)
            && FoucaultPowerFlow.TryCommitPending(legacy, legacyJourney, graph) && legacy.Closed,
            "The legacy perspective page must close the room.");

        var tampered = FoucaultPowerState.Create("FOUCAULT_POWER:TEST:3");
        tampered.Standard = FoucaultPowerStandard.Discipline;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
