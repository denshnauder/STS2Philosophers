using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SenecaDailyReviewPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        ZenoRouteFeatureState? feature = shared.ZenoRoutePayload.State;
        ZenoRouteState? route = feature?.Route;
        DiogenesStaySwitchEntryRecord? entry = shared.DiogenesStaySwitchEntry;
        SenecaDailyReviewState? saved = shared.SenecaDailyReview;
        SenecaDailyReview? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        SenecaDailyReviewTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            shared.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current
                && route is { Stage: ZenoRouteStage.ZenoClosed, Outcome: not null },
            entry?.Closed == true,
            feature?.PendingOperation is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!SenecaDailyReviewTriggerPolicy.ShouldInsert(context)) return null;
        if (route is null || entry is null || route.RunId != entry.RunId
            || route.Outcome is null || !DiogenesStaySwitchEntryPolicy.IsValid(entry)) return null;

        var candidate = saved ?? SenecaDailyReviewState.Create(
            $"SENECA_DAILY_REVIEW:{run.Rng.Seed}:1", route, entry);
        if (!SenecaDailyReviewFlow.IsRouteConsistent(candidate, route, entry)) return null;
        shared.SenecaDailyReview = candidate;
        Log.Info("[STS2Philosophers] Presenting Seneca daily review at a native question node.");
        return canonical;
    }

    private static SenecaDailyReview? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SenecaDailyReview>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SenecaDailyReview is unavailable: {exception}");
            return null;
        }
    }
}
