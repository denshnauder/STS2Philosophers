using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal enum ZenoRouteNativeTriggerStatus
{
    NotHandled,
    Entered,
    Blocked,
}

internal static class ZenoRouteNativeTriggerRegistry
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<RunState, ZenoRouteNativeTriggerSession> Sessions = new();

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.CurrentActIndex != 2
            || run.Players[0].GetRelicById(ModelDb.GetId<SocratesBalanceWeight>()) is not SocratesBalanceWeight
            || !PhilosophyRunStateService.TryGet(run, out var shared) || shared is null
            || shared.DiogenesStaySwitchEntry is not { Closed: true }
            || shared.ZenoRoutePayload.State is not { PendingOperation: null, Route.Stage: ZenoRouteStage.ReadyToClaim } state
            || !ZenoRouteStateCodec.IsValidFeature(state, shared.ZenoRouteValidationCatalog)
            || !TryGet(RunManager.Instance, out var session) || session is null || !session.CanSelectNative) return null;
        try
        {
            var canonical = ModelDb.Event<ZenoAssentBoundary>();
            return run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
        }
        catch { return null; }
    }

    internal static string NativeDestinationId(RunState run)
    {
        if (run.CurrentMapCoord is not { } coord) throw new InvalidOperationException("Missing native question coordinate.");
        return FormattableString.Invariant($"NATIVE_QUESTION_{run.Rng.Seed}_ACT_{run.CurrentActIndex}_ROW_{coord.row}_COL_{coord.col}");
    }

    internal static bool IsNativeDestination(string? id) => id?.StartsWith("NATIVE_QUESTION_", StringComparison.Ordinal) == true;

    internal static bool MatchesNativeDestination(RunState run, string? id)
    {
        if (id is null || run.CurrentMapCoord is null || run.CurrentActIndex != 2
            || run.CurrentMapPoint is not { PointType: MapPointType.Unknown }) return false;
        string[] parts = id.Split('_');
        return parts.Length == 9 && parts[0] == "NATIVE" && parts[1] == "QUESTION"
            && ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) && seed == run.Rng.Seed
            && parts[3] == "ACT" && parts[4] == "2" && parts[5] == "ROW" && parts[7] == "COL"
            && int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out int row) && row >= 0
            && int.TryParse(parts[8], NumberStyles.None, CultureInfo.InvariantCulture, out int col) && col >= 0
            && id == NativeDestinationId(run);
    }

    // Native exit awaits option tasks; never await it from that same option.
    internal static void QueueLegacyExit(RunState run, EventModel owner, Func<Task> resume, Action? completed = null)
    {
        Godot.Callable.From(() => { _ = DrainAndResumeAsync(); }).CallDeferred();
        async Task DrainAndResumeAsync()
        {
            try
            {
                await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
                if (!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
                    || run.CurrentRoom is not EventRoom room || !ReferenceEquals(room.LocalMutableEvent, owner)) return;
                await resume();
            }
            catch (Exception error) { RouteEventMapBoundary.Freeze(run, $"Legacy Stoic exit requires recovery: {error}"); }
            finally { completed?.Invoke(); }
        }
    }

    internal static async Task ResumePreviousRoomAsync(RunState run, EventModel owner)
    {
        if (run.CurrentRoomCount != 2 || run.CurrentRoom is not EventRoom room || !ReferenceEquals(room.LocalMutableEvent, owner))
            throw new InvalidOperationException("The old Stoic room stack changed before exit.");
        var method = AccessTools.DeclaredMethod(typeof(RunManager), "ResumePreviousRoom", Type.EmptyTypes)
            ?? throw new MissingMethodException("RunManager.ResumePreviousRoom()");
        await (Task)method.Invoke(RunManager.Instance, null)!;
        if (run.CurrentRoomCount != 1) throw new InvalidOperationException("The old Stoic base room was not restored.");
    }

    public static bool TryGet(
        RunManager runManager,
        out ZenoRouteNativeTriggerSession? session)
    {
        session = null;
        if (!runManager.IsInProgress || runManager.NetService?.Type != NetGameType.Singleplayer ||
            runManager.DebugOnlyGetState() is not { Players.Count: 1 } runState ||
            !PhilosophyRunStateService.TryGet(runState, out PhilosophyRunState? sharedState) ||
            sharedState?.ZenoRoutePayload is not
            {
                Classification: ZenoRoutePayloadClassification.Current,
                State.Route: not null,
            })
        {
            return false;
        }

        ZenoRouteValidationCatalog catalog = sharedState.ZenoRouteValidationCatalog;
        if (!ZenoRouteRuntimeService.TryGetOrCreate(runState, catalog, out ZenoRoutePersistenceRuntime? runtime) ||
            runtime is null)
        {
            return false;
        }

        lock (Gate)
        {
            if (Sessions.TryGetValue(runState, out ZenoRouteNativeTriggerSession? existing))
            {
                if (!existing.Owns(runtime, catalog))
                {
                    return false;
                }

                session = existing;
                return true;
            }

            session = new ZenoRouteNativeTriggerSession(
                runManager,
                runState,
                runtime,
                catalog);
            Sessions.Add(runState, session);
            return true;
        }
    }
}

