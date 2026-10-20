using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SenecaDailyReviewPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        ZenoRouteFeatureState? feature = shared.ZenoRoutePayload.State;
        ZenoRouteState? route = feature?.Route;
        DiogenesStaySwitchEntryRecord? entry = shared.DiogenesStaySwitchEntry;
        SenecaDailyReviewState? saved = shared.SenecaDailyReview;
        SenecaDailyReview? canonical = TryGetCanonicalEvent();
        SenecaDailyReviewTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
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
        if (!SenecaDailyReviewTriggerPolicy.ShouldInsert(context)) return;
        if (route is null || entry is null || route.RunId != entry.RunId
            || route.Outcome is null || !DiogenesStaySwitchEntryPolicy.IsValid(entry)) return;

        if (saved is null)
            shared.SenecaDailyReview = SenecaDailyReviewState.Create(
                $"SENECA_DAILY_REVIEW:{run.Rng.Seed}:1", route, entry);
        if (shared.SenecaDailyReview is not { } active
            || !SenecaDailyReviewFlow.IsRouteConsistent(active, route, entry))
            throw new InvalidDataException("The Seneca review conflicts with its saved Zeno route.");
        Log.Info("[STS2Philosophers] Presenting Seneca daily review at the map boundary.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
