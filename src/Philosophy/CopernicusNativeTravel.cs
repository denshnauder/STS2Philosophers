using System.Runtime.CompilerServices;
using System.Globalization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// This person's saved table is a travel intention, not a second map or a claim
// that native travel has already completed. Native visited/history data owns arrival.
internal static class CopernicusNativeTravel
{
    private sealed class TravelRuntime { internal Task<bool>? Active; internal bool Queued; }
    private static readonly ConditionalWeakTable<RunState, TravelRuntime> Runtimes = new();
    private static string Seed(RunState run) => run.Rng.Seed.ToString(CultureInfo.InvariantCulture);

    public static bool TryCapture(RunState run, out CopernicusForkSnapshot? fork)
    {
        fork = null;
        if (run.CurrentActIndex != 1 || run.CurrentRoomCount != 1
            || run.BaseRoom is not CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss, Id: int sourceId } || run.CurrentMapCoord is not { } coord
            || run.CurrentMapPoint is not { } point || run.MapPointHistory.Count <= 1
            || !TryExits(run, point, out List<CopernicusForkExit> exits)) return false;
        var captured = new CopernicusForkSnapshot(Seed(run), run.CurrentActIndex,
            coord.row, coord.col, sourceId, run.VisitedMapCoords.Count,
            run.MapPointHistory[1].Count, exits);
        if (!captured.IsUsable()) return false;
        fork = captured;
        return true;
    }

    internal static bool TryCaptureQuestion(RunState run, out CopernicusForkSnapshot? fork)
    {
        fork = null;
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.CurrentActIndex != 1
            || run.CurrentMapCoord is not { } coord || run.MapPointHistory.Count <= 1
            || !TryExits(run, run.CurrentMapPoint!, out List<CopernicusForkExit> exits)) return false;
        var pending = new CopernicusForkSnapshot(Seed(run), 1, coord.row, coord.col, 0,
            run.VisitedMapCoords.Count, run.MapPointHistory[1].Count + 1, exits)
            { Source = CopernicusForkSource.QuestionPending };
        if (!pending.IsUsable()) return false;
        fork = pending;
        return true;
    }

    internal static bool MatchesPendingQuestion(RunState run, CopernicusForkSnapshot fork) =>
        fork.Source == CopernicusForkSource.QuestionPending && fork.IsUsable()
        && NativeQuestionNodeEvents.IsEntry(run) && Seed(run) == fork.RunSeed
        && run.CurrentActIndex == fork.ActIndex && run.CurrentMapCoord is { } coord
        && coord.row == fork.SourceRow && coord.col == fork.SourceColumn
        && run.VisitedMapCoords.Count == fork.VisitedCount && run.MapPointHistory.Count > fork.ActIndex
        && run.MapPointHistory[fork.ActIndex].Count + 1 == fork.HistoryCount
        && TryExits(run, run.CurrentMapPoint!, out var exits) && exits.SequenceEqual(fork.Exits);

    internal static bool TryBindQuestion(RunState run, CopernicusShiftedCenterState state)
    {
        if (state.Fork.Source != CopernicusForkSource.QuestionPending) return true;
        if (!state.IsUsable() || !MatchesQuestionLocation(run, state.Fork)
            || run.CurrentRoom is not EventRoom room) return false;
        state.Fork = state.Fork with { Source = CopernicusForkSource.QuestionEvent, SourceRoomId = room.Id ?? 0 };
        return state.IsUsable();
    }

    private static bool MatchesQuestionLocation(RunState run, CopernicusForkSnapshot fork) =>
        run.CurrentRoomCount == 1 && run.CurrentRoom is EventRoom { CanonicalEvent: CopernicusShiftedCenter } room
        && ReferenceEquals(run.BaseRoom, room) && run.CurrentMapPoint is { PointType: MapPointType.Unknown } point
        && run.CurrentMapCoord is { } coord && coord.row == fork.SourceRow && coord.col == fork.SourceColumn
        && run.CurrentActIndex == fork.ActIndex && Seed(run) == fork.RunSeed
        && run.VisitedMapCoords.Count == fork.VisitedCount && run.MapPointHistory.Count > fork.ActIndex
        && run.MapPointHistory[fork.ActIndex].Count == fork.HistoryCount
        && run.CurrentMapPointHistoryEntry is { } history
        && history.Rooms.Any(record => record.RoomType == RoomType.Event
            && Equals(record.ModelId, ModelDb.GetId<CopernicusShiftedCenter>()))
        && TryExits(run, point, out var exits) && exits.SequenceEqual(fork.Exits)
        && PhilosophyRunStateService.TryGet(run, out var shared)
        && shared?.CopernicusShiftedCenter is { } current && ReferenceEquals(current.Fork, fork)
        && current.InstanceId == $"COPERNICUS_SHIFTED_CENTER:{run.Rng.Seed}:2";

    private static bool TryExits(RunState run, MapPoint source, out List<CopernicusForkExit> exits)
    {
        exits = [];
        // Do not filter Children: native free-travel hooks can authorize other
        // points on the next row, and a third legal point invalidates this table.
        MapPoint[] legal = MapTravel.GetTravelablePointsFrom(run, source).ToArray();
        if (legal.Length != 2 || legal.Any(point => point.coord.row <= source.coord.row
                || point.PointType is MapPointType.Boss or MapPointType.Unassigned)
            || legal.Select(point => point.coord).Distinct().Count() != 2) return false;
        exits = legal.OrderBy(point => point.coord.col).ThenBy(point => point.coord.row)
            .Select(point => new CopernicusForkExit(point.coord.row, point.coord.col, point.PointType.ToString())).ToList();
        return true;
    }

    public static bool MatchesSource(RunState run, CopernicusForkSnapshot saved)
    {
        if (saved.Source != CopernicusForkSource.LegacyCombat)
            return saved.Source == CopernicusForkSource.QuestionEvent && saved.IsUsable()
                && MatchesQuestionLocation(run, saved);
        if (!saved.IsUsable() || run.CurrentActIndex != saved.ActIndex || Seed(run) != saved.RunSeed
            || run.CurrentMapCoord is not { } coord || coord.row != saved.SourceRow || coord.col != saved.SourceColumn
            || run.VisitedMapCoords.Count != saved.VisitedCount || run.MapPointHistory.Count <= saved.ActIndex
            || run.MapPointHistory[saved.ActIndex].Count != saved.HistoryCount
            || run.CurrentMapPoint is not { } source || !TryExits(run, source, out List<CopernicusForkExit> exits)
            || !exits.SequenceEqual(saved.Exits)) return false;
        if (run.CurrentRoomCount is 1 or 2 && run.BaseRoom is CombatRoom
                { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss } combat
            && combat.Id == saved.SourceRoomId)
            return run.CurrentRoomCount == 1 || run.CurrentRoom is EventRoom
                { CanonicalEvent: CopernicusShiftedCenter };
        // A native checkpoint restores the inserted event as its sole room. The
        // old combat object is gone; require this exact saved event/Fork instead.
        return run.CurrentRoomCount == 1 && run.CurrentRoom is EventRoom
                { CanonicalEvent: CopernicusShiftedCenter } room
            && ReferenceEquals(run.BaseRoom, room)
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.CopernicusShiftedCenter is { } state && ReferenceEquals(state.Fork, saved)
            && state.InstanceId == $"COPERNICUS_SHIFTED_CENTER:{run.Rng.Seed}:2";
    }

    public static bool HasArrived(RunState run, CopernicusShiftedCenterState state)
    {
        if (!state.IsUsable() || !state.TravelPending || state.SelectedExitIndex is < 0 or > 1) return false;
        CopernicusForkSnapshot fork = state.Fork;
        CopernicusForkExit target = fork.Exits[state.SelectedExitIndex];
        if (run.CurrentActIndex != fork.ActIndex || Seed(run) != fork.RunSeed
            || run.VisitedMapCoords.Count <= fork.VisitedCount || run.MapPointHistory.Count <= fork.ActIndex
            || run.MapPointHistory[fork.ActIndex].Count <= fork.HistoryCount) return false;
        MapCoord source = run.VisitedMapCoords[fork.VisitedCount - 1];
        MapCoord entered = run.VisitedMapCoords[fork.VisitedCount];
        var history = run.MapPointHistory[fork.ActIndex][fork.HistoryCount];
        if (source.row != fork.SourceRow || source.col != fork.SourceColumn
            || entered.row != target.Row || entered.col != target.Column
            || history.MapPointType.ToString() != target.PointType || history.Rooms.Count == 0) return false;
        if (run.VisitedMapCoords.Count > fork.VisitedCount + 1)
            return run.MapPointHistory[fork.ActIndex].Count > fork.HistoryCount + 1;
        // AddVisitedMapCoord runs before native room entry and before its save.
        // The coordinate alone cannot establish that the selected exit was entered.
        return run.CurrentMapCoord is { } current && current.row == target.Row && current.col == target.Column
            && run.ActFloor == target.Row + 1 && run.CurrentRoomCount == 1 && run.BaseRoom is { } room
            && ReferenceEquals(room, run.CurrentRoom)
            && room is not EventRoom { CanonicalEvent: CopernicusShiftedCenter }
            && history.Rooms.Any(record => record.RoomType == room.RoomType && Equals(record.ModelId, room.ModelId));
    }

    public static Task<bool> ResumeTravelAsync(RunState run, EventModel owner, CopernicusShiftedCenterState state)
    {
        TravelRuntime runtime = Runtimes.GetValue(run, static _ => new());
        lock (runtime)
        {
            if (runtime.Active is { IsCompleted: false } active) return active;
            return runtime.Active = ResumeCoreAsync(run, owner, state);
        }
    }

    // An option callback is itself held by EventSynchronizer. Native room exit
    // waits for those callbacks, so travel must begin after this callback returns.
    public static void QueueTravel(RunState run, EventModel owner, CopernicusShiftedCenterState state,
        Action<string> onBlocked)
    {
        TravelRuntime runtime = Runtimes.GetValue(run, static _ => new());
        lock (runtime)
        {
            if (runtime.Queued || runtime.Active is { IsCompleted: false }) return;
            runtime.Queued = true;
            Godot.Callable.From(() =>
            {
                lock (runtime) runtime.Active = RunQueuedAsync(run, owner, state, onBlocked, runtime);
            }).CallDeferred();
        }
    }

    private static bool StillOwnsEvent(RunState run, EventModel owner) =>
        RunManager.Instance.IsInProgress && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
        && run.CurrentRoom is EventRoom room && ReferenceEquals(room.LocalMutableEvent, owner);

    private static async Task<bool> RunQueuedAsync(RunState run, EventModel owner,
        CopernicusShiftedCenterState state, Action<string> onBlocked, TravelRuntime runtime)
    {
        void Report(string reason)
        {
            if (!StillOwnsEvent(run, owner)) return;
            RouteEventMapBoundary.Freeze(run, reason);
            try { onBlocked(reason); }
            catch (Exception exception) { Log.Error($"[STS2Philosophers] Copernicus recovery display failed: {exception}"); }
        }
        try
        {
            if (!StillOwnsEvent(run, owner)) return false;
            await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
            if (!StillOwnsEvent(run, owner)) return false;
            bool completed = await ResumeCoreAsync(run, owner, state);
            if (!completed) Report("Copernicus selected exit has not been confirmed by native travel.");
            return completed;
        }
        catch (Exception exception)
        {
            Report($"Copernicus deferred travel requires checkpoint recovery: {exception}");
            return false;
        }
        finally { lock (runtime) runtime.Queued = false; }
    }

    private static async Task<bool> ResumeCoreAsync(RunState run, EventModel owner, CopernicusShiftedCenterState state)
    {
        if (HasArrived(run, state)) return true;
        RunManager manager = RunManager.Instance;
        if (!manager.IsInProgress || !ReferenceEquals(manager.DebugOnlyGetState(), run)
            || !state.TravelPending || !state.IsUsable()
            || !RouteEventMapBoundary.IsActiveEvent(run, owner, 1)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.CopernicusShiftedCenter is not { } current || !ReferenceEquals(current, state)
            || shared.WesternJourney is not { } journey || shared.DemocritusArrangement is not { } democritus
            || shared.EmpedoclesRootsInquiry is not { } empedocles
            || !CopernicusShiftedCenterFlow.IsRouteConsistent(state, journey, democritus, empedocles,
                WesternRouteGraph.LoadEmbedded()) || !MatchesSource(run, state.Fork)) return false;
        try
        {
            // Proceed only opens the native map; it does not consume a map node.
            await NEventRoom.Proceed();
            NMapScreen? map = NMapScreen.Instance;
            if (map is null || !map.IsOpen || map.IsTraveling || !MatchesSource(run, state.Fork)) return false;
            map.SetTravelEnabled(false);
            CopernicusForkExit exit = state.Fork.Exits[state.SelectedExitIndex];
            await map.TravelToMapCoord(new MapCoord(exit.Column, exit.Row));
            // Native travel saves its target coordinate and our still-pending
            // intention before adding history/entering the target room. Reload
            // completes that native entry; never re-save this event into it.
            return HasArrived(run, state);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Copernicus travel requires native checkpoint recovery: {exception}");
            RouteEventMapBoundary.Freeze(run, "Copernicus native travel did not confirm its selected exit.");
            return false;
        }
    }
}
