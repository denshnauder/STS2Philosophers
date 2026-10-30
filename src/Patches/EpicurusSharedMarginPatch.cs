using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EpicurusSharedMarginPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        EpicurusSharedMargin? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        EpicurusSharedMarginState? saved = philosophy.EpicurusSharedMargin;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        EpicurusSharedMarginTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.CompletedEdgeIds.Contains(EpicurusSharedMarginState.RequiredEdgeId) == true,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.Epicurus,
                run.CurrentActIndex),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!EpicurusSharedMarginTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? EpicurusSharedMarginState.Create(
            $"EPICURUS_SHARED_MARGIN:{run.Rng.Seed}:3");
        if (!candidate.IsUsable()) return null;
        philosophy.EpicurusSharedMargin = candidate;

        Log.Info("[STS2Philosophers] Presenting Epicurus's act-three shared margin.");
        return canonical;
    }

    private static EpicurusSharedMargin? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<EpicurusSharedMargin>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] EpicurusSharedMargin is unavailable: {exception}");
            return null;
        }
    }
}
