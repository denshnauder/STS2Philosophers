using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

internal static class HanFeiPublicStandardPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null)
        {
            return;
        }

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
        HanFeiPublicStandard? canonical = TryGetCanonicalEvent();
        HanFeiPublicStandardState? saved = philosophy.HanFeiPublicStandard;
        ActBehaviorState? behavior =
            philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        HanFeiPublicStandardTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
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
            return;
        }

        if (saved is null)
        {
            philosophy.HanFeiPublicStandard = HanFeiPublicStandardState.Create(
                $"HAN_FEI_PUBLIC_STANDARD:{run.Rng.Seed}:3");
        }

        Log.Info("[STS2Philosophers] Presenting Han Fei's act-three public standard.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
