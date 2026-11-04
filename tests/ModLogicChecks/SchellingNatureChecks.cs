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

        // The default nature-and-becoming line takes Schelling as its third-act
        // fixed room, after Spinoza's second-act fixed slot. The graph caps
        // LastFixedAct at three, so adoption must not expect a fourth slot.
        var third = SchellingNatureState.CreateAt("SCHELLING_NATURE:TEST:3", 3,
            SchellingNatureState.CompletionEdgeId);
        third.RoutePlaced = true;
        third.RouteSourceNodeId = SchellingNatureState.SourceNodeId;
        var thirdJourney = new WesternJourneyState
        {
            CurrentNodeId = SchellingNatureState.SourceNodeId,
            LastFixedAct = 3,
        };
        Check(SchellingNatureFlow.IsRouteConsistent(third, thirdJourney),
            "The default third-act fixed slot opens on Spinoza's node.");
        Check(SchellingNatureFlow.TryPrepare(third, SchellingNatureAction.NatureAsSpirit)
            && SchellingNatureFlow.TryCommitPending(third, thirdJourney, graph),
            "The third-act fixed choice commits at the capped slot.");
        Check(third.EdgeCommitted
            && thirdJourney.CompletedEdgeIds.Contains(SchellingNatureState.CompletionEdgeId)
            && thirdJourney.CurrentNodeId == SchellingNatureState.AdoptedNodeId
            && thirdJourney.LastFixedAct == 3,
            "The third-act adoption moves to Schelling and keeps LastFixedAct at three.");

        var thirdKept = SchellingNatureState.CreateAt("SCHELLING_NATURE:TEST:4", 3,
            SchellingNatureState.CompletionEdgeId);
        thirdKept.RoutePlaced = true;
        thirdKept.RouteSourceNodeId = SchellingNatureState.SourceNodeId;
        Check(SchellingNatureFlow.TryPrepare(thirdKept, SchellingNatureAction.KeepSpinoza),
            "Keeping Spinoza is available in the third-act fixed slot.");
        var thirdDeclined = new WesternJourneyState
        {
            CurrentNodeId = SchellingNatureState.SourceNodeId,
            LastFixedAct = 3,
        };
        Check(SchellingNatureFlow.TryCommitPending(thirdKept, thirdDeclined, graph)
            && thirdDeclined.DeclinedFixedActs.Contains(3)
            && thirdDeclined.CurrentNodeId == SchellingNatureState.SourceNodeId
            && thirdDeclined.LastFixedAct == 3,
            "Declining the third-act fixed slot leaves Spinoza and records the refusal.");

        Check(SchellingNatureTriggerPolicy.ShouldInsert(new(true, true, true, 2, true, 1,
            SchellingNatureState.SourceNodeId, 3, true, false, false, false, false, true, 3)),
            "The default line offers Schelling at the third-act boundary.");
        Check(!SchellingNatureTriggerPolicy.ShouldInsert(new(true, true, true, 1, true, 1,
            SchellingNatureState.SourceNodeId, 3, true, false, false, false, false, true, 3)),
            "The third-act record is not offered in the second act.");
        Check(!SchellingNatureTriggerPolicy.ShouldInsert(new(true, true, true, 2, true, 1,
            SchellingNatureState.SourceNodeId, 2, true, false, false, false, false, true, 3)),
            "The third-act record needs the act-three fixed slot.");
        Check(!SchellingNatureTriggerPolicy.ShouldInsert(new(true, true, true, 2, true, 1,
            SchellingNatureState.SourceNodeId, 3, false, false, false, false, false, true, 3)),
            "The third-act record needs 057 to be eligible.");
        Check(SchellingNatureTriggerPolicy.ShouldInsert(new(true, true, true, 1, true, 1,
            SchellingNatureState.SourceNodeId, 2, true, false, false, false, false, true, 0)),
            "The old second-act question path remains legal.");

        Check(SchellingNatureState.Create("SCHELLING_NATURE:TEST:5").ActIndex == 2,
            "Old records without an act still default to the second act.");
        var badAct = SchellingNatureState.CreateAt("SCHELLING_NATURE:TEST:6", 1,
            SchellingNatureState.CompletionEdgeId);
        Check(!badAct.IsUsable(), "An act outside two or three is rejected.");

        var tampered = SchellingNatureState.Create("SCHELLING_NATURE:TEST:3");
        tampered.Standard = SchellingNatureStandard.Mechanism;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
