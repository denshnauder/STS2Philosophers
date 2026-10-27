using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LutherSignedCarePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not PlatoCivicSeal { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        var journey = shared.WesternJourney;
        var predecessor = shared.AugustinePromise;
        var saved = shared.LutherSignedCare;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        LutherSignedCare? canonical;
        try { canonical = ModelDb.Event<LutherSignedCare>(); }
        catch (Exception exception) { Log.Error($"[STS2Philosophers] Luther event unavailable: {exception}"); return null; }
        if (run.VisitedEventIds.Contains(canonical.Id)) return null;
        var context = new LutherSignedCareTriggerContext(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1, run.CurrentActIndex,
            NativeQuestionNodeEvents.IsEntry(run),
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
        if (!LutherSignedCareTriggerPolicy.ShouldInsert(context)) return null;

        return canonical;
    }
}
