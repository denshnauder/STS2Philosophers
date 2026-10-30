using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AquinasBridgeNoticePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        AquinasBridgeNoticeState? saved = shared.AquinasBridgeNotice;
        AquinasBridgeNotice? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool eligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == AquinasBridgeNoticeState.CompletionEdgeId);
        AquinasBridgeNoticeTriggerContext context = new(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            NativeQuestionNodeEvents.CombatCount(behavior),
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
        if (!AquinasBridgeNoticeTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? AquinasBridgeNoticeState.Create(
            $"AQUINAS_BRIDGE_NOTICE:{run.Rng.Seed}:3");
        if (journey is null || !AquinasBridgeNoticeFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.AquinasBridgeNotice = candidate;

        Log.Info("[STS2Philosophers] Presenting Aquinas's public bridge notice.");
        return canonical;
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
