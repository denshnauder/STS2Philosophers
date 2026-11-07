using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class CamusRevoltLimitPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
            || shared.CamusRevoltLimit is not { } saved || saved.Closed || !saved.IsUsable()) return null;
        if (shared.WesternJourney is not { } journey
            || !CamusRevoltLimitFlow.IsRouteConsistent(saved, journey)) return null;
        CamusRevoltLimit? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        return canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 2
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return null;

        WesternJourneyState? journey = shared.WesternJourney;
        CamusRevoltLimit? canonical = TryGetCanonicalEvent();
        if (journey is null || canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || journey.RetainedEnding) return null;
        bool afterNietzsche = journey.CurrentNodeId == CamusRevoltLimitState.NietzscheSourceNodeId;
        string hostNode = afterNietzsche
            ? CamusRevoltLimitState.NietzscheSourceNodeId : CamusRevoltLimitState.SourceNodeId;
        string hostEdge = afterNietzsche ? string.Empty : CamusRevoltLimitState.RequiredEdgeId;
        if (journey.CurrentNodeId != hostNode
            || journey.LastFixedAct != 3
            || hostEdge.Length > 0 && !journey.CompletedEdgeIds.Contains(hostEdge)
            || journey.ResolvedQuestionActs.Contains(3)) return null;

        CamusRevoltLimitState? saved = shared.CamusRevoltLimit;
        if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        var candidate = saved is not null && saved.HostNode == hostNode && saved.HostEdgeId == hostEdge
            ? saved
            : CamusRevoltLimitState.Create($"CAMUS_REVOLT_LIMIT:{run.Rng.Seed}:3");
        candidate.HostNode = hostNode;
        candidate.HostEdgeId = hostEdge;
        if (!candidate.IsUsable()
            || !CamusRevoltLimitFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.CamusRevoltLimit = candidate;

        Log.Info(afterNietzsche
            ? "[STS2Philosophers] Presenting Camus's limit of revolt after Nietzsche's third-act room."
            : "[STS2Philosophers] Presenting Camus's limit of revolt at Zeno's terminal node.");
        return canonical;
    }

    private static CamusRevoltLimit? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<CamusRevoltLimit>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] CamusRevoltLimit is unavailable: {exception}");
            return null;
        }
    }
}
