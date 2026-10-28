using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class CarnapFrameworkChoicePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.CarnapFrameworkChoice is not { } saved || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || !CarnapFrameworkChoiceFlow.IsRouteConsistent(saved, journey)) return null;
        CarnapFrameworkChoice? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        CarnapFrameworkChoice? canonical = TryGetCanonicalEvent();
        if (journey is null || canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey.RetainedEnding
            || journey.CurrentNodeId != CarnapFrameworkChoiceState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")) return null;

        CarnapFrameworkChoiceState? saved = shared.CarnapFrameworkChoice;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? CarnapFrameworkChoiceState.Create(
            $"CARNAP_FRAMEWORK_CHOICE:{run.Rng.Seed}:2");
        if (!candidate.IsUsable()
            || !CarnapFrameworkChoiceFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.CarnapFrameworkChoice = candidate;

        Log.Info("[STS2Philosophers] Presenting Carnap's framework choice at Aristotle's language node after Ockham.");
        return canonical;
    }

    private static CarnapFrameworkChoice? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<CarnapFrameworkChoice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] CarnapFrameworkChoice is unavailable: {exception}");
            return null;
        }
    }
}
