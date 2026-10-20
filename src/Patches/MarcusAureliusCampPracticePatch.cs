using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MarcusAureliusCampPracticePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        ZenoRouteFeatureState? feature = shared.ZenoRoutePayload.State;
        ZenoRouteState? route = feature?.Route;
        SenecaDailyReviewState? seneca = shared.SenecaDailyReview;
        EpictetusGuidePracticeState? epictetus = shared.EpictetusGuidePractice;
        MarcusAureliusCampPracticeState? saved = shared.MarcusAureliusCampPractice;
        MarcusAureliusCampPractice? canonical = TryGetCanonicalEvent();
        MarcusAureliusCampPracticeTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss },
            shared.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current
                && route is { Stage: ZenoRouteStage.ZenoClosed, Outcome: not null },
            seneca?.Closed == true,
            epictetus?.Closed == true,
            feature?.PendingOperation is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!MarcusAureliusCampPracticeTriggerPolicy.ShouldInsert(context)) return;
        if (route is null || seneca is null || epictetus is null
            || shared.DiogenesStaySwitchEntry is not { } entry
            || !SenecaDailyReviewFlow.IsRouteConsistent(seneca, route, entry)
            || !EpictetusGuidePracticeFlow.IsRouteConsistent(epictetus, route, seneca)) return;

        if (saved is null)
            shared.MarcusAureliusCampPractice = MarcusAureliusCampPracticeState.Create(
                $"MARCUS_AURELIUS_CAMP_PRACTICE:{run.Rng.Seed}:1", route, epictetus);
        if (shared.MarcusAureliusCampPractice is not { } active
            || !MarcusAureliusCampPracticeFlow.IsRouteConsistent(active, route, epictetus))
            throw new InvalidDataException("The Marcus Aurelius practice conflicts with its saved Stoic route.");
        Log.Info("[STS2Philosophers] Presenting Marcus Aurelius camp practice after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
