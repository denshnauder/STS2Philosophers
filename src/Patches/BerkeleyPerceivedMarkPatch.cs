using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class BerkeleyPerceivedMarkPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.BerkeleyPerceivedMark is not { } saved || saved.Closed || !saved.IsUsable()
            || !MatchesOpenHost(shared, saved)) return null;
        BerkeleyPerceivedMark? canonical = TryGetCanonicalEvent();
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
            || shared.LockeCargoIdentity is not { Closed: true, EdgeCommitted: true, ActIndex: 2 }
            || journey.CurrentNodeId != LockeCargoIdentityState.AdoptedNodeId
            || journey.LastFixedAct is not (2 or 3)
            || journey.ResolvedQuestionActs.Contains(2)) return null;
        BerkeleyPerceivedMark? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        BerkeleyPerceivedMarkState? saved = shared.BerkeleyPerceivedMark;
        if (saved is { Closed: true }
            || saved is not null && (!saved.IsUsable() || !MatchesOpenHost(shared, saved))) return null;
        // A genuinely completed nested Berkeley segment on the parent Locke record
        // means the encounter already happened; do not open a second independent
        // one. An empty, never-entered sub-record does not block the opportunity.
        var candidate = saved ?? BerkeleyPerceivedMarkState.CreateAt(
            $"BERKELEY_PERCEIVED_MARK:{run.Rng.Seed}:2", 2);
        if (!candidate.IsUsable()) return null;
        shared.BerkeleyPerceivedMark = candidate;
        Log.Info("[STS2Philosophers] Presenting Berkeley's independent second-act question after Locke.");
        return canonical;
    }

    private static bool MatchesOpenHost(PhilosophyRunState shared, BerkeleyPerceivedMarkState saved) =>
        saved.ActIndex == 2
        && shared.WesternJourney is { } journey
        && shared.LockeCargoIdentity is { Closed: true, EdgeCommitted: true, ActIndex: 2 }
        && journey.CurrentNodeId == LockeCargoIdentityState.AdoptedNodeId
        && journey.LastFixedAct is 2 or 3
        && !journey.ResolvedQuestionActs.Contains(2);

    private static BerkeleyPerceivedMark? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<BerkeleyPerceivedMark>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] BerkeleyPerceivedMark is unavailable: {exception}");
            return null;
        }
    }
}