internal sealed class ZenoRouteNativeTriggerSession : IZenoRouteRoomDestinationReader
{
    private readonly object _taskGate = new();
    private readonly RunManager _runManager;
    private readonly RunState _runState;
    private readonly ZenoRoutePersistenceRuntime _runtime;
    private readonly ZenoRouteValidationCatalog _catalog;
    private readonly ZenoRouteSchedulingRuntime _scheduler;
    private readonly ZenoAssentBoundaryFactory _factory;
    private readonly ZenoAssentBoundaryRoomEntryAdapter _roomEntry;
    private Task<ZenoRouteNativeTriggerStatus>? _activeTask;
    private Task<bool>? _closeTask;
    private AbstractRoom? _destinationRoom;
    private ZenoRouteObservedDestination? _destination;
    private bool _legacyCloseQueued;

    internal bool CanResumeLegacyClaim => (_runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route) is
        { Stage: ZenoRouteStage.OpeningClaimed or ZenoRouteStage.EventActive, ResumeDestinationId: { } destination }
        && !ZenoRouteNativeTriggerRegistry.IsNativeDestination(destination);

    internal bool CanSelectNative => _runtime.CurrentState is { PendingOperation: null, Route.Stage: ZenoRouteStage.ReadyToClaim }
        && _runtime.PendingRequestId is null;

    internal bool NeedsIntervalObservation => _runtime.CurrentState.Route?.Stage == ZenoRouteStage.WaitingInterval;
    internal bool HasIntervalRecovery => _runtime.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.IntervalCompleted
        || _runtime.CurrentState.Route?.Stage == ZenoRouteStage.ReadyToClaim && _runtime.PendingRequestId is not null;

    internal async Task<bool> ObserveIntervalAsync(string? receipt)
    {
        if (HasIntervalRecovery)
            return (await _runtime.RetryAsync()).Status == ZenoRoutePersistenceCoordinationStatus.Completed;
        if (receipt is null || _runtime.CurrentState is not { PendingOperation: null, Route.Stage: ZenoRouteStage.WaitingInterval }) return true;
        return (await _scheduler.ExecuteAsync(new ZenoRouteSchedulingContext(CompletedRoomReceiptId: receipt))).Status
            == ZenoRouteSchedulingExecutionStatus.Completed;
    }

    public ZenoRouteNativeTriggerSession(
        RunManager runManager,
        RunState runState,
        ZenoRoutePersistenceRuntime runtime,
        ZenoRouteValidationCatalog catalog)
    {
        _runManager = runManager;
        _runState = runState;
        _runtime = runtime;
        _catalog = catalog;
        _scheduler = new ZenoRouteSchedulingRuntime(runtime, catalog);
        _factory = new ZenoAssentBoundaryFactory(runtime, catalog);
        _roomEntry = new ZenoAssentBoundaryRoomEntryAdapter(
            runManager,
            runState,
            runtime,
            catalog,
            this,
            CloseAndResumeAsync);
    }

