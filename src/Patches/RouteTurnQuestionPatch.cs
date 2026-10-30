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
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (run.CurrentActIndex != 0) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        if (shared.WesternJourney is not { } journey || journey.LastFixedAct != 1) return null;
        if (journey.RetainedEnding || journey.ResolvedQuestionActs.Contains(1)) return null;
        if (WesternQuestionNodePolicy.HasOpenQuestion(shared)) return null;
        if (journey.CurrentNodeId != journey.EntryNodeId) return null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        if (!RouteTurnQuestionOptions.HasTurn(RouteTurnQuestionOptions.For(journey, graph))) return null;
        RouteTurnQuestion? canonical;
        try { canonical = ModelDb.Event<RouteTurnQuestion>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] RouteTurnQuestion is unavailable: {exception}");
            return null;
        }
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }
}
