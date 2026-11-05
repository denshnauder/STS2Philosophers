using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class NeowProceedPatch
{
    private static readonly object EntryGate = new();
    private static Task? _activeEntryTask;

    private static bool Prefix(ref Task __result)
    {
        lock (EntryGate)
        {
            if (_activeEntryTask is { IsCompleted: false } activeEntryTask)
            {
                __result = activeEntryTask;
                return false;
            }

            _activeEntryTask = null;

            RunManager runManager = RunManager.Instance;
            bool runInProgress = runManager.IsInProgress;
            RunState? runState = runInProgress
                ? runManager.DebugOnlyGetState()
                : null;
            if (runState is not null && !PhilosophyRunStateService.IsAvailable(runState)) return true;
            EventRoom? eventRoom = runState?.CurrentRoom as EventRoom;
            if (eventRoom?.LocalMutableEvent is PhilosophersGaze { IsFinished: true } gaze
                && !gaze.HasConfirmedCompletion)
            {
                __result = gaze.ConfirmCompletionAsync();
                return false;
            }
            CurrentEventKind currentEvent = eventRoom?.LocalMutableEvent switch
            {
                Neow => CurrentEventKind.Neow,
                AncientEventModel => CurrentEventKind.Ancient,
                PhilosophersGaze => CurrentEventKind.PhilosophersGaze,
                null => CurrentEventKind.None,
                _ => CurrentEventKind.Other,
            };

            PhilosophersGaze? canonicalEvent = TryGetCanonicalEvent();
            bool historyContainsEvent = canonicalEvent is not null
                && (runState?.CurrentMapPointHistoryEntry?.Rooms.Any(
                    room => room.ModelId == canonicalEvent.Id) ?? false);

            if (runState is not null && eventRoom is not null
                && currentEvent == CurrentEventKind.Ancient
                && eventRoom.LocalMutableEvent is { IsFinished: true }
                && ActFixedEntryHost.TryPrepare(runState, out EventRoom? fixedRoom) && fixedRoom is not null)
            {
                Task fixedEntryTask = EnterActFixedRoom(runManager, runState, eventRoom, fixedRoom);
                _activeEntryTask = fixedEntryTask;
                _ = fixedEntryTask.ContinueWith(
                    completedTask => ClearActiveEntryTask(completedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                __result = fixedEntryTask;
                return false;
            }

            PhilosophersGazeInterceptionContext context = new(
                runInProgress,
                eventRoom is not null,
                currentEvent,
                eventRoom?.LocalMutableEvent.IsFinished ?? false,
                historyContainsEvent,
                canonicalEvent is not null,
                runState?.Players.Count == 1);

            if (!PhilosophersGazeInterceptionPolicy.ShouldIntercept(context))
            {
                return true;
            }

            Task entryTask = EnterPhilosophersGaze(
                runManager,
                runState!,
                eventRoom!,
                canonicalEvent!);
            _activeEntryTask = entryTask;
            _ = entryTask.ContinueWith(
                completedTask => ClearActiveEntryTask(completedTask),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            __result = entryTask;
            return false;
        }
    }

    private static async Task EnterPhilosophersGaze(
        RunManager runManager,
        RunState runState,
        EventRoom neowRoom,
        PhilosophersGaze canonicalEvent)
    {
        neowRoom.MarkPreFinished();
        PhilosophyRunStateService.GetOrGenerateActOneCandidates(runState);
        EventRoom room = new(canonicalEvent);
        if (runManager.ShouldSave && (!PhilosophyRunStateService.TryGet(runState, out var shared) || shared is null
            || !await RouteEventMapBoundary.PersistEventAsync(runState, shared, room)))
        {
            RouteEventMapBoundary.Freeze(runState, "PhilosophersGaze entry checkpoint could not be confirmed.");
            return;
        }

        await runManager.EnterRoomWithoutExitingCurrentRoom(
            room,
            fadeToBlack: true);
    }

    private static async Task EnterActFixedRoom(
        RunManager runManager,
        RunState runState,
        EventRoom ancientRoom,
        EventRoom fixedRoom)
    {
        ancientRoom.MarkPreFinished();
        if (runManager.ShouldSave && (!PhilosophyRunStateService.TryGet(runState, out var shared) || shared is null
            || !await RouteEventMapBoundary.PersistEventAsync(runState, shared, fixedRoom)))
        {
            RouteEventMapBoundary.Freeze(runState, "The act's fixed event checkpoint could not be confirmed.");
            return;
        }

        await runManager.EnterRoomWithoutExitingCurrentRoom(
            fixedRoom,
            fadeToBlack: true);
    }

    private static PhilosophersGaze? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<PhilosophersGaze>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PhilosophersGaze is unavailable; allowing the original event proceed behavior: {exception}");
            return null;
        }
    }

    private static void ClearActiveEntryTask(Task completedTask)
    {
        lock (EntryGate)
        {
            if (ReferenceEquals(_activeEntryTask, completedTask))
            {
                _activeEntryTask = null;
            }
        }
    }
}
