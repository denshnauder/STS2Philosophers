using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AugustinePromisePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        AugustinePromiseState? saved = shared.AugustinePromise;
        AugustinePromise? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == AugustinePromiseState.CompletionEdgeId);
        AugustinePromiseTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(AugustinePromiseState.RequiredEdgeId) == true,
            edgeEligible,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && AugustinePromiseFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!AugustinePromiseTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? AugustinePromiseState.Create(
            $"AUGUSTINE_PROMISE:{run.Rng.Seed}:2");
        if (journey is null || !AugustinePromiseFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.AugustinePromise = candidate;

        Log.Info("[STS2Philosophers] Presenting Augustine's act-two promise inquiry.");
        return canonical;
    }

    private static AugustinePromise? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AugustinePromise>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AugustinePromise is unavailable: {exception}");
            return null;
        }
    }
}
