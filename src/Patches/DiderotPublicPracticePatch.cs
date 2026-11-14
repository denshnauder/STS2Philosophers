using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DiderotPublicPracticePatch
{
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        DiderotPracticeState? saved = shared.DiderotPublicPractice;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        DiderotPublicPractice? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        DiderotPracticeState? saved = shared.DiderotPublicPractice;
        DiderotPublicPractice? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        BaconSampleLedgerState? host = shared.BaconSampleLedger;
        DiderotPublicPracticeContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, journey?.RouteId, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            host is not null && host.EdgeCommitted, host?.IsUsable() == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            canonical is not null);
        if (!DiderotPublicPracticePolicy.ShouldInsert(context)) return null;
        // The Diderot room restores only DIDEROT_PUBLIC_PRACTICE:<seed>:2. Reusing
        // Voltaire's prefix created a state the room could never restore.
        var candidate = saved ?? DiderotPracticeState.Create($"DIDEROT_PUBLIC_PRACTICE:{run.Rng.Seed}:2");
        shared.DiderotPublicPractice = candidate;
        Log.Info("[STS2Philosophers] Presenting Diderot's public practice at the act-two question node.");
        return canonical;
    }

    private static DiderotPublicPractice? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DiderotPublicPractice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DiderotPublicPractice is unavailable: {exception}");
            return null;
        }
    }
}
