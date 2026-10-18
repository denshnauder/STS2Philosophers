using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaximenesBreathInvitationPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy)
            || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AnaximenesBreathInvitation? canonical = TryGetCanonicalEvent();
        AnaximenesBreathInvitationState? saved = philosophy.AnaximenesBreathInvitation;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        AnaximenesBreathInvitationTriggerContext context = new(
            manager.IsInProgress, RouteEventMapBoundary.IsSafe(run), run.Players.Count == 1,
            run.CurrentActIndex, behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId,
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            player?.GetRelicById(ModelDb.GetId<AnaximenesBreath>()) is not null,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            canonical is not null);
        if (!AnaximenesBreathInvitationTriggerPolicy.ShouldInsert(context)) return;
        philosophy.AnaximenesBreathInvitation ??= AnaximenesBreathInvitationState.Create(
            $"ANAXIMENES_BREATH_INVITATION:{run.Rng.Seed}:3");
        Log.Info("[STS2Philosophers] Presenting Anaximenes breath practice after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AnaximenesBreathInvitation? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnaximenesBreathInvitation>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnaximenesBreathInvitation is unavailable: {exception}");
            return null;
        }
    }
}
