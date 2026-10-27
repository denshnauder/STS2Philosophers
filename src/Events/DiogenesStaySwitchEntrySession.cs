using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

// Prepares a native question slot; legacy records retain their source identity.
internal sealed class DiogenesStaySwitchEntrySession
{
    private static readonly ConditionalWeakTable<RunState, DiogenesStaySwitchEntrySession> Sessions = new();
    private readonly RunState _run;
    private readonly PhilosophyRunState _shared;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _closeConfirmed;
    private bool _legacyResumeQueued;
    internal bool HasQueuedLegacyExit => _legacyResumeQueued;

    private DiogenesStaySwitchEntrySession(RunState run, PhilosophyRunState shared)
    {
        _run = run;
        _shared = shared;
    }

    internal static DiogenesStaySwitchEntrySession? Get(RunManager manager)
    {
        if (!manager.IsInProgress || manager.NetService?.Type != NetGameType.Singleplayer ||
            manager.DebugOnlyGetState() is not { Players.Count: 1 } run)
        {
            return null;
        }

        if (!PhilosophyRunStateService.TryGet(run, out var shared) || shared is null) return null;
        DiogenesStaySwitchEntrySession session = Sessions.GetValue(run, key => new(key, shared));
        return ReferenceEquals(session._shared, shared) ? session : null;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.CurrentActIndex != 2
            || run.CurrentMapCoord is not { } coord
            || run.Players[0].GetRelicById(ModelDb.GetId<SocratesBalanceWeight>()) is not SocratesBalanceWeight
            || !PhilosophyRunStateService.TryGet(run, out var shared) || shared is null) return null;
        EventModel canonical;
        try { canonical = ModelDb.Event<DiogenesStaySwitch>(); }
        catch { return null; }
        if (run.VisitedEventIds.Contains(canonical.Id)) return null;
        if (shared.DiogenesStaySwitchEntry is { } saved)
            return !saved.Closed && saved.Version == 1 && DiogenesStaySwitchEntryPolicy.IsValid(saved)
                && shared.ZenoRoutePayload.State is { Route: { } route } feature && route.RunId == saved.RunId
                && route.Stage is ZenoRouteStage.Unresolved or ZenoRouteStage.DiogenesClosed or ZenoRouteStage.WaitingInterval
                && (feature.PendingOperation is null || feature.PendingOperation.Kind is ZenoRouteOperationKind.Stay or ZenoRouteOperationKind.Switch)
                ? canonical : null;
        if (!DiogenesStaySwitchEntryPolicy.TryCreateNative(shared, run.Rng.Seed,
            new(2, coord.row, coord.col), out var entry) || entry is null) return null;
        shared.DiogenesStaySwitchEntry = entry;
        shared.SetCurrentZenoRouteState(DiogenesStaySwitchEntryPolicy.CreateInitialState(entry),
            ZenoRouteValidationCatalogs.SaveRestore);
        return canonical;
    }

    internal async Task<bool> PrepareEventRoomAsync(EventRoom room)
    {
        if (!IsCurrent() || _shared.DiogenesStaySwitchEntry is not { } entry ||
            !DiogenesStaySwitchEntryPolicy.IsValid(entry) ||
            !MatchesRoom(entry, room) || room.IsPreFinished && !entry.Closed ||
            !ZenoRouteRuntimeService.TryGetOrCreate(_run, _shared.ZenoRouteValidationCatalog, out var runtime) ||
            runtime is null)
        {
            return false;
        }

        // The room is already on the native stack, but its mutable event has
        // not started. Persist this exact identity before any pending retry.
        if (!await RouteEventMapBoundary.PersistEventAsync(_run, _shared, room)) return false;

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
            !entry.Closed && !DiogenesStaySwitchPageFlow.TryRestore(runtime.CurrentState, _shared.ZenoRouteValidationCatalog, out _))
        {
            return false;
        }

        if (entry.Closed) room.MarkPreFinished();
        // This is the actual native room, already pushed by RunManager. Confirm
        // its identity and the exact business payload before OnStart exposes buttons.
        if (!await RouteEventMapBoundary.PersistEventAsync(_run, _shared, room)) return false;

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
                _run.CurrentRoom is not EventRoom { LocalMutableEvent: DiogenesStaySwitch local } ||
                !MatchesRoom(entry, (EventRoom)_run.CurrentRoom) ||
                ((IZenoRouteEventSceneIdentity)local).RouteEventInstanceId != entry.RunId ||
                _shared.ZenoRoutePayload.State is not { PendingOperation: null, Route: { } route } ||
                route.Stage is not (ZenoRouteStage.DiogenesClosed or ZenoRouteStage.WaitingInterval))
            {
                return false;
            }

            _shared.DiogenesStaySwitchEntry = entry with { Closed = true };
            ((EventRoom)_run.CurrentRoom).MarkPreFinished();
            if (!_closeConfirmed)
            {
                _closeConfirmed = await PersistBoundaryAsync();
                if (!_closeConfirmed)
                {
                    return false;
                }
            }
            if (_run.CurrentRoomCount == 2)
            {
                if (!_legacyResumeQueued)
                {
                    _legacyResumeQueued = true;
                    NMapScreen.Instance?.SetTravelEnabled(false);
                    ZenoRouteNativeTriggerRegistry.QueueLegacyExit(_run, local, async () =>
                    {
                        try
                        {
                            await ZenoRouteNativeTriggerRegistry.ResumePreviousRoomAsync(_run, local);
                            NMapScreen.Instance?.SetTravelEnabled(false);
                            if (!await PersistBoundaryAsync()) throw new IOException("The closed Diogenes base checkpoint is unconfirmed.");
                            _legacyResumeQueued = false;
                            await NEventRoom.Proceed();
                        }
                        finally { _legacyResumeQueued = false; }
                    }, () => _legacyResumeQueued = false);
                }
                return true;
            }
            local.FinishResolvedEntry();
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
        if (_run.CurrentRoom is EventRoom room)
            return await RouteEventMapBoundary.PersistEventAsync(_run, _shared, room);
        if (_run.CurrentRoomCount != 1 || _run.CurrentRoom is not MapRoom source
            || _shared.DiogenesStaySwitchEntry?.Closed != true) return false;
        ZenoRouteGameSaveGateway gateway = new(_run, _shared, _shared.ZenoRouteValidationCatalog);
        if (!gateway.TryBindState(state)) return false;
        string expected = PhilosophyRunStateCodec.Encode(_shared);
        await gateway.SaveRunAsync();
        ZenoRouteSaveReadBack read = gateway.ReadCurrentRunSave();
        return ReferenceEquals(_run.CurrentRoom, source) && read.IsAvailable && read.MarkerEntries.Count == 1
            && read.MarkerEntries[0] == PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix + expected;
    }

    private bool MatchesRoom(DiogenesStaySwitchEntryRecord entry, EventRoom room)
    {
        if (!ReferenceEquals(_run.CurrentRoom, room) || room.CanonicalEvent is not DiogenesStaySwitch
            || entry.RunId != DiogenesStaySwitchEntryPolicy.RunIdPrefix + _run.Rng.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
        if (_run.CurrentRoomCount == 2)
            return entry.Version == 1 && _run.BaseRoom is MapRoom source && source.Id == entry.SourceRoomId;
        if (_run.CurrentRoomCount != 1 || !ReferenceEquals(_run.BaseRoom, room)) return false;
        if (entry.NativeNode is not { } node) return entry.Version == 1;
        return _run.CurrentActIndex == node.ActIndex && _run.CurrentMapCoord is { } coord
            && coord.row == node.Row && coord.col == node.Column
            && _run.CurrentMapPoint is { PointType: MapPointType.Unknown };
    }

    private bool IsCurrent() => RunManager.Instance.IsInProgress &&
        ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), _run) &&
        PhilosophyRunStateService.TryGet(_run, out var shared) && ReferenceEquals(shared, _shared);
}
