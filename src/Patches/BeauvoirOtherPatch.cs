using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class BeauvoirOtherPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        BeauvoirOtherState? saved = shared.BeauvoirOther;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !BeauvoirOtherFlow.IsRouteConsistent(saved, journey)) return null;
        BeauvoirOther? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        BeauvoirOtherState? saved = shared.BeauvoirOther;
        BeauvoirOther? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == BeauvoirOtherState.CompletionEdgeId);
        BeauvoirOtherTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<WollstonecraftAdmission>(), "WOLLSTONECRAFT_ADMISSION"),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(BeauvoirOtherState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && BeauvoirOtherFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!BeauvoirOtherTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? BeauvoirOtherState.Create($"BEAUVOIR_OTHER:{run.Rng.Seed}:3");
        if (journey is null || !BeauvoirOtherFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.BeauvoirOther = candidate;
        Log.Info("[STS2Philosophers] Presenting Beauvoir's other-subject question.");
        return canonical;
    }

    private static BeauvoirOther? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<BeauvoirOther>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] BeauvoirOther is unavailable: {exception}");
            return null;
        }
    }
}
