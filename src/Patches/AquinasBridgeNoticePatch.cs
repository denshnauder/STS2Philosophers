using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AquinasBridgeNoticePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;

        WesternJourneyState? journey = shared.WesternJourney;
        AquinasBridgeNoticeState? saved = shared.AquinasBridgeNotice;
        AquinasBridgeNotice? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool eligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == AquinasBridgeNoticeState.CompletionEdgeId);
        AquinasBridgeNoticeTriggerContext context = new(
            manager.IsInProgress, RouteEventMapBoundary.IsSafe(run), run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(AquinasBridgeNoticeState.RequiredEdgeId) == true,
            journey?.CompletedEdgeIds.Contains("WESTERN_EDGE_017") == true,
            eligible,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Aquinas,
                run.CurrentActIndex),
            shared.AristotleCivicInquiry is { Closed: true } predecessor
                && predecessor.IsUsable() && predecessor.Stage == AristotleCivicInquiryStage.ResultAdopted,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null
                && AquinasBridgeNoticeFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!AquinasBridgeNoticeTriggerPolicy.ShouldInsert(context)) return;
        if (saved is null)
            shared.AquinasBridgeNotice = AquinasBridgeNoticeState.Create(
                $"AQUINAS_BRIDGE_NOTICE:{run.Rng.Seed}:3");

        Log.Info("[STS2Philosophers] Presenting Aquinas's public bridge notice.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AquinasBridgeNotice? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AquinasBridgeNotice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AquinasBridgeNotice is unavailable: {exception}");
            return null;
        }
    }
}
