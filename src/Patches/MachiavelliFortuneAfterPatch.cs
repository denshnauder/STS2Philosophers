using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MachiavelliFortuneAfterPatch
{
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        MachiavelliFortuneAfterState? saved = shared.MachiavelliFortuneAfter;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || !MachiavelliFortuneAfterFlow.IsRouteConsistent(saved, journey)) return null;
        MachiavelliFortuneAfter? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!FixedEntryScope.Active || run.CurrentActIndex != 2
            || !NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || journey.RouteId != MachiavelliFortuneAfterState.RouteId
            || journey.CurrentNodeId != MachiavelliFortuneAfterState.SourceNodeId
            || !journey.CompletedEdgeIds.Contains(MachiavelliFortuneAfterState.RequiredEdgeId)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_040")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_047")
            || journey.RetainedEnding) return null;
        MachiavelliFortuneAfterState? saved = shared.MachiavelliFortuneAfter;
        MachiavelliFortuneAfter? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        var candidate = saved ?? MachiavelliFortuneAfterState.Create(
            $"MACHIAVELLI_FORTUNE_AFTER:{run.Rng.Seed}:3");
        if (!candidate.IsUsable() || !MachiavelliFortuneAfterFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.MachiavelliFortuneAfter = candidate;
        Log.Info("[STS2Philosophers] Presenting Machiavelli's third-act fortune after the flood.");
        return canonical;
    }

    private static MachiavelliFortuneAfter? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MachiavelliFortuneAfter>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MachiavelliFortuneAfter is unavailable: {exception}");
            return null;
        }
    }
}
