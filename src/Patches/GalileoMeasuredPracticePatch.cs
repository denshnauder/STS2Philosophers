using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class GalileoMeasuredPracticePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (run.CurrentActIndex != 2 || run.Players.Count != 1
            || !NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not HeraclitusLyre { CanOfferGalileo: true }
            || !LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Galileo, run.CurrentActIndex)
            || shared.WesternJourney is not { } journey || shared.DemocritusArrangement is not { } democritus
            || shared.EmpedoclesRootsInquiry is not { } empedocles) return null;
        string id = $"GALILEO_MEASURED_PRACTICE:{run.Rng.Seed}:3";
        var state = shared.GalileoMeasuredPractice;
        if (state is not null && (state.Closed || state.InstanceId != id)) return null;
        if (state is null && !NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true)) return null;
        var candidate = state ?? GalileoMeasuredPracticeState.Create(id, democritus.InstanceId, empedocles.InstanceId);
        if (!GalileoMeasuredPracticeFlow.IsRouteConsistent(candidate, journey, democritus, empedocles)) return null;
        GalileoMeasuredPractice canonical;
        try { canonical = ModelDb.Event<GalileoMeasuredPractice>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Galileo event unavailable: {exception}");
            return null;
        }
        if (run.VisitedEventIds.Contains(canonical.Id)) return null;
        shared.GalileoMeasuredPractice = candidate;
        return canonical;
    }
}
