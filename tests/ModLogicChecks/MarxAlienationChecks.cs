using STS2Philosophers;

internal static class MarxAlienationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = MarxAlienationState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { MarxAlienationState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = MarxAlienationState.Create("MARX_ALIENATION:TEST:1");
        Check(MarxAlienationFlow.Options(state.Stage).Count == 3,
            "Marx must offer alienated labor, historical progress, or keeping Hegel.");
        Check(MarxAlienationFlow.TryPrepare(state, MarxAlienationAction.AlienatedLabor),
            "Alienated labor must be selectable.");
        var journey = Journey();
        Check(MarxAlienationFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Hegel.");
        Check(MarxAlienationFlow.TryCommitPending(state, journey, graph),
            "Alienated labor must take the third-act question.");
        Check(state.Standard == MarxAlienationStandard.Alienation && state.EdgeCommitted
            && state.Stage == MarxAlienationStage.ResultAlienation,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(MarxAlienationState.CompletionEdgeId)
            && journey.CurrentNodeId == MarxAlienationState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = MarxAlienationState.Create("MARX_ALIENATION:TEST:2");
        MarxAlienationFlow.TryPrepare(kept, MarxAlienationAction.KeepHegel);
        var declined = Journey();
        Check(MarxAlienationFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hegel must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == MarxAlienationState.SourceNodeId,
            "Declining must leave the player on Hegel's node.");

        WesternJourneyState FixedJourney() => new()
        {
            CurrentNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE",
            LastFixedAct = 2,
            RouteId = "SOCIAL_CRITIQUE",
        };

        var fixedAdopt = MarxAlienationState.CreateAt(
            "MARX_ALIENATION:TEST:2", 2, "WESTERN_EDGE_118");
        fixedAdopt.RoutePlaced = true;
        fixedAdopt.FixedMode = true;
        fixedAdopt.RouteSourceNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE";
        var fixedJourney = FixedJourney();
        Check(MarxAlienationFlow.IsRouteConsistent(fixedAdopt, fixedJourney),
            "Marx opens in the second-act fixed slot after Rousseau.");
        Check(MarxAlienationFlow.DescriptionKey(fixedAdopt) == "INVITATION_ROUSSEAU"
            && MarxAlienationFlow.ActionKey(fixedAdopt, MarxAlienationAction.KeepHegel) == "KEEP_ROUSSEAU",
            "The default Rousseau source must use Rousseau's invitation and refusal, not Hegel's.");
        Check(MarxAlienationFlow.TryPrepare(fixedAdopt, MarxAlienationAction.AlienatedLabor),
            "Fixed Marx can prepare alienated labor.");
        var fixedReload = System.Text.Json.JsonSerializer.Deserialize<MarxAlienationState>(
            System.Text.Json.JsonSerializer.Serialize(fixedAdopt))!;
        Check(MarxAlienationFlow.TryCommitPending(fixedReload, fixedJourney, graph),
            "Fixed Marx commits alienated labor after a reload.");
        Check(fixedJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_118")
            && fixedJourney.LastFixedAct == 3
            && fixedJourney.CurrentNodeId == MarxAlienationState.AdoptedNodeId
            && !fixedJourney.ResolvedQuestionActs.Contains(2),
            "Fixed adoption advances the fixed slot and does not spend act two's question.");

        var fixedDecline = MarxAlienationState.CreateAt(
            "MARX_ALIENATION:TEST:2", 2, "WESTERN_EDGE_118");
        fixedDecline.RoutePlaced = true;
        fixedDecline.FixedMode = true;
        fixedDecline.RouteSourceNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE";
        var declinedFixedJourney = FixedJourney();
        Check(MarxAlienationFlow.TryPrepare(fixedDecline, MarxAlienationAction.KeepHegel)
            && MarxAlienationFlow.TryCommitPending(fixedDecline, declinedFixedJourney, graph),
            "Fixed Marx declines the fixed slot rather than the question.");
        Check(declinedFixedJourney.DeclinedFixedActs.Contains(2)
            && !declinedFixedJourney.ResolvedQuestionActs.Contains(2)
            && declinedFixedJourney.LastFixedAct == 2,
            "Fixed decline records the slot and leaves the question open.");
        Check(MarxAlienationFlow.DescriptionKey(fixedDecline) == "RESULT_KEPT_ROUSSEAU",
            "The default Rousseau refusal must not claim Hegel was kept.");

        var tampered = MarxAlienationState.Create("MARX_ALIENATION:TEST:3");
        tampered.Standard = MarxAlienationStandard.Progress;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
