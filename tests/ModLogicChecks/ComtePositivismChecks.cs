using System.Text.Json;
using STS2Philosophers;

internal static class ComtePositivismChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = ComtePositivismState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { ComtePositivismState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:1");
        Check(ComtePositivismFlow.Options(state.Stage).Count == 3,
            "Marx must offer the three stages, keeping the anomaly, or keeping Bacon.");
        Check(ComtePositivismFlow.TryPrepare(state, ComtePositivismAction.ThreeStages),
            "Keeping the stages must be selectable.");
        var journey = Journey();
        Check(ComtePositivismFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Bacon.");
        Check(ComtePositivismFlow.TryCommitPending(state, journey, graph),
            "Keeping the stages must take the third-act question.");
        Check(state.Standard == ComtePositivismStandard.Stages && state.EdgeCommitted
            && state.Stage == ComtePositivismStage.ResultStages,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(ComtePositivismState.CompletionEdgeId)
            && journey.CurrentNodeId == ComtePositivismState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:2");
        ComtePositivismFlow.TryPrepare(kept, ComtePositivismAction.KeepBacon);
        var declined = Journey();
        Check(ComtePositivismFlow.TryCommitPending(kept, declined, graph),
            "Keeping Bacon must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == ComtePositivismState.SourceNodeId,
            "Declining must leave the player on Bacon's node.");


        // Default social-knowledge line: Bacon second act, Comte third act.
        var fixedState = ComtePositivismState.CreateAt("COMTE_POSITIVISM:TEST:3", 3, "WESTERN_EDGE_141");
        fixedState.RoutePlaced = true;
        var fixedJourney = new WesternJourneyState
        {
            CurrentNodeId = ComtePositivismState.SourceNodeId,
            RouteId = "SOCIAL_KNOWLEDGE",
            LastFixedAct = 3,
            CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_140" },
        };
        Check(ComtePositivismFlow.IsRouteConsistent(fixedState, fixedJourney),
            "The default Comte room must sit on the third-act fixed slot.");
        ComtePositivismFlow.TryPrepare(fixedState, ComtePositivismAction.ThreeStages);
        Check(ComtePositivismFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "The default Comte choice must take the third-act fixed link.");
        Check(fixedJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_141")
            && fixedJourney.CurrentNodeId == ComtePositivismState.AdoptedNodeId
            && fixedJourney.LastFixedAct == 3,
            "Adopting on the default line must move to Comte.");

        // Early first-act Bacon turn: Comte second act, Kuhn third act.
        var earlyState = ComtePositivismState.CreateAt("COMTE_POSITIVISM:TEST:4", 2, "WESTERN_EDGE_198");
        earlyState.RoutePlaced = true;
        var earlyJourney = new WesternJourneyState
        {
            CurrentNodeId = ComtePositivismState.SourceNodeId,
            RouteId = "SOCIAL_KNOWLEDGE",
            LastFixedAct = 2,
            CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_197" },
        };
        Check(ComtePositivismFlow.IsRouteConsistent(earlyState, earlyJourney),
            "The early Comte room must sit on the second-act fixed slot.");
        ComtePositivismFlow.TryPrepare(earlyState, ComtePositivismAction.ThreeStages);
        Check(ComtePositivismFlow.TryCommitPending(earlyState, earlyJourney, graph),
            "The early Comte choice must take the second-act fixed link.");
        Check(earlyJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_198")
            && earlyJourney.CurrentNodeId == ComtePositivismState.AdoptedNodeId
            && earlyJourney.LastFixedAct == 3,
            "Adopting on the early line must move to Comte and leave Kuhn's act.");

        Check(ComtePositivismTriggerPolicy.ShouldInsert(new(
                true, true, true, 2, true, 1, ComtePositivismState.SourceNodeId, 3, true, true,
                false, false, false, false, true, 3)),
            "The default Comte fixed entry must trigger at the third-act boundary.");
        Check(ComtePositivismTriggerPolicy.ShouldInsert(new(
                true, true, true, 1, true, 1, ComtePositivismState.SourceNodeId, 2, true, true,
                false, false, false, false, true, 2)),
            "The early Comte fixed entry must trigger at the second-act boundary.");
        Check(!ComtePositivismTriggerPolicy.ShouldInsert(new(
                true, true, true, 0, true, 1, ComtePositivismState.SourceNodeId, 3, true, true,
                false, false, false, false, true, 3)),
            "The Comte fixed entry must not trigger in the first act.");

        foreach (var (action, stage) in new[]
        {
            (ComtePositivismAction.ThreeStages, ComtePositivismStage.ResultStages),
            (ComtePositivismAction.KeepAnomaly, ComtePositivismStage.ResultAnomaly),
            (ComtePositivismAction.KeepBacon, ComtePositivismStage.ResultKept),
        })
        {
            var result = ComtePositivismState.Create("COMTE_POSITIVISM:PAGE:" + stage);
            Check(ComtePositivismFlow.TryPrepare(result, action), "Every Comte result must be selectable.");
            ComtePositivismFlow.Apply(result, action);
            string key = $"COMTE_POSITIVISM.pages.{ComtePositivismFlow.DescriptionKey(result)}.description";
            foreach (string language in new[] { "eng", "zhs" })
            {
                using JsonDocument text = JsonDocument.Parse(File.ReadAllText(
                    $"content/STS2Philosophers/localization/{language}/events.json"));
                Check(text.RootElement.TryGetProperty(key, out _),
                    $"Comte's {stage} page key {key} must exist in {language}.");
            }
        }

        var tampered = ComtePositivismState.Create("COMTE_POSITIVISM:TEST:5");
        tampered.Standard = ComtePositivismStandard.Anomaly;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