    public bool CanIntercept
    {
        get
        {
            ZenoRouteState? route = _runtime.CurrentState.PendingOperation?.Candidate ??
                                    _runtime.CurrentState.Route;
            return route?.Stage is
                ZenoRouteStage.WaitingInterval or
                ZenoRouteStage.ReadyToClaim or
                ZenoRouteStage.OpeningClaimed or
                ZenoRouteStage.EventActive;
        }
    }

    internal bool HasPendingClose =>
        _runtime.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed;

    internal string? EffectiveEventInstanceId =>
        (_runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route)
            ?.EventInstanceId;

    public bool Owns(
        ZenoRoutePersistenceRuntime runtime,
        ZenoRouteValidationCatalog catalog) =>
        ReferenceEquals(_runtime, runtime) && ReferenceEquals(_catalog, catalog);

    public Task<ZenoRouteNativeTriggerStatus> TriggerAsync(
        ZenoRouteNativeTriggerPlan plan,
        AbstractRoom destinationRoom,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(destinationRoom);

        lock (_taskGate)
        {
            if (_activeTask is { IsCompleted: false } active)
            {
                return active;
            }

            Task<ZenoRouteNativeTriggerStatus> task = _activeTask = TriggerCoreAsync(plan, destinationRoom, cancellationToken);
            _ = task.ContinueWith(
                completed => ClearActiveTask(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    public ZenoRouteObservedDestination? ReadDestination(AbstractRoom room)
    {
        lock (_taskGate)
        {
            return ReferenceEquals(room, _destinationRoom) ? _destination : null;
        }
    }

    internal Task<bool> CloseAndResumeAsync(
        string eventInstanceId,
        CancellationToken cancellationToken = default)
    {
        lock (_taskGate)
        {
            ZenoRouteState? route = _runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route;
            if (route?.ResumeDestinationId is { } destination && !ZenoRouteNativeTriggerRegistry.IsNativeDestination(destination)
                && _runState.CurrentRoom is EventRoom { LocalMutableEvent: ZenoAssentBoundary } legacyRoom)
            {
                if (!_legacyCloseQueued)
                {
                    _legacyCloseQueued = true;
                    ZenoRouteNativeTriggerRegistry.QueueLegacyExit(_runState, legacyRoom.LocalMutableEvent, async () =>
                    {
                        try
                        {
                            if (!await CloseAndResumeCoreAsync(eventInstanceId, cancellationToken))
                                RouteEventMapBoundary.Freeze(_runState, "The original Zeno destination has not been confirmed.");
                        }
                        finally { lock (_taskGate) _legacyCloseQueued = false; }
                    }, () => { lock (_taskGate) _legacyCloseQueued = false; });
                }
                return Task.FromResult(true);
            }
            if (_closeTask is { IsCompleted: false } active)
            {
                return active;
            }

            Task<bool> task = _closeTask = CloseAndResumeCoreAsync(eventInstanceId, cancellationToken);
            _ = task.ContinueWith(
                completed => ClearCloseTask(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    internal async Task<bool> PrepareRestoredEventRoomAsync(
        EventRoom eventRoom,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventRoom);
        if (_runState.CurrentRoom is not EventRoom currentRoom || !ReferenceEquals(currentRoom, eventRoom)
            || eventRoom.CanonicalEvent is not ZenoAssentBoundary
            || _runState.CurrentRoomCount is not (1 or 2)) return false;
        if (_runtime.CurrentState is { PendingOperation: null, Route: { Stage: ZenoRouteStage.ReadyToClaim } ready })
        {
            if (_runState.CurrentRoomCount != 1 || _runState.CurrentActIndex != 2
                || _runState.CurrentMapPoint is not { PointType: MapPointType.Unknown }
                || _runState.Players.Count != 1
                || _runState.Players[0].GetRelicById(ModelDb.GetId<SocratesBalanceWeight>()) is not SocratesBalanceWeight
                || !PhilosophyRunStateService.TryGet(_runState, out var shared) || shared is null
                || !await RouteEventMapBoundary.PersistEventAsync(_runState, shared, eventRoom)) return false;
            var claim = await _runtime.ExecuteAsync(ZenoRouteStateService.PrepareOpeningClaim(
                _runtime.CurrentState, ready.Revision, ZenoOpeningTrigger.SafeBoundary, ZenoResumeDestination.Map,
                ZenoRouteNativeTriggerRegistry.NativeDestinationId(_runState),
                ZenoRouteSchedulingPolicy.CreateEventInstanceId(ready), _catalog), cancellationToken);
            if (claim.Status != ZenoRoutePersistenceCoordinationStatus.Completed) return false;
        }
        if (_runtime.CurrentState.PendingOperation is { Kind: not ZenoRouteOperationKind.EventClosed }
            || _runtime.CurrentState.PendingOperation is null && _runtime.PendingRequestId is not null)
        {
            ZenoRoutePersistenceCoordinationResult retry =
                await _runtime.RetryAsync(cancellationToken);
            if (retry.Status != ZenoRoutePersistenceCoordinationStatus.Completed)
            {
                return false;
            }
        }

        if (_runtime.CurrentState.Route?.Stage == ZenoRouteStage.OpeningClaimed
            && !(await _factory.EstablishAsync(cancellationToken)).IsReady) return false;
        ZenoRouteState? route = _runtime.CurrentState.Route;
        if (route is not
            {
                Stage: ZenoRouteStage.EventActive or ZenoRouteStage.OutcomeCommitted or ZenoRouteStage.ZenoClosed,
                EventInstanceId: { } eventInstanceId,
            })
        {
            return false;
        }
        if (ZenoRouteNativeTriggerRegistry.IsNativeDestination(route.ResumeDestinationId)
            && !ZenoRouteNativeTriggerRegistry.MatchesNativeDestination(_runState, route.ResumeDestinationId)) return false;
        if (eventRoom.IsPreFinished && route.Stage is not (ZenoRouteStage.OutcomeCommitted or ZenoRouteStage.ZenoClosed)) return false;
        if (route.Stage == ZenoRouteStage.ZenoClosed) eventRoom.MarkPreFinished();
        if (!PhilosophyRunStateService.TryGet(_runState, out var latest) || latest is null
            || !await RouteEventMapBoundary.PersistEventAsync(_runState, latest, eventRoom)) return false;

        Action<MegaCrit.Sts2.Core.Models.EventModel> configure = mutableEvent =>
        {
            if (mutableEvent is not ZenoAssentBoundary zenoEvent)
            {
                throw new InvalidOperationException(
                    "The restored Zeno room created an unexpected local event model.");
            }

            zenoEvent.Configure(
                eventInstanceId,
                new ZenoAssentBoundaryStateHost(
                    _runtime,
                    _catalog,
                    eventInstanceId,
                    token => CloseAndResumeAsync(eventInstanceId, token)),
                _catalog);
        };
        AccessTools.PropertySetter(typeof(EventRoom), nameof(EventRoom.OnStart))
            .Invoke(eventRoom, [configure]);
        return true;
    }

    private async Task<ZenoRouteNativeTriggerStatus> TriggerCoreAsync(
        ZenoRouteNativeTriggerPlan plan,
        AbstractRoom destinationRoom,
        CancellationToken cancellationToken)
    {
        if (!CanResumeLegacyClaim || !_runManager.IsInProgress ||
            !ReferenceEquals(_runManager.DebugOnlyGetState(), _runState) ||
            _runState.CurrentRoomCount != 1 ||
            !ReferenceEquals(_runState.BaseRoom, destinationRoom))
        {
            return ZenoRouteNativeTriggerStatus.NotHandled;
        }

        ZenoRouteState? expected = _runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route;
        if (expected is null || plan.Destination.Kind != expected.ResumeDestination
            || !string.Equals(plan.Destination.Id, expected.ResumeDestinationId, StringComparison.Ordinal))
            return ZenoRouteNativeTriggerStatus.Blocked;

        if (_runtime.CurrentState.PendingOperation is { Kind: not ZenoRouteOperationKind.EventClosed })
        {
            ZenoRoutePersistenceCoordinationResult retry =
                await _runtime.RetryAsync(cancellationToken);
            if (retry.Status != ZenoRoutePersistenceCoordinationStatus.Completed)
            {
                return ZenoRouteNativeTriggerStatus.Blocked;
            }
        }

        ZenoRouteState frozen = _runtime.CurrentState.Route!;
        if (!ReferenceEquals(_runManager.DebugOnlyGetState(), _runState)
            || _runState.CurrentRoomCount != 1 || !ReferenceEquals(_runState.BaseRoom, destinationRoom)
            || plan.Destination.Kind != frozen.ResumeDestination
            || !string.Equals(plan.Destination.Id, frozen.ResumeDestinationId, StringComparison.Ordinal))
        {
            // The old claim belongs to its original base/destination. Never
            // relabel a different current room with that frozen identity.
            return ZenoRouteNativeTriggerStatus.Blocked;
        }
        lock (_taskGate)
        {
            _destinationRoom = destinationRoom;
            _destination = plan.Destination;
        }

        ZenoRouteSchedulingExecutionResult scheduled = await _scheduler.ExecuteAsync(
            plan.Context,
            cancellationToken);
        if (scheduled.Status == ZenoRouteSchedulingExecutionStatus.Ignored)
        {
            ClearDestinationIfUnclaimed();
            return ZenoRouteNativeTriggerStatus.NotHandled;
        }

        if (scheduled.Status is not (
                ZenoRouteSchedulingExecutionStatus.Completed or
                ZenoRouteSchedulingExecutionStatus.ExistingClaim) ||
            scheduled.State.Route?.Stage is not (
                ZenoRouteStage.OpeningClaimed or
                ZenoRouteStage.EventActive))
        {
            return ZenoRouteNativeTriggerStatus.Blocked;
        }

        ZenoAssentBoundaryEstablishmentResult<ZenoAssentBoundary> establishment =
            await _factory.EstablishAsync(cancellationToken);
        if (!establishment.IsReady)
        {
            return ZenoRouteNativeTriggerStatus.Blocked;
        }

        NMapScreen.Instance?.Close(animateOut: false);
        ZenoAssentBoundaryRoomEntryStatus entry = await _roomEntry.EnterAsync(
            establishment,
            cancellationToken);
        return entry is
            ZenoAssentBoundaryRoomEntryStatus.Entered or
            ZenoAssentBoundaryRoomEntryStatus.AlreadyEntered
                ? ZenoRouteNativeTriggerStatus.Entered
                : ZenoRouteNativeTriggerStatus.Blocked;
    }

    private void ClearDestinationIfUnclaimed()
    {
        if (_runtime.CurrentState.Route?.Stage is not (
                ZenoRouteStage.OpeningClaimed or
                ZenoRouteStage.EventActive or
                ZenoRouteStage.OutcomeCommitted))
        {
            lock (_taskGate)
            {
                _destinationRoom = null;
                _destination = null;
            }
        }
    }

    private void ClearActiveTask(Task<ZenoRouteNativeTriggerStatus> completed)
    {
        lock (_taskGate)
        {
            if (ReferenceEquals(_activeTask, completed))
            {
                _activeTask = null;
            }
        }
    }

    private async Task<bool> CloseAndResumeCoreAsync(
        string eventInstanceId,
        CancellationToken cancellationToken)
    {
        if (!_runManager.IsInProgress ||
            !ReferenceEquals(_runManager.DebugOnlyGetState(), _runState) ||
            string.IsNullOrWhiteSpace(eventInstanceId))
        {
            return false;
        }

        ZenoRouteState? origin = _runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route;
        if (ZenoRouteNativeTriggerRegistry.IsNativeDestination(origin?.ResumeDestinationId))
            return await CloseNativeQuestionAsync(eventInstanceId, cancellationToken);

        ZenoRouteFeatureState current = _runtime.CurrentState;
        ZenoRouteState? effective = current.PendingOperation?.Candidate ?? current.Route;
        if (effective is null ||
            !string.Equals(effective.EventInstanceId, eventInstanceId, StringComparison.Ordinal) ||
            effective.Stage is not (ZenoRouteStage.OutcomeCommitted or ZenoRouteStage.ZenoClosed))
        {
            return false;
        }

        if (current.PendingOperation is null && _runtime.PendingRequestId is not null)
        {
            if ((await _runtime.RetryAsync(cancellationToken)).Status != ZenoRoutePersistenceCoordinationStatus.Completed) return false;
            current = _runtime.CurrentState;
        }
        if (current.Route?.Stage == ZenoRouteStage.ZenoClosed)
            return await ConfirmLegacyClosedRoomAsync();
        if (_runState.CurrentRoom is EventRoom { LocalMutableEvent: ZenoAssentBoundary } closingRoom)
            closingRoom.MarkPreFinished();

        if (current.PendingOperation is null)
        {
            if (current.Route?.Stage != ZenoRouteStage.OutcomeCommitted)
            {
                return current.Route?.Stage == ZenoRouteStage.ZenoClosed;
            }

            ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareClose(
                current,
                current.Route.Revision,
                _catalog);
            if (!prepared.IsAccepted)
            {
                return false;
            }

            await _runtime.PrepareExternalAsync(prepared, cancellationToken);
        }

        if (!_runtime.HasConfirmedExternalPreparation ||
            _runtime.CurrentState.PendingOperation?.Kind != ZenoRouteOperationKind.EventClosed)
        {
            return false;
        }

        ZenoRouteState closing = _runtime.CurrentState.PendingOperation.Candidate;
        if (!await ResumeFrozenDestinationAsync(closing, cancellationToken))
        {
            return false;
        }

        ZenoRoutePersistenceCoordinationResult committed =
            await _runtime.CommitExternalAsync(cancellationToken);
        bool completed = committed.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
                         committed.State.Route?.Stage == ZenoRouteStage.ZenoClosed &&
                         committed.State.PendingOperation is null;
        if (completed && closing.ResumeDestination == ZenoResumeDestination.Map)
        {
            if (!await ConfirmLegacyClosedRoomAsync()) return false;
            NMapScreen.Instance?.SetTravelEnabled(true);
            await RouteEventMapBoundary.AfterZenoClosedAsync(_runState);
        }

        return completed;
    }

    private async Task<bool> ConfirmLegacyClosedRoomAsync()
    {
        if (_runState.CurrentRoom is not EventRoom { LocalMutableEvent: ZenoAssentBoundary local } room) return true;
        room.MarkPreFinished();
        if (!PhilosophyRunStateService.TryGet(_runState, out var shared) || shared is null
            || !await RouteEventMapBoundary.PersistEventAsync(_runState, shared, room)) return false;
        local.FinishResolvedRoute();
        return true;
    }

    private async Task<bool> CloseNativeQuestionAsync(string instanceId, CancellationToken token)
    {
        ZenoRouteState? route = _runtime.CurrentState.PendingOperation?.Candidate ?? _runtime.CurrentState.Route;
        if (route is null || route.EventInstanceId != instanceId
            || route.Stage is not (ZenoRouteStage.OutcomeCommitted or ZenoRouteStage.ZenoClosed)
            || !ZenoRouteNativeTriggerRegistry.MatchesNativeDestination(_runState, route.ResumeDestinationId)
            || _runState.CurrentRoomCount != 1 || _runState.CurrentRoom is not EventRoom
                { LocalMutableEvent: ZenoAssentBoundary local } room || !ReferenceEquals(_runState.BaseRoom, room)
            || ((IZenoRouteEventSceneIdentity)local).RouteEventInstanceId != instanceId) return false;
        room.MarkPreFinished();
        if (_runtime.CurrentState.PendingOperation is not null || _runtime.PendingRequestId is not null)
        {
            var retry = _runtime.HasConfirmedExternalPreparation
                ? await _runtime.CommitExternalAsync(token) : await _runtime.RetryAsync(token);
            if (retry.Status != ZenoRoutePersistenceCoordinationStatus.Completed) return false;
        }
        if (_runtime.CurrentState.Route is { Stage: ZenoRouteStage.OutcomeCommitted } outcome)
        {
            var closed = await _runtime.ExecuteAsync(ZenoRouteStateService.PrepareClose(
                _runtime.CurrentState, outcome.Revision, _catalog), token);
            if (closed.Status != ZenoRoutePersistenceCoordinationStatus.Completed) return false;
        }
        if (_runtime.CurrentState is not { PendingOperation: null, Route.Stage: ZenoRouteStage.ZenoClosed }
            || !PhilosophyRunStateService.TryGet(_runState, out var shared) || shared is null
            || !await RouteEventMapBoundary.PersistEventAsync(_runState, shared, room)) return false;
        local.FinishResolvedRoute();
        await NEventRoom.Proceed();
        return true;
    }

    private async Task<bool> ResumeFrozenDestinationAsync(
        ZenoRouteState closing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_runState.CurrentRoom is EventRoom eventRoom)
        {
            if (eventRoom.LocalMutableEvent is not IZenoRouteEventSceneIdentity identity ||
                identity.RouteEventKind != ZenoRouteObservedEventKind.Zeno ||
                !string.Equals(
                    identity.RouteEventInstanceId,
                    closing.EventInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (_runState.CurrentRoomCount == 2)
                await ZenoRouteNativeTriggerRegistry.ResumePreviousRoomAsync(_runState, eventRoom.LocalMutableEvent);
            else if (_runState.CurrentRoomCount != 1) return false;
            if (closing.ResumeDestination == ZenoResumeDestination.Map) await NEventRoom.Proceed();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return closing.ResumeDestination switch
        {
            ZenoResumeDestination.Map => _runState.CurrentRoomCount == 1,
            ZenoResumeDestination.Boss => await ResumeBossAsync(closing.ResumeDestinationId),
            ZenoResumeDestination.ActExit => await ResumeActExitAsync(),
            _ => false,
        };
    }

    private async Task<bool> ResumeBossAsync(string? destinationId)
    {
        if (!TryParseBossDestination(destinationId, out MapCoord coordinate))
        {
            return false;
        }

        if (_runState.CurrentMapCoord is { } current &&
            current.row == coordinate.row &&
            current.col == coordinate.col)
        {
            return IsAtBoss(coordinate);
        }

        if (_runState.CurrentMapPoint is not { } source
            || !MapTravel.GetTravelablePointsFrom(_runState, source).Any(point => Equals(point.coord, coordinate))) return false;

        await _runManager.EnterMapCoord(coordinate);
        return IsAtBoss(coordinate);
    }

    private bool IsAtBoss(MapCoord coordinate) => Equals(_runState.CurrentMapCoord, coordinate)
        && _runState.CurrentRoomCount == 1 && ReferenceEquals(_runState.BaseRoom, _runState.CurrentRoom)
        && _runState.CurrentRoom is { RoomType: RoomType.Boss } room
        && _runState.CurrentMapPointHistoryEntry?.Rooms.Any(record => record.RoomType == RoomType.Boss
            && Equals(record.ModelId, room.ModelId)) == true;

    private async Task<bool> ResumeActExitAsync()
    {
        if (_runState.CurrentActIndex > 2)
        {
            return true;
        }

        if (_runState.CurrentActIndex != 2 || _runState.CurrentRoom is not CombatRoom
            { RoomType: RoomType.Boss, IsPreFinished: true } || _runState.CurrentRoomCount != 1)
        {
            return false;
        }

        await _runManager.EnterNextAct();
        return _runState.CurrentActIndex > 2;
    }

    private static bool TryParseBossDestination(
        string? destinationId,
        out MapCoord coordinate)
    {
        coordinate = default;
        if (string.IsNullOrWhiteSpace(destinationId))
        {
            return false;
        }

        string[] parts = destinationId.Split('_');
        if (parts.Length != 7 ||
            parts[0] != "BOSS" || parts[1] != "ACT" || parts[3] != "ROW" ||
            parts[5] != "COL" ||
            !int.TryParse(parts[2], out int actIndex) || actIndex != 2 ||
            !int.TryParse(parts[4], out int row) || row < 0 ||
            !int.TryParse(parts[6], out int column) || column < 0)
        {
            return false;
        }

        coordinate = new MapCoord(column, row);
        return true;
    }

    private void ClearCloseTask(Task<bool> completed)
    {
        lock (_taskGate)
        {
            if (ReferenceEquals(_closeTask, completed))
            {
                _closeTask = null;
            }
        }
    }
}
