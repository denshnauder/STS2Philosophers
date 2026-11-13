using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class VoltairePublicCritiquePatch
{
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        VoltaireToleranceState? saved = shared.VoltairePublicCritique;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        VoltairePublicCritique? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        VoltaireToleranceState? saved = shared.VoltairePublicCritique;
        VoltairePublicCritique? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        MontesquieuBalanceState? host = shared.MontesquieuBalance;
        VoltairePublicCritiqueContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, journey?.RouteId, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            host is not null && host.EdgeCommitted, host?.IsUsable() == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            canonical is not null);
        if (!VoltairePublicCritiquePolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? VoltaireToleranceState.Create($"VOLTAIRE_PUBLIC_CRITIQUE:{run.Rng.Seed}:2");
        shared.VoltairePublicCritique = candidate;
        Log.Info("[STS2Philosophers] Presenting Voltaire's public critique at the act-two question node.");
        return canonical;
    }

    private static VoltairePublicCritique? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<VoltairePublicCritique>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] VoltairePublicCritique is unavailable: {exception}");
            return null;
        }
    }
}
