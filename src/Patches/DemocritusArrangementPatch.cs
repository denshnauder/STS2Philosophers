using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DemocritusArrangementPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        DemocritusArrangementState? saved = shared.DemocritusArrangement;
        DemocritusArrangement? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        DemocritusArrangementTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Democritus, run.CurrentActIndex),
            journey?.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId) == true,
            journey?.RetainedEnding == true,
            NativeQuestionNodeEvents.IsEntry(run),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!DemocritusArrangementTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? DemocritusArrangementState.Create(
            $"DEMOCRITUS_ARRANGEMENT:{run.Rng.Seed}:2");
        if (journey is null || !DemocritusArrangementFlow.IsJourneyConsistent(
                candidate, journey, WesternRouteGraph.LoadEmbedded())) return null;
        shared.DemocritusArrangement = candidate;
        return canonical;
    }

    private static DemocritusArrangement? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DemocritusArrangement>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DemocritusArrangement is unavailable: {exception}");
            return null;
        }
    }
}
