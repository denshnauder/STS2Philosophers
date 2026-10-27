using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaximenesBreathInvitationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy)
            || philosophy is null) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AnaximenesBreathInvitation? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        AnaximenesBreathInvitationState? saved = philosophy.AnaximenesBreathInvitation;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        AnaximenesBreathInvitationTriggerContext context = new(
            manager.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId,
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            player?.GetRelicById(ModelDb.GetId<AnaximenesBreath>()) is not null,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            canonical is not null);
        if (!AnaximenesBreathInvitationTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? AnaximenesBreathInvitationState.Create(
            $"ANAXIMENES_BREATH_INVITATION:{run.Rng.Seed}:3");
        if (!candidate.IsUsable()) return null;
        philosophy.AnaximenesBreathInvitation = candidate;
        Log.Info("[STS2Philosophers] Presenting Anaximenes breath practice at a native question node.");
        return canonical;
    }

    private static AnaximenesBreathInvitation? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnaximenesBreathInvitation>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnaximenesBreathInvitation is unavailable: {exception}");
            return null;
        }
    }
}
