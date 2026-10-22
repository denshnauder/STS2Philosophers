using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;
internal static class MachiavelliFloodPreparationPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        var journey = shared.WesternJourney;
        var saved = shared.MachiavelliFloodPreparation;
        MachiavelliFloodPreparation? canonical;
        try { canonical = ModelDb.Event<MachiavelliFloodPreparation>(); }
        catch (Exception e) { Log.Error($"[STS2Philosophers] Machiavelli event unavailable: {e}"); return; }
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var context = new MachiavelliFloodPreparationTriggerContext(
            manager.IsInProgress, RouteEventMapBoundary.IsSafe(run), run.Players.Count == 1, run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss },
            shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex)?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey is not null && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == MachiavelliFloodPreparationState.CompletionEdgeId),
            player?.GetRelicById(ModelDb.GetId<RousseauUncarvedStone>()) is RousseauUncarvedStone { CanOfferMachiavelli: true }
                && LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Machiavelli, run.CurrentActIndex),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && MachiavelliFloodPreparationFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!MachiavelliFloodPreparationTriggerPolicy.ShouldInsert(context)) return;
        if (saved is null) shared.MachiavelliFloodPreparation = MachiavelliFloodPreparationState.Create(
            $"MACHIAVELLI_FLOOD_PREPARATION:{run.Rng.Seed}:2");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }
}
