using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaximanderOriginInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AnaximanderOriginInquiry? canonical = TryGetCanonicalEvent();
        AnaximanderOriginInquiryState? saved = philosophy.AnaximanderOriginInquiry;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        AnaximanderOriginInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            philosophy.ParmenidesInquiry is { Closed: true } prior && prior.IsUsable(),
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            journey is not null && !journey.DeclinedFixedActs.Contains(2),
            journey is not null && graph.GetEligibleEdges(journey, "Fixed").Count == 0,
            journey is not null && !journey.ResolvedQuestionActs.Contains(2),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!AnaximanderOriginInquiryTriggerPolicy.ShouldInsert(context)) return;


        Log.Info("[STS2Philosophers] Presenting Anaximander origin inquiry after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AnaximanderOriginInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnaximanderOriginInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnaximanderOriginInquiry is unavailable: {exception}");
            return null;
        }
    }
}
