using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class OckhamNamesInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        GorgiasSpeechInquiryState? gorgias = shared.GorgiasSpeechInquiry;
        OckhamNamesInquiryState? saved = shared.OckhamNamesInquiry;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        OckhamNamesInquiry? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool gorgiasConsistent = journey is not null && gorgias is not null
            && GorgiasSpeechInquiryFlow.IsJourneyConsistent(gorgias, journey);
        OckhamNamesInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is AristotleCategoryTablet { CanOfferOckham: true },
            gorgias?.Closed == true,
            gorgiasConsistent,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && gorgias is not null
                && OckhamNamesInquiryFlow.IsRouteConsistent(saved, journey, gorgias),
            canonical is not null);
        if (!OckhamNamesInquiryTriggerPolicy.ShouldInsert(context)) return;


        Log.Info("[STS2Philosophers] Presenting Ockham's second-act language critique.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static OckhamNamesInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<OckhamNamesInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] OckhamNamesInquiry is unavailable: {exception}");
            return null;
        }
    }
}
