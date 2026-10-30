using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KantApperceptionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KantApperceptionState? saved = shared.KantApperception;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KantApperceptionFlow.IsRouteConsistent(saved, journey)) return null;
        KantApperception? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantApperceptionState? saved = shared.KantApperception;
        KantApperception? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == KantApperceptionState.CompletionEdgeId);
        KantApperceptionTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfterAcrossActs(run,
                ModelDb.GetId<HumeInduction>(), "HUME_INDUCTION"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey is not null && KantApperceptionState.HasHumeArrival(journey),
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && KantApperceptionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!KantApperceptionTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? KantApperceptionState.Create($"KANT_APPERCEPTION:{run.Rng.Seed}:3");
        if (journey is null || !KantApperceptionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KantApperception = candidate;

        Log.Info("[STS2Philosophers] Presenting Kant's apperception question.");
        return canonical;
    }

    private static KantApperception? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KantApperception>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KantApperception is unavailable: {exception}");
            return null;
        }
    }
}
