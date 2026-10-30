using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ProtagorasSharedDeliberationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<SocratesBalanceWeight>()) is not SocratesBalanceWeight { CanOfferNativeQuestion: true } carrier
            || philosophy.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        ProtagorasSharedDeliberation? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        ProtagorasSharedDeliberationState? saved = philosophy.ProtagorasSharedDeliberation;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (journey is null || journey.RetainedEnding || journey.LastFixedAct != 1
            || journey.CurrentNodeId != ProtagorasSharedDeliberationState.SourceNodeId
            || journey.ResolvedQuestionActs.Contains(1) != saved.QuestionReserved
            || journey.CompletedEdgeIds.Contains(ProtagorasSharedDeliberationState.CompletionEdgeId) != saved.EdgeCommitted)
            return null;
        ProtagorasSharedDeliberationTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.CompletedEdgeIds.Contains(ProtagorasSharedDeliberationState.CompletionEdgeId) == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(context))
        {
            return null;
        }


        return canonical;
    }

    private static ProtagorasSharedDeliberation? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<ProtagorasSharedDeliberation>();
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[STS2Philosophers] ProtagorasSharedDeliberation is unavailable; leaving the map unchanged: {exception}");
            return null;
        }
    }
}
