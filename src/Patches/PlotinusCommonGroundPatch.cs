using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PlotinusCommonGroundPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        PlotinusCommonGroundState? saved = shared.PlotinusCommonGround;
        PlotinusCommonGround? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == PlotinusCommonGroundState.CompletionEdgeId);
        PlotinusCommonGroundTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<EpicurusSharedMargin>(), "EPICURUS_SHARED_MARGIN"),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(PlotinusCommonGroundState.RequiredEdgeId) == true,
            edgeEligible,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Plotinus,
                run.CurrentActIndex),
            shared.EpicurusSharedMargin?.Closed == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && PlotinusCommonGroundFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!PlotinusCommonGroundTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? PlotinusCommonGroundState.Create(
            $"PLOTINUS_COMMON_GROUND:{run.Rng.Seed}:3");
        if (journey is null || !PlotinusCommonGroundFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.PlotinusCommonGround = candidate;

        Log.Info("[STS2Philosophers] Presenting Plotinus's act-three common ground.");
        return canonical;
    }

    private static PlotinusCommonGround? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<PlotinusCommonGround>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PlotinusCommonGround is unavailable: {exception}");
            return null;
        }
    }
}
