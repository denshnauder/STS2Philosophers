using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

// Owns the planned act-entry slot, not a random question-mark event.
internal sealed class DiogenesStaySwitchEntrySession
{
    private static readonly ConditionalWeakTable<RunState, DiogenesStaySwitchEntrySession> Sessions = new();
    private readonly RunState _run;
    private readonly PhilosophyRunState _shared;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _entryConfirmed;
    private bool _closeConfirmed;

    private DiogenesStaySwitchEntrySession(RunState run, PhilosophyRunState shared)
    {
        _run = run;
        _shared = shared;
    }

    internal static DiogenesStaySwitchEntrySession? Get(RunManager manager)
    {
        if (!manager.IsInProgress || manager.NetService.Type != NetGameType.Singleplayer ||
            manager.DebugOnlyGetState() is not { Players.Count: 1 } run)
        {
            return null;
        }

        PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
        DiogenesStaySwitchEntrySession session = Sessions.GetValue(run, key => new(key, shared));
        return ReferenceEquals(session._shared, shared) ? session : null;
    }

    internal async Task EnterAtActMapAsync()
    {
        // Proceed may resume MapRoom through EnterRoomInternal while Close holds the
        // gate. The durable terminal marker makes that callback a no-op, not a lock cycle.
        if (_shared.DiogenesStaySwitchEntry is { Closed: true })
        {
            return;
        }
        await _gate.WaitAsync();
        try
        {
            if (!IsCurrent() || _run.CurrentActIndex != 2 || _run.CurrentRoomCount != 1 ||
                _run.CurrentRoom is not MapRoom mapRoom)
            {
                return;
            }

            DiogenesStaySwitchEntryRecord? entry = _shared.DiogenesStaySwitchEntry;
            if (entry is null)
            {
                if (!DiogenesStaySwitchEntryPolicy.TryCreate(
                        _shared, _run.Rng.Seed,
                        new DiogenesStaySwitchEntryContext(2, 1, true, false, mapRoom.Id),
                        out entry) || entry is null)
                {
                    return;
                }

                _shared.DiogenesStaySwitchEntry = entry;
                _shared.SetCurrentZenoRouteState(
                    DiogenesStaySwitchEntryPolicy.CreateInitialState(entry),
                    ZenoRouteValidationCatalogs.SaveRestore);
            }

            if (entry.Closed || !DiogenesStaySwitchEntryPolicy.IsValid(entry) ||
                entry.SourceRoomId != mapRoom.Id)
            {
                return;
            }

            NMapScreen.Instance?.SetTravelEnabled(false);
            if (!_entryConfirmed)
            {
                _entryConfirmed = await PersistBoundaryAsync();
                if (!_entryConfirmed)
                {
                    throw new InvalidOperationException("Diogenes entry persistence is unknown; keep the map frozen.");
                }
            }

            EventRoom room = new(ModelDb.Event<DiogenesStaySwitch>());
            if (!await PrepareEventRoomAsync(room))
            {
                throw new InvalidOperationException("Diogenes entry cannot bind the persisted route.");
            }

            NMapScreen.Instance?.Close(animateOut: false);
            await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(room, fadeToBlack: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<bool> PrepareEventRoomAsync(EventRoom room)
    {
        if (!IsCurrent() || _shared.DiogenesStaySwitchEntry is not { } entry ||
            !DiogenesStaySwitchEntryPolicy.IsValid(entry) ||
            !ZenoRouteRuntimeService.TryGetOrCreate(_run, _shared.ZenoRouteValidationCatalog, out var runtime) ||
            runtime is null)
        {
            return false;
        }

        // A restored prepared choice must finish the same transaction before any options exist.
        if (runtime.CurrentState.PendingOperation is not null || runtime.PendingRequestId is not null)
        {
            if (runtime.CurrentState.PendingOperation is { Kind: not (ZenoRouteOperationKind.Stay or ZenoRouteOperationKind.Switch) } ||
                (await runtime.RetryAsync()).Status != ZenoRoutePersistenceCoordinationStatus.Completed)
            {
                return false;
            }
        }

        if (runtime.CurrentState.Route is not { } route || route.RunId != entry.RunId ||
            !DiogenesStaySwitchPageFlow.TryRestore(runtime.CurrentState, _shared.ZenoRouteValidationCatalog, out _))
        {
            return false;
        }

        Action<EventModel> configure = model =>
        {
            if (model is not DiogenesStaySwitch local)
            {
                throw new InvalidOperationException("Unexpected event in the Diogenes slot.");
            }
            local.Configure(
                new DiogenesStaySwitchStateHost(runtime, _shared.ZenoRouteValidationCatalog,
                    entry.Material, _ => CloseAsync()),
                _shared.ZenoRouteValidationCatalog,
                entry);
        };
        AccessTools.PropertySetter(typeof(EventRoom), nameof(EventRoom.OnStart)).Invoke(room, [configure]);
        return true;
    }

    private async Task<bool> CloseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!IsCurrent() || _shared.DiogenesStaySwitchEntry is not { } entry ||
                _run.CurrentRoomCount != 2 || _run.BaseRoom is not MapRoom baseRoom ||
                baseRoom.Id != entry.SourceRoomId ||
                _run.CurrentRoom is not EventRoom { LocalMutableEvent: DiogenesStaySwitch local } ||
                ((IZenoRouteEventSceneIdentity)local).RouteEventInstanceId != entry.RunId ||
                _shared.ZenoRoutePayload.State is not { PendingOperation: null, Route: { } route } ||
                route.Stage is not (ZenoRouteStage.DiogenesClosed or ZenoRouteStage.WaitingInterval))
            {
                return false;
            }

            _shared.DiogenesStaySwitchEntry = entry with { Closed = true };
            if (!_closeConfirmed)
            {
                _closeConfirmed = await PersistBoundaryAsync();
                if (!_closeConfirmed)
                {
                    return false;
                }
            }
            await NEventRoom.Proceed();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> PersistBoundaryAsync()
    {
        if (_shared.ZenoRoutePayload.State is not { PendingOperation: null } state)
        {
            return false;
        }
        ZenoRouteGameSaveGateway gateway = new(_run, _shared, _shared.ZenoRouteValidationCatalog);
        if (!gateway.TryBindState(state))
        {
            return false;
        }
        string expected = PhilosophyRunStateCodec.Encode(_shared);
        await gateway.SaveRunAsync();
        ZenoRouteSaveReadBack read = gateway.ReadCurrentRunSave();
        return read.IsAvailable && read.MarkerEntries.Count == 1 &&
            read.MarkerEntries[0] == PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix + expected;
    }

    private bool IsCurrent() => RunManager.Instance.IsInProgress &&
        ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), _run) &&
        PhilosophyRunStateService.TryGet(_run, out var shared) && ReferenceEquals(shared, _shared);
}
