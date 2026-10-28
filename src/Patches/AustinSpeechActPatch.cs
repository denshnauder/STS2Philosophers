using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AustinSpeechActPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.AustinSpeechAct is not { } saved || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || !AustinSpeechActFlow.IsRouteConsistent(saved, journey)) return null;
        AustinSpeechAct? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        AustinSpeechAct? canonical = TryGetCanonicalEvent();
        if (journey is null || canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey.RetainedEnding
            || journey.CurrentNodeId != AustinSpeechActState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_011")) return null;

        AustinSpeechActState? saved = shared.AustinSpeechAct;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? AustinSpeechActState.Create($"AUSTIN_SPEECH_ACT:{run.Rng.Seed}:2");
        if (!candidate.IsUsable()
            || !AustinSpeechActFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.AustinSpeechAct = candidate;

        Log.Info("[STS2Philosophers] Presenting Austin's speech act at the language node's handover station.");
        return canonical;
    }

    private static AustinSpeechAct? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AustinSpeechAct>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AustinSpeechAct is unavailable: {exception}");
            return null;
        }
    }
}
