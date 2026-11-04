using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KantKnowledgeSynthesisPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KantKnowledgeSynthesisState? saved = shared.KantKnowledgeSynthesis;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KantKnowledgeSynthesisFlow.IsRouteConsistent(saved, journey)) return null;
        KantKnowledgeSynthesis? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantKnowledgeSynthesisState? saved = shared.KantKnowledgeSynthesis;
        KantKnowledgeSynthesis? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id) || journey is null) return null;

        if (saved is not null)
            return !saved.Closed && saved.IsUsable()
                && KantKnowledgeSynthesisFlow.IsRouteConsistent(saved, journey) ? canonical : null;

        bool edgeEligible = WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == KantKnowledgeSynthesisState.CompletionEdgeId);
        if (!edgeEligible) return null;

        var candidate = KantKnowledgeSynthesisState.Create($"KANT_KNOWLEDGE_SYNTHESIS:{run.Rng.Seed}:3");
        if (!KantKnowledgeSynthesisFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KantKnowledgeSynthesis = candidate;
        Log.Info("[STS2Philosophers] Presenting Kant's synthesis of knowledge.");
        return canonical;
    }

    private static KantKnowledgeSynthesis? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KantKnowledgeSynthesis>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KantKnowledgeSynthesis is unavailable: {exception}");
            return null;
        }
    }
}
