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
        bool fixedEntry = FixedEntryScope.Active;
        bool late = run.CurrentActIndex == 2;
        if ((!late && !fixedEntry && !NativeQuestionNodeEvents.IsEntry(run))
            || (late && !NativeQuestionNodeEvents.IsEntry(run))
            || run.Players.Count != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        AustinSpeechAct? canonical = TryGetCanonicalEvent();
        if (journey is null || canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey.RetainedEnding) return null;
        int actIndex = late ? 3 : 2;
        if (late)
        {
            if (journey.CurrentNodeId != AustinSpeechActState.LateSourceNodeId
                || journey.LastFixedAct != 3) return null;
        }
        else
        {
            if (journey.CurrentNodeId != AustinSpeechActState.SourceNodeId
                || journey.LastFixedAct != 2
                || !journey.ResolvedQuestionActs.Contains(2)
                || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
                || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")
                || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_011")) return null;
        }

        AustinSpeechActState? saved = shared.AustinSpeechAct;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? AustinSpeechActState.CreateAt(
            $"AUSTIN_SPEECH_ACT:{run.Rng.Seed}:{actIndex}", actIndex);
        if (saved is not null && saved.ActIndex != actIndex && saved.Outcome == AustinSpeechActOutcome.None
            && saved.ConfirmedActions.Count == 0)
        {
            candidate.ActIndex = actIndex;
        }
        if (!candidate.IsUsable()
            || !AustinSpeechActFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.AustinSpeechAct = candidate;

        Log.Info(late
            ? "[STS2Philosophers] Presenting Austin's speech act after Wittgenstein."
            : "[STS2Philosophers] Presenting Austin's speech act at the language node's handover station.");
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
