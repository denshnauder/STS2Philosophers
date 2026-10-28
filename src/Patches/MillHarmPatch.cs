using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MillHarmPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        MillHarmState? saved = shared.MillHarm;
        MillHarm? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == MillHarmState.CompletionEdgeId);
        MillHarmTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<WollstonecraftAdmission>(), "WOLLSTONECRAFT_ADMISSION"),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(MillHarmState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && MillHarmFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!MillHarmTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? MillHarmState.Create($"MILL_HARM:{run.Rng.Seed}:2");
        if (journey is null || !MillHarmFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.MillHarm = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static MillHarm? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MillHarm>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MillHarm is unavailable: {exception}");
            return null;
        }
    }
}
