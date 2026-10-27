using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HanFeiPublicStandardPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        HanFeiPublicStandard? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        HanFeiPublicStandardState? saved = philosophy.HanFeiPublicStandard;
        ActBehaviorState? behavior =
            philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        HanFeiPublicStandardTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.HanFei,
                run.CurrentActIndex),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!HanFeiPublicStandardTriggerPolicy.ShouldInsert(context))
        {
            return null;
        }

        var candidate = saved ?? HanFeiPublicStandardState.Create(
            $"HAN_FEI_PUBLIC_STANDARD:{run.Rng.Seed}:3");
        if (!candidate.IsUsable()) return null;
        philosophy.HanFeiPublicStandard = candidate;

        Log.Info("[STS2Philosophers] Presenting Han Fei's act-three public standard.");
        return canonical;
    }

    private static HanFeiPublicStandard? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<HanFeiPublicStandard>();
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[STS2Philosophers] HanFeiPublicStandard is unavailable: {exception}");
            return null;
        }
    }
}
