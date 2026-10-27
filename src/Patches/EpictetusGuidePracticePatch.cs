using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EpictetusGuidePracticePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        ZenoRouteFeatureState? feature = shared.ZenoRoutePayload.State;
        ZenoRouteState? route = feature?.Route;
        SenecaDailyReviewState? seneca = shared.SenecaDailyReview;
        EpictetusGuidePracticeState? saved = shared.EpictetusGuidePractice;
        EpictetusGuidePractice? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        EpictetusGuidePracticeTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            shared.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current
                && route is { Stage: ZenoRouteStage.ZenoClosed, Outcome: not null },
            seneca?.Closed == true,
            feature?.PendingOperation is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!EpictetusGuidePracticeTriggerPolicy.ShouldInsert(context)) return null;
        if (route is null || seneca is null || shared.DiogenesStaySwitchEntry is not { } entry
            || !SenecaDailyReviewFlow.IsRouteConsistent(seneca, route, entry)) return null;

        var candidate = saved ?? EpictetusGuidePracticeState.Create(
            $"EPICTETUS_GUIDE_PRACTICE:{run.Rng.Seed}:1", route, seneca);
        if (!EpictetusGuidePracticeFlow.IsRouteConsistent(candidate, route, seneca)) return null;
        shared.EpictetusGuidePractice = candidate;
        Log.Info("[STS2Philosophers] Presenting Epictetus guide practice at a native question node.");
        return canonical;
    }

    private static EpictetusGuidePractice? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<EpictetusGuidePractice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] EpictetusGuidePractice is unavailable: {exception}");
            return null;
        }
    }
}
