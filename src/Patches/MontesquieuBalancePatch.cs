using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MontesquieuBalancePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        MontesquieuBalanceState? saved = shared.MontesquieuBalance;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !MontesquieuBalanceFlow.IsRouteConsistent(saved, journey)) return null;
        MontesquieuBalance? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        MontesquieuBalanceState? saved = shared.MontesquieuBalance;
        MontesquieuBalance? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == MontesquieuBalanceState.CompletionEdgeId);
        MontesquieuBalanceTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && MontesquieuBalanceFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!MontesquieuBalanceTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? MontesquieuBalanceState.Create($"MONTESQUIEU_BALANCE:{run.Rng.Seed}:2");
        if (journey is null || !MontesquieuBalanceFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.MontesquieuBalance = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static MontesquieuBalance? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MontesquieuBalance>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MontesquieuBalance is unavailable: {exception}");
            return null;
        }
    }
}
