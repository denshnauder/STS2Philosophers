using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ParmenidesInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        ParmenidesInquiry? canonical = TryGetCanonicalEvent();
        ParmenidesInquiryState? saved = philosophy.ParmenidesInquiry;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        ParmenidesInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!ParmenidesInquiryTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryReserveQuestion(journey, 1)) return;
            philosophy.ParmenidesInquiry = ParmenidesInquiryState.Create(
                $"PARMENIDES_INQUIRY:{run.Rng.Seed}:1");
        }

        Log.Info("[STS2Philosophers] Presenting Parmenides inquiry after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static ParmenidesInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ParmenidesInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ParmenidesInquiry is unavailable: {exception}");
            return null;
        }
    }
}
