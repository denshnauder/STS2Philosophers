using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

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
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("DemocritusArrangement cannot restore its saved event state.");
            return;
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
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("DemocritusArrangement run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("DemocritusArrangement saved event state is unavailable.");
                return;
            }
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
        // Reject an obsolete button before acquiring a gate or touching native run state.
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("DemocritusArrangement run state is unavailable.");
                return;
            }
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

    internal static Task<bool> PersistBoundaryAsync(
        RunState run, PhilosophyRunState shared, EventRoom? expectedRoom = null) =>
        RouteEventMapBoundary.PersistEventAsync(run, shared, expectedRoom);

    private static bool MatchesSavedRoom(SerializableRun save, EventRoom expectedRoom) =>
        RouteEventMapBoundary.MatchesSavedRoom(save, expectedRoom);

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

    private DemocritusArrangementState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.DemocritusArrangement is { } state && state.IsUsable() && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2") return state;
        return null;
    }

    private void Block(string reason)
    {
        _ready = false;
        _blocked = true;
        if (Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            if (!IsFinished) SetEventState(Description ?? InitialDescription, LockedOptions());
            RouteEventMapBoundary.Freeze(run, reason);
        }
        else Log.Error($"[STS2Philosophers] {reason}");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(DemocritusArrangementStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{DemocritusArrangementFlow.StageKey(stage)}.description");

}
