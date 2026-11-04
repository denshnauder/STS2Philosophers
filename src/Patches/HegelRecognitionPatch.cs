using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HegelRecognitionPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        // This room owns a fixed slot; a map question may never open it.
        if (!FixedEntryScope.Active || !NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || journey.RetainedEnding) return null;

        int actIndex = run.CurrentActIndex == 2 ? 3 : run.CurrentActIndex == 1 ? 2 : 0;
        if (actIndex == 0) return null;
        string edgeId = actIndex == 3 ? HegelRecognitionState.DefaultEdgeId : HegelRecognitionState.EarlyEdgeId;
        HegelRecognition? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        HegelRecognitionState? saved = shared.HegelRecognition;
        if (saved is not null && (saved.Closed || !saved.IsUsable() || saved.ActIndex != actIndex)) return null;
        bool edgeEligible = WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == edgeId);
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        HegelRecognitionTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            actIndex,
            journey.CurrentNodeId,
            journey.LastFixedAct,
            NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior),
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && HegelRecognitionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!HegelRecognitionTriggerPolicy.ShouldInsert(context)) return null;

        HegelRecognitionState candidate = saved
            ?? HegelRecognitionState.CreateAt($"HEGEL_RECOGNITION:{run.Rng.Seed}:{actIndex}", actIndex, edgeId);
        if (!HegelRecognitionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.HegelRecognition = candidate;

        Log.Info("[STS2Philosophers] Presenting Hegel's recognition room.");
        return canonical;
    }

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!FixedEntryScope.Active || !NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        HegelRecognitionState? saved = shared.HegelRecognition;
        if (saved is null || saved.Closed || !saved.IsUsable()
            || saved.ActIndex - 1 != run.CurrentActIndex) return null;
        if (shared.WesternJourney is not { } journey
            || !HegelRecognitionFlow.IsRouteConsistent(saved, journey)) return null;
        HegelRecognition? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    private static HegelRecognition? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<HegelRecognition>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] HegelRecognition is unavailable: {exception}");
            return null;
        }
    }
}
