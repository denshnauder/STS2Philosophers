using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AristotleCivicInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AristotleCivicInquiry? canonical = TryGetCanonicalEvent();
        AristotleCivicInquiryState? saved = philosophy.AristotleCivicInquiry;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        AristotleCivicInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!AristotleCivicInquiryTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null) return;
            philosophy.AristotleCivicInquiry = AristotleCivicInquiryState.Create(
                $"ARISTOTLE_CIVIC_INQUIRY:{run.Rng.Seed}:2");
        }

        Log.Info("[STS2Philosophers] Presenting Aristotle civic inquiry after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AristotleCivicInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AristotleCivicInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AristotleCivicInquiry is unavailable: {exception}");
            return null;
        }
    }
}

