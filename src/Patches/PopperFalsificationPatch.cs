using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PopperFalsificationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.PopperFalsification is not { } saved || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || !PopperFalsificationFlow.IsRouteConsistent(saved, journey)) return null;
        PopperFalsification? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 2
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        PopperFalsification? canonical = TryGetCanonicalEvent();
        if (journey is null || canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey.RetainedEnding
            || journey.CurrentNodeId != PopperFalsificationState.SourceNodeId
            || journey.LastFixedAct != 3
            || !journey.CompletedEdgeIds.Contains(PopperFalsificationState.RequiredEdgeId)
            || journey.ResolvedQuestionActs.Contains(3)) return null;

        PopperFalsificationState? saved = shared.PopperFalsification;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? PopperFalsificationState.Create(
            $"POPPER_FALSIFICATION:{run.Rng.Seed}:3");
        if (!candidate.IsUsable()
            || !PopperFalsificationFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.PopperFalsification = candidate;

        Log.Info("[STS2Philosophers] Presenting Popper's dangerous prediction at Bacon's terminal node.");
        return canonical;
    }

    private static PopperFalsification? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<PopperFalsification>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PopperFalsification is unavailable: {exception}");
            return null;
        }
    }
}
