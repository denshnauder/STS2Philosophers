using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HabermasCommunicationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        HabermasCommunicationState? saved = shared.HabermasCommunication;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !HabermasCommunicationFlow.IsRouteConsistent(saved, journey)) return null;
        HabermasCommunication? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        HabermasCommunicationState? saved = shared.HabermasCommunication;
        HabermasCommunication? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == HabermasCommunicationState.CompletionEdgeId);
        HabermasCommunicationTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(HabermasCommunicationState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && HabermasCommunicationFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!HabermasCommunicationTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? HabermasCommunicationState.Create($"HABERMAS_COMMUNICATION:{run.Rng.Seed}:3");
        if (journey is null || !HabermasCommunicationFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.HabermasCommunication = candidate;
        Log.Info("[STS2Philosophers] Presenting Marx's alienation question.");
        return canonical;
    }

    private static HabermasCommunication? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<HabermasCommunication>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] HabermasCommunication is unavailable: {exception}");
            return null;
        }
    }
}
