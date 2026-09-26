using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
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

    public static bool TryGet(
        RunManager runManager,
        out ZenoRouteNativeTriggerSession? session)
    {
        session = null;
        if (!runManager.IsInProgress ||
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
    private AbstractRoom? _destinationRoom;
    private ZenoRouteObservedDestination? _destination;

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
            this);
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

            _activeTask = TriggerCoreAsync(plan, destinationRoom, cancellationToken);
            _ = _activeTask.ContinueWith(
                completed => ClearActiveTask(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return _activeTask;
        }
    }

    public ZenoRouteObservedDestination? ReadDestination(AbstractRoom room)
    {
        lock (_taskGate)
        {
            return ReferenceEquals(room, _destinationRoom) ? _destination : null;
        }
    }

    private async Task<ZenoRouteNativeTriggerStatus> TriggerCoreAsync(
        ZenoRouteNativeTriggerPlan plan,
        AbstractRoom destinationRoom,
        CancellationToken cancellationToken)
    {
        if (!_runManager.IsInProgress ||
            !ReferenceEquals(_runManager.DebugOnlyGetState(), _runState) ||
            _runState.CurrentRoomCount != 1 ||
            !ReferenceEquals(_runState.BaseRoom, destinationRoom))
        {
            return ZenoRouteNativeTriggerStatus.NotHandled;
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
}
