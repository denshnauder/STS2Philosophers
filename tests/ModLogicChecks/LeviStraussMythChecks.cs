using STS2Philosophers;

internal static class LeviStraussMythChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = LeviStraussMythState.SourceNodeId,
        LastFixedAct = 2,
        CompletedEdgeIds = ["WESTERN_EDGE_080"],
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:1");
        Check(LeviStraussMythFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(LeviStraussMythFlow.TryPrepare(state, LeviStraussMythAction.StructuralReading),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(LeviStraussMythFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(LeviStraussMythFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == LeviStraussMythStandard.Structure && state.EdgeCommitted
            && state.Stage == LeviStraussMythStage.ResultStructure,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(LeviStraussMythState.CompletionEdgeId)
            && journey.CurrentNodeId == LeviStraussMythState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:2");
        LeviStraussMythFlow.TryPrepare(kept, LeviStraussMythAction.KeepSaussure);
        var declined = Journey();
        Check(LeviStraussMythFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == LeviStraussMythState.SourceNodeId,
            "Declining must leave the player on Hume's node.");


        var tampered = LeviStraussMythState.Create("LEVI_STRAUSS_MYTH:TEST:3");
        tampered.Standard = LeviStraussMythStandard.History;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");

        RunGraphScenarios(graph);
    }

    private static void RunGraphScenarios(WesternRouteGraph graph)
    {
        // Third act, route-placed with the real selector edge 107.
        foreach (LeviStraussMythAction action in new[]
                 { LeviStraussMythAction.StructuralReading, LeviStraussMythAction.HistoricalReading })
        {
            var state = LeviStraussMythState.CreateAt("LEVI_STRAUSS_MYTH:TEST:107", 3, "WESTERN_EDGE_107");
            state.RoutePlaced = true;
            state.RouteSourceNodeId = LeviStraussMythState.SourceNodeId;
            var journey = new WesternJourneyState
            {
                CurrentNodeId = LeviStraussMythState.SourceNodeId,
                RouteId = "STRUCTURE_AND_EXCLUSION",
                LastFixedAct = 3,
            };
            Check(LeviStraussMythFlow.TryPrepare(state, action), "107 adoption must be selectable.");
            var roundTripped = RoundTrip(state);
            Check(LeviStraussMythFlow.TryCommitPending(roundTripped, journey, graph),
                "107 adoption must commit through the real graph.");
            Check(journey.CompletedEdgeIds.Contains("WESTERN_EDGE_107")
                && journey.CurrentNodeId == LeviStraussMythState.AdoptedNodeId && journey.LastFixedAct == 3,
                "107 adoption must advance along the real fixed edge.");
            Check(!LeviStraussMythFlow.TryCommitPending(roundTripped, journey, graph),
                "A committed 107 adoption must reject a second commit.");
        }

        var declined107 = LeviStraussMythState.CreateAt("LEVI_STRAUSS_MYTH:TEST:107D", 3, "WESTERN_EDGE_107");
        declined107.RoutePlaced = true;
        declined107.RouteSourceNodeId = LeviStraussMythState.SourceNodeId;
        var declinedJourney = new WesternJourneyState
        {
            CurrentNodeId = LeviStraussMythState.SourceNodeId,
                RouteId = "STRUCTURE_AND_EXCLUSION",
            LastFixedAct = 3,
        };
        Check(LeviStraussMythFlow.TryPrepare(declined107, LeviStraussMythAction.KeepSaussure),
            "107 decline must be selectable.");
        Check(LeviStraussMythFlow.TryCommitPending(RoundTrip(declined107), declinedJourney, graph),
            "107 decline must commit through the real graph.");
        Check(declinedJourney.DeclinedFixedActs.Contains(3)
            && declinedJourney.CurrentNodeId == LeviStraussMythState.SourceNodeId,
            "107 decline must consume act three and stay on Saussure.");

        // Early second act, route-placed with selector edge 187.
        var early = LeviStraussMythState.CreateAt("LEVI_STRAUSS_MYTH:TEST:187", 2, "WESTERN_EDGE_187");
        early.RoutePlaced = true;
        early.RouteSourceNodeId = LeviStraussMythState.SourceNodeId;
        var earlyJourney = new WesternJourneyState
        {
            CurrentNodeId = LeviStraussMythState.SourceNodeId,
                RouteId = "STRUCTURE_AND_EXCLUSION",
            LastFixedAct = 2,
        };
        Check(LeviStraussMythFlow.TryPrepare(early, LeviStraussMythAction.KeepSaussure),
            "187 decline must be selectable.");
        Check(LeviStraussMythFlow.TryCommitPending(RoundTrip(early), earlyJourney, graph),
            "187 decline must commit through the real graph.");
        Check(earlyJourney.DeclinedFixedActs.Contains(2) && !earlyJourney.DeclinedFixedActs.Contains(3)
            && earlyJourney.LastFixedAct == 2,
            "187 decline must consume act two without spending act three.");
    }

    private static LeviStraussMythState RoundTrip(LeviStraussMythState state) =>
        System.Text.Json.JsonSerializer.Deserialize<LeviStraussMythState>(
            System.Text.Json.JsonSerializer.Serialize(state))!;
}
