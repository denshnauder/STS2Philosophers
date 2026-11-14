using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;
internal static class BaconSampleLedgerPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        BaconSampleLedgerState? saved = shared.BaconSampleLedger;
        WesternJourneyState? journey = shared.WesternJourney;
        if (saved is null || saved.Closed || !saved.IsUsable() || journey is null
            || !BaconSampleLedgerFlow.IsRouteConsistent(saved, journey,
                shared.OckhamNamesInquiry!, shared.GorgiasSpeechInquiry!)) return null;
        BaconSampleLedger? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

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
        if (run.CurrentActIndex == 1)
        {
            string actTwoId = $"BACON_SAMPLE_LEDGER:{run.Rng.Seed}:2";
            bool ActTwoConsistent(BaconSampleLedgerState state) => journey is not null
                && state.InstanceId == actTwoId
                && BaconSampleLedgerFlow.IsRouteConsistent(state, journey, ockham!, gorgias!);
            if (saved is not null && saved.ActIndex != 2) return null;
            bool actTwoReady = journey is not null
                && journey.RouteId == "SOCIAL_KNOWLEDGE"
                && journey.CurrentNodeId == BaconSampleLedgerState.AdoptedNodeId
                && journey.LastFixedAct is 1 or 2
                && !journey.ResolvedQuestionActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_198")
                && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                    .Any(edge => edge.EdgeId == "WESTERN_EDGE_198");
            bool actTwoRelic = journey?.RouteId == "SOCIAL_KNOWLEDGE"
                ? player?.GetRelicById(ModelDb.GetId<RousseauUncarvedStone>()) is RousseauUncarvedStone { CanOfferBacon: true }
                : player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is AristotleCategoryTablet { CanOfferBacon: true };
            if (!manager.IsInProgress || !actTwoReady || !actTwoRelic) return null;
            if (saved is not null && (!saved.IsUsable() || saved.Closed || !ActTwoConsistent(saved))) return null;
            var actTwo = saved ?? BaconSampleLedgerState.CreateAt(actTwoId, 2, "ACT_TWO_BACON");
            if (!ActTwoConsistent(actTwo)) return null;
            shared.BaconSampleLedger = actTwo;
            return canonical;
        }
        string id = $"BACON_SAMPLE_LEDGER:{run.Rng.Seed}:3";
        bool Consistent(BaconSampleLedgerState state) => journey is not null && ockham is not null && gorgias is not null
            && state.InstanceId == id && BaconSampleLedgerFlow.IsRouteConsistent(state, journey, ockham, gorgias);
        var context = new BaconSampleLedgerTriggerContext(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1, run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            NativeQuestionNodeEvents.CombatCount(shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex)),
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

    private static BaconSampleLedger? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<BaconSampleLedger>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] BaconSampleLedger is unavailable: {exception}");
            return null;
        }
    }
}
