using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class GalileoMeasuredPracticePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex != 2 || run.Players.Count != 1
            || !RouteEventMapBoundary.IsSafe(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not HeraclitusLyre { CanOfferGalileo: true }
            || !LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Galileo, run.CurrentActIndex)
            || shared.WesternJourney is not { } journey || shared.DemocritusArrangement is not { } democritus
            || shared.EmpedoclesRootsInquiry is not { } empedocles) return;
        string id = $"GALILEO_MEASURED_PRACTICE:{run.Rng.Seed}:3";
        var state = shared.GalileoMeasuredPractice;
        if (state is not null && (state.Closed || state.InstanceId != id)) return;
        if (state is null && run.CurrentRoom is not CombatRoom
            { IsPreFinished: true, IsVictoryRoom: false, RoomType: RoomType.Monster }) return;
        var candidate = state ?? GalileoMeasuredPracticeState.Create(id, democritus.InstanceId, empedocles.InstanceId);
        if (!GalileoMeasuredPracticeFlow.IsRouteConsistent(candidate, journey, democritus, empedocles)) return;
        GalileoMeasuredPractice canonical;
        try { canonical = ModelDb.Event<GalileoMeasuredPractice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Galileo event unavailable: {exception}");
            return;
        }
        shared.GalileoMeasuredPractice = candidate;
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical);
    }
}
