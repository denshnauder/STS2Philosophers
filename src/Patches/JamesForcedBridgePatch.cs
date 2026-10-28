using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class JamesForcedBridgePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.JamesForcedBridge is not { } saved
            || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || shared.ZenoRoutePayload.State?.Route is not { } route
            || !JamesForcedBridgeFlow.IsRouteConsistent(saved, journey, route)) return null;
        JamesForcedBridge? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        ZenoRouteState? route = shared.ZenoRoutePayload.State?.Route;
        JamesForcedBridge? canonical = TryGetCanonicalEvent();
        if (journey is null || route is null || canonical is null
            || run.VisitedEventIds.Contains(canonical.Id)
            || journey.CurrentNodeId != JamesForcedBridgeState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains(JamesForcedBridgeState.RouteEdgeId)
            || route.Stage != ZenoRouteStage.DiogenesClosed
            || route.RouteEdgeId != JamesForcedBridgeState.RouteEdgeId) return null;

        JamesForcedBridgeState? saved = shared.JamesForcedBridge;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? JamesForcedBridgeState.Create(
            $"JAMES_FORCED_BRIDGE:{run.Rng.Seed}:2", route.RunId);
        if (!candidate.IsUsable()
            || !JamesForcedBridgeFlow.IsRouteConsistent(candidate, journey, route)) return null;
        shared.JamesForcedBridge = candidate;

        Log.Info("[STS2Philosophers] Presenting James's forced bridge at Plato's virtue node.");
        return canonical;
    }

    private static JamesForcedBridge? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<JamesForcedBridge>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] JamesForcedBridge is unavailable: {exception}");
            return null;
        }
    }
}
