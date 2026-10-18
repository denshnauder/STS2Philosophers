using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MachiavelliEmergencyMandatePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
        WesternJourneyState? journey = shared.WesternJourney;
        MachiavelliEmergencyMandateState? saved = shared.MachiavelliEmergencyMandate;
        MachiavelliEmergencyMandate? canonical = TryGetCanonicalEvent();
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        MachiavelliEmergencyMandateTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Machiavelli, run.CurrentActIndex),
            journey?.CompletedEdgeIds.Contains(MachiavelliEmergencyMandateState.PrerequisiteEdgeId) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            journey?.RetainedEnding == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!MachiavelliEmergencyMandateTriggerPolicy.ShouldInsert(context)) return;
        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryReserveQuestion(journey, 2)) return;
            shared.MachiavelliEmergencyMandate = MachiavelliEmergencyMandateState.Create(
                $"MACHIAVELLI_EMERGENCY_MANDATE:{run.Rng.Seed}:2");
        }
        if (!MachiavelliEmergencyMandate.IsJourneyConsistent(shared))
            throw new InvalidDataException("The Machiavelli boundary conflicts with its reserved question.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static MachiavelliEmergencyMandate? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MachiavelliEmergencyMandate>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MachiavelliEmergencyMandate is unavailable: {exception}");
            return null;
        }
    }
}
