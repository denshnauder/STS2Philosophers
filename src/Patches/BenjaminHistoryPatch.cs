using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class BenjaminHistoryPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.BenjaminHistory is not { } saved || saved.Closed || !saved.IsUsable()
            || !MatchesOpenHost(shared, saved)) return null;
        BenjaminHistory? canonical = TryGetCanonicalEvent();
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
            || shared.FoucaultPower is not { Closed: true, EdgeCommitted: true, ActIndex: 2 }
            || journey.CurrentNodeId != FoucaultPowerState.AdoptedNodeId
            || journey.LastFixedAct != 3
            || journey.ResolvedQuestionActs.Contains(2)) return null;
        BenjaminHistory? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        BenjaminHistoryState? saved = shared.BenjaminHistory;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved ?? BenjaminHistoryState.CreateAt(
            $"BENJAMIN_HISTORY:{run.Rng.Seed}:2", 2);
        if (!candidate.IsUsable()) return null;
        shared.BenjaminHistory = candidate;
        Log.Info("[STS2Philosophers] Presenting Benjamin's independent second-act perspective after Foucault.");
        return canonical;
    }

    private static bool MatchesOpenHost(PhilosophyRunState shared, BenjaminHistoryState saved) =>
        saved.ActIndex == 2
        && shared.WesternJourney is { } journey
        && shared.FoucaultPower is { Closed: true, EdgeCommitted: true, ActIndex: 2 }
        && journey.CurrentNodeId == FoucaultPowerState.AdoptedNodeId
        && journey.LastFixedAct == 3
        && !journey.ResolvedQuestionActs.Contains(2);

    private static BenjaminHistory? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<BenjaminHistory>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] BenjaminHistory is unavailable: {exception}");
            return null;
        }
    }
}
