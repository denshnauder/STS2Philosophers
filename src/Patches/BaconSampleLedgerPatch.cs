using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;
internal static class BaconSampleLedgerPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var journey = shared.WesternJourney;
        var saved = shared.BaconSampleLedger;
        var ockham = shared.OckhamNamesInquiry;
        var gorgias = shared.GorgiasSpeechInquiry;
        BaconSampleLedger? canonical;
        try { canonical = ModelDb.Event<BaconSampleLedger>(); }
        catch (Exception e) { Log.Error($"[STS2Philosophers] Bacon event unavailable: {e}"); return null; }
        if (run.VisitedEventIds.Contains(canonical.Id)) return null;
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        string id = $"BACON_SAMPLE_LEDGER:{run.Rng.Seed}:3";
        bool Consistent(BaconSampleLedgerState state) => journey is not null && ockham is not null && gorgias is not null
            && state.InstanceId == id && BaconSampleLedgerFlow.IsRouteConsistent(state, journey, ockham, gorgias);
        var context = new BaconSampleLedgerTriggerContext(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1, run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex)?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey is not null && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == BaconSampleLedgerState.CompletionEdgeId),
            player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is AristotleCategoryTablet { CanOfferBacon: true }
                && LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Bacon, run.CurrentActIndex),
            ockham is not null && Consistent(BaconSampleLedgerState.Create(id, ockham.InstanceId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && Consistent(saved), canonical is not null);
        if (!BaconSampleLedgerTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? BaconSampleLedgerState.Create(id, ockham!.InstanceId);
        if (!Consistent(candidate)) return null;
        shared.BaconSampleLedger = candidate;
        return canonical;
    }
}
