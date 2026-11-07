using System.Text.Json;
using STS2Philosophers;

internal static class SmithSympathyChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:1");
        Check(SmithSympathyFlow.Options(state.Stage).Count == 3,
            "Smith must offer sympathy, exchange, or keeping Plato's arrangement.");
        Check(SmithSympathyFlow.TryPrepare(state, SmithSympathyAction.SympathyOrder),
            "Sympathy must be selectable.");
        var journey = graph.Start(SmithSympathyState.SourceNodeId);
        journey.LastFixedAct = 2;
        Check(SmithSympathyFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(SmithSympathyFlow.TryCommitPending(state, journey, graph),
            "Sympathy must take the first-act fixed link.");
        Check(state.Standard == SmithSympathyStandard.Sympathy && state.EdgeCommitted
            && state.Stage == SmithSympathyStage.ResultSympathy,
            "Sympathy must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(SmithSympathyState.CompletionEdgeId)
            && journey.CurrentNodeId == SmithSympathyState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Smith and consume act one.");
        Check(SmithSympathyFlow.TryPrepare(state, SmithSympathyAction.Leave), "A finished room must close.");
        SmithSympathyFlow.Apply(state, SmithSympathyAction.Leave);
        Check(state.Closed && state.IsUsable(), "A closed room must stay replayable.");

        var kept = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:2");
        SmithSympathyFlow.TryPrepare(kept, SmithSympathyAction.PreserveArrangement);
        var declined = graph.Start(SmithSympathyState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(SmithSympathyFlow.TryCommitPending(kept, declined, graph),
            "Keeping Plato's arrangement must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == SmithSympathyState.SourceNodeId,
            "Declining must leave the player on Plato's institutions node.");

        var tampered = SmithSympathyState.Create("SMITH_SYMPATHY:TEST:3");
        tampered.Standard = SmithSympathyStandard.Exchange;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
        tampered.Version = SmithSympathyState.CurrentVersion + 1;
        Check(!tampered.IsUsable(), "An unknown version must be rejected.");
        // The act-one question already grants Smith on that branch; the act-two
        // fixed slot must not hand him out a second time.
        WesternRouteGraph earlyGraph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState early = earlyGraph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        early.RouteId = "SYMPATHY_AND_PUBLIC_ACTION"; early.RouteRules = true;
        Check(earlyGraph.TryApply(early, "WESTERN_EDGE_174") && early.SeenThinkerIds.Contains("ADAM_SMITH"),
            "The act-one question hands Smith out on the sympathy branch.");
        Check(!earlyGraph.GetEligibleEdges(early, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_116"),
            "Smith is not granted twice after the early question.");

        foreach (var (action, stage) in new[]
        {
            (SmithSympathyAction.SympathyOrder, SmithSympathyStage.ResultSympathy),
            (SmithSympathyAction.ExchangeOrder, SmithSympathyStage.ResultExchange),
            (SmithSympathyAction.PreserveArrangement, SmithSympathyStage.ResultArrangement),
        })
        {
            var result = SmithSympathyState.Create("SMITH_SYMPATHY:PAGE:" + stage);
            Check(SmithSympathyFlow.TryPrepare(result, action), "Every Smith result must be selectable.");
            SmithSympathyFlow.Apply(result, action);
            string key = $"SMITH_SYMPATHY.pages.{SmithSympathyFlow.DescriptionKey(result)}.description";
            foreach (string language in new[] { "eng", "zhs" })
            {
                using JsonDocument text = JsonDocument.Parse(File.ReadAllText(
                    $"content/STS2Philosophers/localization/{language}/events.json"));
                Check(text.RootElement.TryGetProperty(key, out _),
                    $"Smith's {stage} page key {key} must exist in {language}.");
            }
        }
    }
}
