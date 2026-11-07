using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class WollstonecraftAdmissionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WollstonecraftAdmissionState? saved = shared.WollstonecraftAdmission;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !WollstonecraftAdmissionFlow.IsRouteConsistent(saved, journey)) return null;
        WollstonecraftAdmission? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        WollstonecraftAdmissionState? saved = shared.WollstonecraftAdmission;
        WollstonecraftAdmission? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "MARY_WOLLSTONECRAFT" ? 2
                : route.ActThreeFigure == "MARY_WOLLSTONECRAFT" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == WollstonecraftAdmissionState.CompletionEdgeId);
        WollstonecraftAdmissionTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            edgeEligible,
            journey?.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.RivalEdgeId) == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && WollstonecraftAdmissionFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!WollstonecraftAdmissionTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? WollstonecraftAdmissionState.Create(
            $"WOLLSTONECRAFT_ADMISSION:{run.Rng.Seed}:2");
        if (journey is null || !WollstonecraftAdmissionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.WollstonecraftAdmission = candidate;

        Log.Info("[STS2Philosophers] Presenting Wollstonecraft's equal-admission question.");
        return canonical;
    }

    private static WollstonecraftAdmission? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<WollstonecraftAdmission>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] WollstonecraftAdmission is unavailable: {exception}");
            return null;
        }
    }
}
