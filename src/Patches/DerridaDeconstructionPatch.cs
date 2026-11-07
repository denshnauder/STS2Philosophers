using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DerridaDeconstructionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        DerridaDeconstructionState? saved = shared.DerridaDeconstruction;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !DerridaDeconstructionFlow.IsRouteConsistent(saved, journey)) return null;
        DerridaDeconstruction? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        DerridaDeconstructionState? saved = shared.DerridaDeconstruction;
        DerridaDeconstruction? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool fixedEntry = FixedEntryScope.Active;
        bool fixedMode = fixedEntry && journey is not null
            && journey.CompletedEdgeIds.Contains(DerridaDeconstructionState.FixedRequiredEdgeId)
            && graph.GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == DerridaDeconstructionState.FixedCompletionEdgeId);
        if (fixedEntry && !fixedMode) return null;
        string edgeId = fixedMode
            ? DerridaDeconstructionState.FixedCompletionEdgeId
            : DerridaDeconstructionState.CompletionEdgeId;
        string requiredEdgeId = fixedMode
            ? DerridaDeconstructionState.FixedRequiredEdgeId
            : DerridaDeconstructionState.RequiredEdgeId;
        bool edgeEligible = journey is not null && graph
            .GetEligibleEdges(journey, fixedMode ? "Fixed" : "Question")
            .Any(edge => edge.EdgeId == edgeId);
        DerridaDeconstructionTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(requiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && DerridaDeconstructionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!DerridaDeconstructionTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? DerridaDeconstructionState.CreateAt(
            $"DERRIDA_DECONSTRUCTION:{run.Rng.Seed}:3", 3, edgeId, fixedMode);
        if (journey is null || !DerridaDeconstructionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.DerridaDeconstruction = candidate;
        Log.Info("[STS2Philosophers] Presenting Derrida's deconstruction question.");
        return canonical;
    }

    private static DerridaDeconstruction? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DerridaDeconstruction>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DerridaDeconstruction is unavailable: {exception}");
            return null;
        }
    }
}
