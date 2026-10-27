using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MarcusAureliusCampPracticePatch
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
        EpictetusGuidePracticeState? epictetus = shared.EpictetusGuidePractice;
        MarcusAureliusCampPracticeState? saved = shared.MarcusAureliusCampPractice;
        MarcusAureliusCampPractice? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        MarcusAureliusCampPracticeTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            saved is not null || NativeQuestionNodeEvents.HasCompletedCombatAfter(
                run, ModelDb.GetId<EpictetusGuidePractice>(), "EPICTETUS_GUIDE_PRACTICE"),
            shared.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current
                && route is { Stage: ZenoRouteStage.ZenoClosed, Outcome: not null },
            seneca?.Closed == true,
            epictetus?.Closed == true,
            feature?.PendingOperation is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!MarcusAureliusCampPracticeTriggerPolicy.ShouldInsert(context)) return null;
        if (route is null || seneca is null || epictetus is null
            || shared.DiogenesStaySwitchEntry is not { } entry
            || !SenecaDailyReviewFlow.IsRouteConsistent(seneca, route, entry)
            || !EpictetusGuidePracticeFlow.IsRouteConsistent(epictetus, route, seneca)) return null;

        var candidate = saved ?? MarcusAureliusCampPracticeState.Create(
            $"MARCUS_AURELIUS_CAMP_PRACTICE:{run.Rng.Seed}:1", route, epictetus);
        if (!MarcusAureliusCampPracticeFlow.IsRouteConsistent(candidate, route, epictetus)) return null;
        shared.MarcusAureliusCampPractice = candidate;
        Log.Info("[STS2Philosophers] Presenting Marcus Aurelius camp practice at a native question node.");
        return canonical;
    }

    private static MarcusAureliusCampPractice? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MarcusAureliusCampPractice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MarcusAureliusCampPractice is unavailable: {exception}");
            return null;
        }
    }
}
