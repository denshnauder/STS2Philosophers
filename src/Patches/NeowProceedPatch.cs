using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

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
            EventRoom? eventRoom = runState?.CurrentRoom as EventRoom;
            CurrentEventKind currentEvent = eventRoom?.LocalMutableEvent switch
            {
                Neow => CurrentEventKind.Neow,
                PhilosophersGaze => CurrentEventKind.PhilosophersGaze,
                null => CurrentEventKind.None,
                _ => CurrentEventKind.Other,
            };

            PhilosophersGaze? canonicalEvent = TryGetCanonicalEvent();
            bool historyContainsEvent = canonicalEvent is not null
                && (runState?.CurrentMapPointHistoryEntry?.Rooms.Any(
                    room => room.ModelId == canonicalEvent.Id) ?? false);

            PhilosophersGazeInterceptionContext context = new(
                runInProgress,
                eventRoom is not null,
                currentEvent,
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
        PhilosophersGaze canonicalEvent)
    {
        PhilosophyRunState philosophyState = PhilosophyRunStateService.GetOrCreate(runState);
        philosophyState.GeneratedCandidates.TryGetValue(
            PhilosophersGazeActOneCandidatePolicy.GenerationKey,
            out GeneratedCandidates? previousCandidates);
        GeneratedCandidates candidates = PhilosophyRunStateService.GetOrGenerateActOneCandidates(runState);
        if (!ReferenceEquals(previousCandidates, candidates) && runManager.ShouldSave)
        {
            await SaveManager.Instance.SaveRun(null);
        }

        await runManager.EnterRoomWithoutExitingCurrentRoom(
            new EventRoom(canonicalEvent),
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
