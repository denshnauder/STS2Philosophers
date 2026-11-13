using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SaussureSignPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        SaussureSignState? saved = shared.SaussureSign;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !SaussureSignFlow.IsRouteConsistent(saved, journey)) return null;
        SaussureSign? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SaussureSignState? saved = shared.SaussureSign;
        SaussureSign? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        // A first-act direction can stand Saussure on the room's own node
        // before the second act. The room then owns that act's fixed slot and
        // hands over forward to the line's third-act figure.
        if (run.CurrentActIndex == 1 && journey is not null
            && journey.CurrentNodeId == SaussureSignState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_186"))
        {
            WesternRouteGraph turnedGraph = WesternRouteGraph.LoadEmbedded();
            WesternGraphEdge? forward = turnedGraph.GetEligibleEdges(journey, "Fixed")
                .FirstOrDefault(edge => edge.Act == 2 && edge.FromNodeId == SaussureSignState.AdoptedNodeId);
            if (forward is null) return null;
            string turnedId = $"SAUSSURE_SIGN:{run.Rng.Seed}:2";
            SaussureSignState turned = saved ?? SaussureSignState.CreateAt(turnedId, 2, forward.EdgeId);
            turned.EdgeId = forward.EdgeId;
            turned.ActIndex = 2;
            turned.SourceNode = SaussureSignState.AdoptedNodeId;
            turned.TargetNode = forward.ToNodeId;
            if (!SaussureSignFlow.IsRouteConsistent(turned, journey)) return null;
            shared.SaussureSign = turned;
            return canonical;
        }
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "FERDINAND_DE_SAUSSURE" ? 2
                : route.ActThreeFigure == "FERDINAND_DE_SAUSSURE" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == SaussureSignState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? SaussureSignState.CompletionEdgeId
            : SaussureSignState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        SaussureSignTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            SaussureSignState.RequiredEdgeId.Length == 0
                || journey?.CompletedEdgeIds.Contains(SaussureSignState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && SaussureSignFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!SaussureSignTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? SaussureSignState.CreateAt($"SAUSSURE_SIGN:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : SaussureSignState.Create($"SAUSSURE_SIGN:{run.Rng.Seed}:1"));
        if (journey is null || !SaussureSignFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.SaussureSign = candidate;
        Log.Info("[STS2Philosophers] Presenting Saussure's sign question.");
        return canonical;
    }

    private static SaussureSign? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SaussureSign>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SaussureSign is unavailable: {exception}");
            return null;
        }
    }
}
