using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;
internal static class CopernicusShiftedCenterPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var saved = shared.CopernicusShiftedCenter;
        var journey = shared.WesternJourney;
        var democritus = shared.DemocritusArrangement;
        var empedocles = shared.EmpedoclesRootsInquiry;
        CopernicusShiftedCenter? canonical;
        try { canonical = ModelDb.Event<CopernicusShiftedCenter>(); }
        catch (Exception e) { Log.Error($"[STS2Philosophers] Copernicus event unavailable: {e}"); return null; }
        if (run.VisitedEventIds.Contains(canonical.Id)) return null;
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        string id = $"COPERNICUS_SHIFTED_CENTER:{run.Rng.Seed}:2";
        bool captured = CopernicusNativeTravel.TryCaptureQuestion(run, out var fork);
        bool Consistent(CopernicusShiftedCenterState state) => state.InstanceId == id && journey is not null
            && democritus is not null && empedocles is not null && CopernicusShiftedCenterFlow.IsRouteConsistent(
                state, journey, democritus, empedocles, WesternRouteGraph.LoadEmbedded());
        var context = new CopernicusShiftedCenterTriggerContext(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1, run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run, ModelDb.GetId<EmpedoclesRootsInquiry>(), "EMPEDOCLES_ROOTS_INQUIRY"),
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is HeraclitusLyre { CanOfferCopernicus: true }
                && LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Copernicus, run.CurrentActIndex),
            captured && democritus is not null && empedocles is not null
                && Consistent(CopernicusShiftedCenterState.Create(id, democritus.InstanceId, empedocles.InstanceId, fork!)),
            saved is null ? captured : saved.IsUsable() && CopernicusNativeTravel.MatchesPendingQuestion(run, saved.Fork),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && Consistent(saved), canonical is not null);
        if (!CopernicusShiftedCenterTriggerPolicy.ShouldInsert(context)) return null;
        if (saved is null) shared.CopernicusShiftedCenter = CopernicusShiftedCenterState.Create(
            id, democritus!.InstanceId, empedocles!.InstanceId, fork!);
        return canonical;
    }
}
