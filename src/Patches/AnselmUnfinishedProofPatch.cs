using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnselmUnfinishedProofPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not SocratesQuestionCup { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        PyrrhoReportedCrossingState? pyrrho = shared.PyrrhoReportedCrossing;
        AnselmUnfinishedProofState? saved = shared.AnselmUnfinishedProof;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        AnselmUnfinishedProof? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool pyrrhoConsistent = journey is not null && pyrrho is not null
            && shared.SextusOpposedAppearances is { } sextus
            && shared.DescartesMethodicReconstruction is { } descartes
            && PyrrhoReportedCrossingFlow.IsRouteConsistent(pyrrho, journey, sextus, descartes);
        AnselmUnfinishedProofTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.IsEntry(run),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            journey?.LastFixedAct == 3,
            journey?.ResolvedQuestionActs.Contains(3) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            pyrrho?.Closed == true,
            pyrrhoConsistent,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && pyrrho is not null
                && AnselmUnfinishedProofFlow.IsRouteConsistent(saved, journey, pyrrho),
            canonical is not null);
        if (!AnselmUnfinishedProofTriggerPolicy.ShouldInsert(context)) return null;


        return canonical;
    }

    private static AnselmUnfinishedProof? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnselmUnfinishedProof>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnselmUnfinishedProof is unavailable: {exception}");
            return null;
        }
    }
}
