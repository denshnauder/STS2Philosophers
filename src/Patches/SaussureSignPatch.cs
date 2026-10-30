using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SaussureSignPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        SaussureSignState? saved = shared.SaussureSign;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !SaussureSignFlow.IsRouteConsistent(saved, journey)) return null;
        SaussureSign? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SaussureSignState? saved = shared.SaussureSign;
        SaussureSign? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == SaussureSignState.CompletionEdgeId);
        SaussureSignTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            SaussureSignState.RequiredEdgeId.Length == 0
                || journey?.CompletedEdgeIds.Contains(SaussureSignState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && SaussureSignFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!SaussureSignTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? SaussureSignState.Create($"SAUSSURE_SIGN:{run.Rng.Seed}:1");
        if (journey is null || !SaussureSignFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.SaussureSign = candidate;
        Log.Info("[STS2Philosophers] Presenting Saussure's sign question.");
        return canonical;
    }

    private static SaussureSign? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SaussureSign>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SaussureSign is unavailable: {exception}");
            return null;
        }
    }
}
