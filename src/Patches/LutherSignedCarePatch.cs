using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LutherSignedCarePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        var journey = shared.WesternJourney;
        var predecessor = shared.AugustinePromise;
        var saved = shared.LutherSignedCare;
        LutherSignedCare? canonical;
        try { canonical = ModelDb.Event<LutherSignedCare>(); }
        catch (Exception exception) { Log.Error($"[STS2Philosophers] Luther event unavailable: {exception}"); return; }
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var context = new LutherSignedCareTriggerContext(
            manager.IsInProgress, RouteEventMapBoundary.IsSafe(run), run.Players.Count == 1, run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss },
            shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex)?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId, journey?.LastFixedAct ?? 0, journey?.ResolvedQuestionActs.Contains(2) == true,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is PlatoCivicSeal { CanOfferLuther: true },
            predecessor is { EdgeCommitted: true, Closed: true },
            predecessor is not null && journey is not null && AugustinePromiseFlow.IsRouteConsistent(predecessor, journey),
            journey is not null && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Question")
                .Any(edge => edge.EdgeId == LutherSignedCareState.EdgeId),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && predecessor is not null
                && LutherSignedCareFlow.IsRouteConsistent(saved, journey, predecessor), canonical is not null);
        if (!LutherSignedCareTriggerPolicy.ShouldInsert(context)) return;
        if (saved is null) shared.LutherSignedCare = LutherSignedCareState.Create(
            $"LUTHER_SIGNED_CARE:{run.Rng.Seed}:2", predecessor!.InstanceId);
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }
}
