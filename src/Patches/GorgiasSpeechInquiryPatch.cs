using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class GorgiasSpeechInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        GorgiasSpeechInquiryState? saved = shared.GorgiasSpeechInquiry;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        GorgiasSpeechInquiry? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        GorgiasSpeechInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey is not null && !journey.ResolvedQuestionActs.Contains(1),
            player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!GorgiasSpeechInquiryTriggerPolicy.ShouldInsert(context)) return;

        if (shared.GorgiasSpeechInquiry is not { } active || journey is null
            || !GorgiasSpeechInquiryFlow.IsJourneyConsistent(active, journey))
            throw new InvalidDataException("The Gorgias boundary conflicts with its route.");
        Log.Info("[STS2Philosophers] Presenting Gorgias speech inquiry at the map boundary.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static GorgiasSpeechInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<GorgiasSpeechInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] GorgiasSpeechInquiry is unavailable: {exception}");
            return null;
        }
    }
}
