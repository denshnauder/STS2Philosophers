using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// The first act's question at the entry: it offers the lines this run could turn
/// onto, including the two the shared Husserl figure has to ask a direction
/// about. It never takes the act's fixed slot by itself; answering a question is
/// what a question is for.
/// </summary>
internal static class RouteTurnQuestionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    /// <summary>
    /// A fork a run left open is resumed before any new room may open: the node it
    /// was asked on is the only place the answer can be given.
    /// </summary>
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        // The question belongs to the first act. A record left open there cannot
        // bring the fork back later: the run has left the node it was asked on.
        if (run.CurrentActIndex != 0) return null;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        RouteTurnQuestionState? saved = shared.RouteTurnQuestion;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        RouteTurnQuestion? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (run.CurrentActIndex != 0) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        if (shared.WesternJourney is not { } journey || journey.LastFixedAct != 1) return null;
        if (journey.RetainedEnding || journey.ResolvedQuestionActs.Contains(1)) return null;
        if (WesternQuestionNodePolicy.HasOpenQuestion(shared)) return null;
        if (journey.CurrentNodeId != journey.EntryNodeId) return null;
        // The fork is asked once per run: an answered record means the run already
        // chose, and staying is an answer too.
        if (shared.RouteTurnQuestion is { } previous) return null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        if (!RouteTurnQuestionOptions.HasTurn(RouteTurnQuestionOptions.For(journey, graph))) return null;
        RouteTurnQuestion? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        shared.RouteTurnQuestion = RouteTurnQuestionState.Create(
            $"{RouteTurnQuestionOptions.InstanceIdPrefix}:{run.Rng.Seed}:{run.CurrentActIndex}");
        Log.Info("[STS2Philosophers] Presenting RouteTurnQuestion room.");
        return canonical;
    }

    private static RouteTurnQuestion? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<RouteTurnQuestion>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] RouteTurnQuestion is unavailable: {exception}");
            return null;
        }
    }
}
