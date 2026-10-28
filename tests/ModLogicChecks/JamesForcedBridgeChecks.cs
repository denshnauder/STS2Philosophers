using System.Text.Json;
using STS2Philosophers;

internal static class JamesForcedBridgeChecks
{
    internal static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        var journey = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_008"), "James fixture completes Socrates 008.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_009"), "James fixture adopts Plato 009.");
        Check(graph.TryReserveQuestion(journey, 2), "James fixture resolves Diogenes question 2.");
        var route = new ZenoRouteState(
            ZenoRouteState.CurrentVersion, ZenoRouteStage.DiogenesClosed, 0, 0,
            "RUN_JAMES_001", 2, "WESTERN_EDGE_011", "ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE",
            null, null, null, null, null, null, null, null);
        var shared = new PhilosophyRunState { WesternJourney = journey };
        shared.SetCurrentZenoRouteState(new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, route));

        var state = JamesForcedBridgeState.Create("JAMES_FORCED_BRIDGE:TEST:2", route.RunId);
        Check(JamesForcedBridgeFlow.IsRouteConsistent(state, journey, route),
            "James opens after Plato 009 and a Diogenes Stay close, without consuming 011.");
        Check(JamesForcedBridgeFlow.TryPrepare(state, JamesForcedBridgeAction.CheckConditions)
            && JamesForcedBridgeFlow.TryCommitPending(state, journey, route)
            && state.Stage == JamesForcedBridgeStage.Evidence
            && state.ForcedAssessment && state.MomentousAssessment && state.BothFeasible,
            "James records the forced, momentous and feasible assessment.");
        Check(JamesForcedBridgeFlow.TryPrepare(state, JamesForcedBridgeAction.CrossBridge)
            && JamesForcedBridgeFlow.TryCommitPending(state, journey, route)
            && state.Outcome == JamesForcedBridgeOutcome.Crossed && state.Cost == 0,
            "James crossing keeps the residual-risk outcome and charges no gold.");
        Check(JamesForcedBridgeFlow.TryPrepare(state, JamesForcedBridgeAction.Leave)
            && JamesForcedBridgeFlow.TryCommitPending(state, journey, route) && state.Closed,
            "James result closes after the crossing record.");
        Check(state.IsUsable(), "James crossing state replays after close.");

        var detour = JamesForcedBridgeState.Create("JAMES_FORCED_BRIDGE:TEST:2", route.RunId);
        Check(JamesForcedBridgeFlow.TryPrepare(detour, JamesForcedBridgeAction.AskKeeper)
            && JamesForcedBridgeFlow.TryCommitPending(detour, journey, route)
            && detour.NewEvidenceSeen && detour.BothFeasible
            && JamesForcedBridgeFlow.TryPrepare(detour, JamesForcedBridgeAction.TakeDetour)
            && JamesForcedBridgeFlow.TryCommitPending(detour, journey, route)
            && detour.Outcome == JamesForcedBridgeOutcome.Detoured && detour.Cost == 10,
            "James detour records new evidence and the 10-gold cost.");
        detour.CostAttempted = true;
        detour.CostPaid = true;
        Check(detour.IsUsable(), "James detour state accepts the paid cost receipt.");
        string encoded = JsonSerializer.Serialize(detour);
        JamesForcedBridgeState reloaded = JsonSerializer.Deserialize<JamesForcedBridgeState>(encoded)!;
        reloaded.NormalizeAfterLoad();
        Check(reloaded.IsUsable() && reloaded.Cost == 10
            && JamesForcedBridgeFlow.IsRouteConsistent(reloaded, journey, route),
            "James detour state reloads on the same route.");
        Check(JamesForcedBridgeFlow.TryPrepare(detour, JamesForcedBridgeAction.Leave)
            && JamesForcedBridgeFlow.TryCommitPending(detour, journey, route) && detour.Closed,
            "James detour result closes.");

        var beforeQuestion = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(beforeQuestion, "WESTERN_EDGE_008"), "James negative fixture completes Socrates 008.");
        Check(graph.TryApply(beforeQuestion, "WESTERN_EDGE_009"), "James negative fixture adopts Plato 009.");
        var before = JamesForcedBridgeState.Create("JAMES_FORCED_BRIDGE:TEST:2", route.RunId);
        Check(!JamesForcedBridgeFlow.IsRouteConsistent(before, beforeQuestion, route),
            "James cannot open before Diogenes 010 is closed.");
        var switched = route with { Stage = ZenoRouteStage.WaitingInterval };
        Check(!JamesForcedBridgeFlow.IsRouteConsistent(state, journey, switched),
            "James cannot open after Diogenes Switch.");
        Check(!JamesForcedBridgeFlow.IsRouteConsistent(state, journey, null),
            "James requires a live Zeno route record.");

        string[] keys =
        [
            "JAMES_FORCED_BRIDGE.title",
            "JAMES_FORCED_BRIDGE.pages.ASSESSMENT.description",
            "JAMES_FORCED_BRIDGE.pages.ASSESSMENT.options.CHECK_CONDITIONS.title",
            "JAMES_FORCED_BRIDGE.pages.ASSESSMENT.options.CHECK_CONDITIONS.description",
            "JAMES_FORCED_BRIDGE.pages.ASSESSMENT.options.ASK_KEEPER.title",
            "JAMES_FORCED_BRIDGE.pages.ASSESSMENT.options.ASK_KEEPER.description",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.description",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.CROSS_BRIDGE.title",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.CROSS_BRIDGE.description",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.TAKE_DETOUR.title",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.TAKE_DETOUR.description",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.REFUSE_CHOICE.title",
            "JAMES_FORCED_BRIDGE.pages.EVIDENCE.options.REFUSE_CHOICE.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_CROSSED.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_CROSSED.options.LEAVE.title",
            "JAMES_FORCED_BRIDGE.pages.RESULT_CROSSED.options.LEAVE.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_DETOURED.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_DETOURED.options.LEAVE.title",
            "JAMES_FORCED_BRIDGE.pages.RESULT_DETOURED.options.LEAVE.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_REFUSED.description",
            "JAMES_FORCED_BRIDGE.pages.RESULT_REFUSED.options.LEAVE.title",
            "JAMES_FORCED_BRIDGE.pages.RESULT_REFUSED.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"James localization is missing {language}/{key}.");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
