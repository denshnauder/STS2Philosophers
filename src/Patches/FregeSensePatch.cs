using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class FregeSensePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        FregeSenseState? saved = shared.FregeSense;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !FregeSenseFlow.IsRouteConsistent(saved, journey)) return null;
        FregeSense? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        FregeSenseState? saved = shared.FregeSense;
        FregeSense? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == FregeSenseState.CompletionEdgeId);
        FregeSenseTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && WesternFirstActBranch.IsChosen(run, journey, WesternRouteGraph.LoadEmbedded(),
                    FregeSenseState.CompletionEdgeId),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && FregeSenseFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!FregeSenseTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? FregeSenseState.Create($"FREGE_SENSE:{run.Rng.Seed}:1");
        if (journey is null || !FregeSenseFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.FregeSense = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static FregeSense? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<FregeSense>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] FregeSense is unavailable: {exception}");
            return null;
        }
    }
}
