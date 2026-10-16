using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch]
internal static class DemocritusArrangementPatch
{
    private static readonly ConditionalWeakTable<RunState, InsertionGuard> Guards = new();

    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)])
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "EnterRoomInternal(AbstractRoom, bool)");

    private static void Postfix(AbstractRoom room, bool isRestoringRoomStackBase, ref Task __result)
    {
        if (room is MapRoom mapRoom && !isRestoringRoomStackBase)
        {
            __result = InsertAfterMapEntryAsync(__result, mapRoom);
        }
    }

    internal static async Task InsertAfterMapEntryAsync(
        Task nativeEntry, MapRoom? expectedMap = null, bool savedInstanceOnly = false)
    {
        await nativeEntry;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentRoom is not MapRoom mapRoom
            || expectedMap is not null && !ReferenceEquals(expectedMap, mapRoom)) return;

        InsertionGuard guard = Guards.GetValue(run, static _ => new());
        if (guard.Blocked || Interlocked.Exchange(ref guard.Inserting, 1) != 0) return;
        try
        {
            if (manager.NetService.Type != NetGameType.Singleplayer || !IsSafeMap(run, mapRoom)) return;
            PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
            WesternJourneyState? journey = shared.WesternJourney;
            DemocritusArrangementState? saved = shared.DemocritusArrangement;
            if (savedInstanceOnly && saved is null) return;
            DemocritusArrangement? canonical = TryGetCanonicalEvent();
            ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
            var player = run.Players.Count == 1 ? run.Players[0] : null;
            DemocritusArrangementTriggerContext context = new(
                manager.IsInProgress,
                run.CurrentRoom is MapRoom,
                run.Players.Count == 1,
                run.CurrentActIndex,
                behavior?.CompletedCombatCount ?? 0,
                journey?.CurrentNodeId,
                journey?.LastFixedAct ?? 0,
                LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Democritus, run.CurrentActIndex),
                journey?.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId) == true,
                journey?.RetainedEnding == true,
                IsSafeMap(run, mapRoom),
                saved is not null,
                saved?.IsUsable() == true,
                saved?.Closed == true,
                canonical is not null);
            if (!DemocritusArrangementTriggerPolicy.ShouldInsert(context)) return;

            NMapScreen.Instance?.SetTravelEnabled(false);
            if (saved is null)
            {
                shared.DemocritusArrangement = DemocritusArrangementState.Create(
                    $"DEMOCRITUS_ARRANGEMENT:{run.Rng.Seed}:2");
            }
            // The canonical model must already be bound before the first native
            // checkpoint serializes this room; it may be reloaded before entry.
            EventRoom eventRoom = new(canonical!);
            if (!DemocritusArrangement.IsJourneyConsistent(shared)
                || !await DemocritusArrangement.PersistBoundaryAsync(run, shared, eventRoom))
            {
                guard.Blocked = true;
                Log.Error("[STS2Philosophers] Democritus entry persistence or route is unknown; map travel remains frozen until reload.");
                return;
            }
            if (!manager.IsInProgress || !ReferenceEquals(manager.DebugOnlyGetState(), run)
                || !IsSafeMap(run, mapRoom)) return;

            Log.Info("[STS2Philosophers] Presenting Democritus arrangement after combat.");
            NMapScreen.Instance?.Close(animateOut: false);
            await manager.EnterRoomWithoutExitingCurrentRoom(eventRoom, fadeToBlack: false);
        }
        catch (Exception exception)
        {
            guard.Blocked = true;
            NMapScreen.Instance?.SetTravelEnabled(false);
            Log.Error($"[STS2Philosophers] Democritus entry stays frozen for recovery: {exception}");
        }
        finally
        {
            Volatile.Write(ref guard.Inserting, 0);
        }
    }

    private static bool IsSafeMap(RunState run, MapRoom mapRoom) =>
        !run.IsGameOver && run.CurrentRoomCount == 1
        && ReferenceEquals(run.CurrentRoom, mapRoom) && ReferenceEquals(run.BaseRoom, mapRoom);

    private static DemocritusArrangement? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DemocritusArrangement>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DemocritusArrangement is unavailable: {exception}");
            return null;
        }
    }

    private sealed class InsertionGuard
    {
        internal int Inserting;
        internal bool Blocked;
    }
}

// A missing saved overlay may be recovered only after the whole native restore;
// inserting while its base MapRoom restores would duplicate an existing overlay.
[HarmonyPatch]
internal static class DemocritusArrangementLoadedMapPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), "LoadIntoLatestMapCoord", [typeof(AbstractRoom)])
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "LoadIntoLatestMapCoord(AbstractRoom)");

    private static void Postfix(ref Task __result) =>
        __result = DemocritusArrangementPatch.InsertAfterMapEntryAsync(__result, savedInstanceOnly: true);
}
