using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ArendtBeginningPatch
{
    // Arendt closes two lines: the sympathy-and-public-action line after Mill
    // (175 -> 176) and the genealogy line after Foucault (181 -> 182).
    private static readonly string[] RequiredEdges =
        [ArendtBeginningState.MillRequiredEdgeId, ArendtBeginningState.FoucaultRequiredEdgeId];

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        ArendtBeginningState? saved = shared.ArendtBeginning;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !ArendtBeginningFlow.IsRouteConsistent(saved, journey)) return null;
        ArendtBeginning? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        ArendtBeginningState? saved = shared.ArendtBeginning;
        ArendtBeginning? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        string? required = saved?.RequiredEdgeId
            ?? RequiredEdges.FirstOrDefault(edge => journey?.CompletedEdgeIds.Contains(edge) == true);
        if (required is null || journey is null) return null;
        string completion = ArendtBeginningFlow.CompletionEdgeIdOf(required);
        bool arrived = journey.CurrentNodeId == ArendtBeginningState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains(completion);
        if (arrived)
        {
            var arrivedState = saved ?? ArendtBeginningState.Create($"ARENDT_BEGINNING:{run.Rng.Seed}:3", required);
            if (!ArendtBeginningFlow.IsRouteConsistent(arrivedState, journey)) return null;
            shared.ArendtBeginning = arrivedState;
            Log.Info("[STS2Philosophers] Presenting Arendt's public beginning after Mill's handover.");
            return canonical;
        }
        if (journey.CurrentNodeId != ArendtBeginningFlow.SourceNodeIdOf(required)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == completion);
        ModelId predecessor = required == ArendtBeginningState.FoucaultRequiredEdgeId
            ? ModelDb.GetId<FoucaultPower>() : ModelDb.GetId<MillHarm>();
        string predecessorRoot = required == ArendtBeginningState.FoucaultRequiredEdgeId ? "FOUCAULT_POWER" : "MILL_HARM";
        ArendtBeginningTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombatAfter(run, predecessor, predecessorRoot),
            NativeQuestionNodeEvents.CombatCount(behavior), journey.CurrentNodeId, journey.LastFixedAct,
            journey.CompletedEdgeIds.Contains(required), edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && ArendtBeginningFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!ArendtBeginningTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? ArendtBeginningState.Create($"ARENDT_BEGINNING:{run.Rng.Seed}:3", required);
        if (!ArendtBeginningFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.ArendtBeginning = candidate;
        Log.Info("[STS2Philosophers] Presenting Arendt's public beginning.");
        return canonical;
    }

    private static ArendtBeginning? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ArendtBeginning>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ArendtBeginning is unavailable: {exception}");
            return null;
        }
    }
}
