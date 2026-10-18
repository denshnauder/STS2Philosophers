using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

public sealed class DemocritusArrangement : EventModel
{
    private const string LocalizationRoot = "DEMOCRITUS_ARRANGEMENT";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(DemocritusArrangementStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    // Empty native options finish the event. Locked options keep it open while
    // the native awaited AfterEventStarted hook confirms the saved transaction.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        DemocritusArrangementState state = RestoreStateOrThrow();
        if (isPreFinished && !state.Closed)
        {
            throw new InvalidDataException("An open Democritus arrangement cannot be pre-finished.");
        }
        SetEventState(PageDescription(state.Stage), LockedOptions());
    }

    public override Task AfterEventStarted() => ResumeAfterEntryAsync();

    private async Task ResumeAfterEntryAsync()
    {
        if (_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
            DemocritusArrangementState state = RestoreStateOrThrow();
            if (!IsJourneyConsistent(shared))
            {
                Block("The saved Democritus arrangement conflicts with its route.");
                return;
            }

            if (!await PersistBoundaryAsync(run, shared))
            {
                Block("Democritus entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != DemocritusArrangementAction.None)
            {
                if (!CommitPending(shared, state) || !await PersistBoundaryAsync(run, shared))
                {
                    Block("The saved Democritus action remains blocked for recovery.");
                    return;
                }
            }
            if (!IsActiveRoom(run)) return;
            ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"Democritus entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(
        DemocritusArrangementState expectedState,
        string expectedInstanceId,
        DemocritusArrangementStage expectedStage,
        int expectedActionCount,
        DemocritusArrangementAction action)
    {
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
            if (shared.DemocritusArrangement is not { } state
                || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != DemocritusArrangementAction.None || state.Closed) return;
            if (!IsJourneyConsistent(shared))
            {
                Block("The Democritus choice no longer matches its saved route.");
                return;
            }
            if (!DemocritusArrangementFlow.TryPrepare(state, action)) return;

            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await PersistBoundaryAsync(run, shared))
            {
                Block($"Democritus prepared action persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"Democritus action could not commit: {action}.");
                return;
            }
            if (!await PersistBoundaryAsync(run, shared))
            {
                Block($"Democritus committed action persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"Democritus action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, DemocritusArrangementState state) =>
        shared.WesternJourney is { } journey
        && ReferenceEquals(shared.DemocritusArrangement, state)
        && DemocritusArrangementFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    internal static bool IsJourneyConsistent(PhilosophyRunState shared) =>
        shared.DemocritusArrangement is { } state && shared.WesternJourney is { } journey
        && DemocritusArrangementFlow.IsJourneyConsistent(state, journey, WesternRouteGraph.LoadEmbedded());

    internal static async Task<bool> PersistBoundaryAsync(
        RunState run, PhilosophyRunState shared, EventRoom? expectedRoom = null)
    {
        if (!IsCurrentRun(run, shared) || !PhilosophyRunStateRitsuStore.TryBind(run, shared)
            || !PhilosophyRunStateRitsuStore.IsAuthoritative(run, shared)) return false;
        EventRoom? room = expectedRoom ?? run.CurrentRoom as EventRoom;
        if (room?.CanonicalEvent is not DemocritusArrangement) return false;
        string expected = PhilosophyRunStateCodec.Encode(shared);
        // Native SaveRun(null) omits the event and would reload the previous map
        // coordinate's room. Persist this exact event so a reload resumes it as
        // the single native base room, while live insertion remains a map overlay.
        await SaveManager.Instance.SaveRun(room);
        if (!IsCurrentRun(run, shared)
            || PhilosophyRunStateCodec.Encode(shared) != expected) return false;

        SaveManager manager = SaveManager.Instance;
        RunSaveManager runSaveManager = SaveManagerAccess.GetRunSaveManager(manager);
        ISaveStore store = SaveManagerAccess.GetSaveStore(manager);
        string? json = store.ReadFile(SaveManagerAccess.GetCurrentRunSavePath(runSaveManager));
        if (json is null) return false;
        PhilosophyRunStateRitsuSaveReadResult read = PhilosophyRunStateRitsuSaveReader.Read(json);
        if (read.Classification != PhilosophyRunStateRitsuSaveClassification.Current
            || !string.Equals(read.EncodedState, expected, StringComparison.Ordinal)) return false;
        ReadSaveResult<SerializableRun> nativeRead = manager.LoadRunSave();
        return nativeRead.Status == ReadSaveStatus.Success && nativeRead.SaveData is { } nativeSave
            && MatchesSavedRoom(nativeSave, room);
    }

    private static bool MatchesSavedRoom(SerializableRun save, EventRoom expectedRoom) =>
        save.PreFinishedRoom is { } saved && saved.RoomType == RoomType.Event
        && Equals(saved.EventId, expectedRoom.CanonicalEvent.Id)
        && saved.IsPreFinished == expectedRoom.IsPreFinished;

    private static bool IsCurrentRun(RunState run, PhilosophyRunState shared) =>
        RunManager.Instance.IsInProgress && RunManager.Instance.ShouldSave
        && RunManager.Instance.NetService.Type == NetGameType.Singleplayer
        && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
        && PhilosophyRunStateService.TryGet(run, out var current) && ReferenceEquals(current, shared);

    private bool IsActiveRoom(RunState run) =>
        RunManager.Instance.IsInProgress && !run.IsGameOver && run.Players.Count == 1
        && run.CurrentActIndex == 1
        && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
        && run.CurrentRoom is EventRoom room && ReferenceEquals(room.LocalMutableEvent, this)
        && (run.CurrentRoomCount == 2 && run.BaseRoom is not null
            || run.CurrentRoomCount == 1 && ReferenceEquals(run.BaseRoom, room));

    private void ShowConfirmedState(DemocritusArrangementState state)
    {
        if (state.Closed)
        {
            _ready = false;
            SetEventFinished(PageDescription(state.Stage));
            return;
        }
        _ready = true;
        SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, $"{LocalizationRoot}.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(DemocritusArrangementState state)
    {
        string instanceId = state.InstanceId;
        DemocritusArrangementStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return DemocritusArrangementFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{DemocritusArrangementFlow.StageKey(stage)}.options.{DemocritusArrangementFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private DemocritusArrangementState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).DemocritusArrangement is { } state
            && state.IsUsable() && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2") return state;
        throw new InvalidDataException("DemocritusArrangement requires its valid saved instance.");
    }

    private void Block(string message)
    {
        _ready = false;
        _blocked = true;
        if (!IsFinished && Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            SetEventState(Description ?? InitialDescription, LockedOptions());
        }
        Log.Error($"[STS2Philosophers] {message} Choices remain frozen until reload.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(DemocritusArrangementStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{DemocritusArrangementFlow.StageKey(stage)}.description");

    private static class SaveManagerAccess
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_runSaveManager")]
        internal static extern ref RunSaveManager GetRunSaveManager(SaveManager saveManager);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_saveStore")]
        internal static extern ref ISaveStore GetSaveStore(SaveManager saveManager);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CurrentRunSavePath")]
        internal static extern string GetCurrentRunSavePath(RunSaveManager runSaveManager);
    }
}
