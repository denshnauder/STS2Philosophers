using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SextusOpposedAppearancesPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        SextusOpposedAppearances? canonical = TryGetCanonicalEvent();
        SextusOpposedAppearancesState? saved = philosophy.SextusOpposedAppearances;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var currentMapPoint = run.CurrentMapCoord is not null ? run.CurrentMapPoint : null;
        bool hasNaturalFork = currentMapPoint?.Children.Count >= 2;
        SextusOpposedAppearancesTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            hasNaturalFork,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!SextusOpposedAppearancesTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryReserveQuestion(journey, 1)) return;
            philosophy.SextusOpposedAppearances = SextusOpposedAppearancesState.Create(
                $"SEXTUS_OPPOSED_APPEARANCES:{run.Rng.Seed}:1");
        }

        Log.Info("[STS2Philosophers] Presenting Sextus's opposed appearances at a natural map fork.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static SextusOpposedAppearances? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SextusOpposedAppearances>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SextusOpposedAppearances is unavailable: {exception}");
            return null;
        }
    }
}
