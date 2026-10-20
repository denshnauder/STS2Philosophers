using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EpictetusGuidePracticePatch
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
        EpictetusGuidePracticeState? saved = shared.EpictetusGuidePractice;
        EpictetusGuidePractice? canonical = TryGetCanonicalEvent();
        EpictetusGuidePracticeTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
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
        if (!EpictetusGuidePracticeTriggerPolicy.ShouldInsert(context)) return;
        if (route is null || seneca is null || shared.DiogenesStaySwitchEntry is not { } entry
            || !SenecaDailyReviewFlow.IsRouteConsistent(seneca, route, entry)) return;

        if (saved is null)
            shared.EpictetusGuidePractice = EpictetusGuidePracticeState.Create(
                $"EPICTETUS_GUIDE_PRACTICE:{run.Rng.Seed}:1", route, seneca);
        if (shared.EpictetusGuidePractice is not { } active
            || !EpictetusGuidePracticeFlow.IsRouteConsistent(active, route, seneca))
            throw new InvalidDataException("The Epictetus practice conflicts with its saved Stoic route.");
        Log.Info("[STS2Philosophers] Presenting Epictetus guide practice at the map boundary.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
