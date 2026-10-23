using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;
internal static class CopernicusShiftedCenterPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        var saved = shared.CopernicusShiftedCenter;
        var journey = shared.WesternJourney;
        var democritus = shared.DemocritusArrangement;
        var empedocles = shared.EmpedoclesRootsInquiry;
        CopernicusShiftedCenter? canonical;
        try { canonical = ModelDb.Event<CopernicusShiftedCenter>(); }
        catch (Exception e) { Log.Error($"[STS2Philosophers] Copernicus event unavailable: {e}"); return; }
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        string id = $"COPERNICUS_SHIFTED_CENTER:{run.Rng.Seed}:2";
        bool captured = CopernicusNativeTravel.TryCapture(run, out var fork);
        bool Consistent(CopernicusShiftedCenterState state) => state.InstanceId == id && journey is not null
            && democritus is not null && empedocles is not null && CopernicusShiftedCenterFlow.IsRouteConsistent(
                state, journey, democritus, empedocles, WesternRouteGraph.LoadEmbedded());
        var context = new CopernicusShiftedCenterTriggerContext(
            manager.IsInProgress, RouteEventMapBoundary.IsSafe(run), run.Players.Count == 1, run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss },
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is HeraclitusLyre { CanOfferCopernicus: true }
                && LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Copernicus, run.CurrentActIndex),
            captured && democritus is not null && empedocles is not null
                && Consistent(CopernicusShiftedCenterState.Create(id, democritus.InstanceId, empedocles.InstanceId, fork!)),
            saved is null ? captured : saved.IsUsable() && CopernicusNativeTravel.MatchesSource(run, saved.Fork),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && Consistent(saved), canonical is not null);
        if (!CopernicusShiftedCenterTriggerPolicy.ShouldInsert(context)) return;
        if (saved is null) shared.CopernicusShiftedCenter = CopernicusShiftedCenterState.Create(
            id, democritus!.InstanceId, empedocles!.InstanceId, fork!);
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }
}
