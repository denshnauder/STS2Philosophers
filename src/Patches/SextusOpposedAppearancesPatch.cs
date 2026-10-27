using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SextusOpposedAppearancesPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not SocratesQuestionCup { CanOfferNativeQuestion: true } carrier
            || philosophy.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        SextusOpposedAppearances? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        SextusOpposedAppearancesState? saved = philosophy.SextusOpposedAppearances;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (journey is null || journey.RetainedEnding || journey.LastFixedAct != 1
            || journey.CurrentNodeId != SextusOpposedAppearancesState.RequiredNodeId
            || !journey.ResolvedQuestionActs.Contains(1)) return null;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var currentMapPoint = run.CurrentMapCoord is not null ? run.CurrentMapPoint : null;
        bool hasNaturalFork = currentMapPoint?.Children.Count >= 2;
        SextusOpposedAppearancesTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            hasNaturalFork,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!SextusOpposedAppearancesTriggerPolicy.ShouldInsert(context)) return null;


        return canonical;
    }

    private static SextusOpposedAppearances? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SextusOpposedAppearances>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SextusOpposedAppearances is unavailable: {exception}");
            return null;
        }
    }
}
