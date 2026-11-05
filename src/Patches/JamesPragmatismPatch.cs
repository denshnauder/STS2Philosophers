using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class JamesPragmatismPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.JamesPragmatism is not { } saved || saved.Closed || !saved.IsUsable()
            || !MatchesOpenHost(shared, saved)) return null;
        JamesPragmatism? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1
            || run.CurrentActIndex != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;
        if (shared.WesternJourney is not { } journey
            || shared.PeirceInquiry is not { Closed: true, EdgeCommitted: true } peirce
            || journey.CurrentNodeId != PeirceInquiryState.AdoptedNodeId
            || journey.LastFixedAct != 3
            || journey.ResolvedQuestionActs.Contains(2)) return null;
        JamesPragmatism? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        JamesPragmatismState? saved = shared.JamesPragmatism;
        if (saved is { Closed: true }
            || saved is not null && (!saved.IsUsable() || !MatchesOpenHost(shared, saved))) return null;
        var candidate = saved ?? JamesPragmatismState.CreateAt(
            $"JAMES_PRAGMATISM:{run.Rng.Seed}:2", 2);
        if (!candidate.IsUsable()) return null;
        shared.JamesPragmatism = candidate;
        Log.Info("[STS2Philosophers] Presenting James's independent second-act question after Peirce.");
        return canonical;
    }

    private static bool MatchesOpenHost(PhilosophyRunState shared, JamesPragmatismState saved) =>
        saved.ActIndex == 2
        && shared.WesternJourney is { } journey
        && shared.PeirceInquiry is { Closed: true, EdgeCommitted: true, ActIndex: 2 }
        && journey.CurrentNodeId == PeirceInquiryState.AdoptedNodeId
        && journey.LastFixedAct == 3
        && !journey.ResolvedQuestionActs.Contains(2);

    private static JamesPragmatism? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<JamesPragmatism>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] JamesPragmatism is unavailable: {exception}");
            return null;
        }
    }
}
