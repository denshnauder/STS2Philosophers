using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SchopenhauerWillDenialPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        SchopenhauerWillDenialState? saved = shared.SchopenhauerWillDenial;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey || shared.KantApperception is not { } kant
            || !SchopenhauerWillDenialFlow.IsRouteConsistent(saved, journey, kant)) return null;
        SchopenhauerWillDenial? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1
            || run.CurrentActIndex != 2) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantApperceptionState? kant = shared.KantApperception;
        SchopenhauerWillDenialState? saved = shared.SchopenhauerWillDenial;
        SchopenhauerWillDenial? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey is null || kant is null || !kant.Closed || !kant.EdgeCommitted
            || !KantApperceptionFlow.IsRouteConsistent(kant, journey)
            || journey.CurrentNodeId != SchopenhauerWillDenialState.RequiredNodeId
            || journey.LastFixedAct != SchopenhauerWillDenialState.QuestionAct
            || journey.ResolvedQuestionActs.Contains(SchopenhauerWillDenialState.QuestionAct)
            || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(SchopenhauerWillDenialState.RequiredEdgeId)
            || saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;

        var candidate = saved ?? SchopenhauerWillDenialState.Create(
            $"SCHOPENHAUER_WILL_DENIAL:{run.Rng.Seed}:3", kant.InstanceId);
        if (!SchopenhauerWillDenialFlow.IsRouteConsistent(candidate, journey, kant)) return null;
        shared.SchopenhauerWillDenial = candidate;
        Log.Info("[STS2Philosophers] Presenting Schopenhauer's will-denial question at Kant's self node.");
        return canonical;
    }

    private static SchopenhauerWillDenial? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SchopenhauerWillDenial>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SchopenhauerWillDenial is unavailable: {exception}");
            return null;
        }
    }
}
