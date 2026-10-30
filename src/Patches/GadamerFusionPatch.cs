using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class GadamerFusionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        GadamerFusionState? saved = shared.GadamerFusion;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !GadamerFusionFlow.IsRouteConsistent(saved, journey)) return null;
        GadamerFusion? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        GadamerFusionState? saved = shared.GadamerFusion;
        GadamerFusion? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == GadamerFusionState.CompletionEdgeId);
        GadamerFusionTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            GadamerFusionState.RequiredEdgeId.Length == 0
                || journey?.CompletedEdgeIds.Contains(GadamerFusionState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && GadamerFusionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!GadamerFusionTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? GadamerFusionState.Create($"GADAMER_FUSION:{run.Rng.Seed}:3");
        if (journey is null || !GadamerFusionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.GadamerFusion = candidate;
        Log.Info("[STS2Philosophers] Presenting Marx's alienation question.");
        return canonical;
    }

    private static GadamerFusion? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<GadamerFusion>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] GadamerFusion is unavailable: {exception}");
            return null;
        }
    }
}
