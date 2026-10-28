using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace STS2Philosophers;

internal sealed record RouteEventMapBoundaryScope(RunState Run, AbstractRoom? Source);

// The native map is a screen over the completed source room, not a new MapRoom.
// These runtime receipts authorize only that observed screen/source pair; the
// route state and native EventRoom remain in the existing Ritsu/native save.
internal static class RouteEventMapBoundary
{
    private static readonly ConditionalWeakTable<RunState, BoundaryRuntime> Runtimes = new();

    internal static bool IsRegisteredEvent(EventModel? model) => model is
        PhilosophersGaze or ProtagorasSharedDeliberation or PlatoWholeArrangement or EpicurusSharedMargin or PlotinusCommonGround
        or ZenoCivicChallenge or AugustinePromise or AnselmUnfinishedProof or NewtonTwoSites or SpinozaCauseNetwork
        or SextusOpposedAppearances or DescartesMethodicReconstruction or HanFeiPublicStandard
        or ParmenidesInquiry or AnaximanderOriginInquiry or AnaximenesBreathInvitation
        or AristotleCivicInquiry or DemocritusArrangement or EmpedoclesRootsInquiry
        or AquinasBridgeNotice or MachiavelliRealistChallenge or OckhamNamesInquiry or MachiavelliFloodPreparation or LutherSignedCare or BaconSampleLedger or CopernicusShiftedCenter or GalileoMeasuredPractice
        or AnaxagorasMixtureInquiry or GorgiasSpeechInquiry or SenecaDailyReview
        or EpictetusGuidePractice or MarcusAureliusCampPractice or PyrrhoReportedCrossing
        or DiogenesStaySwitch or ZenoAssentBoundary or LeibnizIndependentClocks or HobbesAuthorizedSovereignty or LockeCargoIdentity or HumeInduction or WollstonecraftAdmission or KantApperception;

    internal static bool IsSafe(RunState run) => IsSafe(run, allowOwnTravelLock: false);

    private static bool IsSafe(RunState run, bool allowOwnTravelLock)
    {
        RunManager manager = RunManager.Instance;
        if (!IsCurrentRun(manager, run) || !PhilosophyRunStateService.IsAvailable(run)
            || run.IsGameOver || run.CurrentRoomCount != 1
            || run.CurrentRoom is not { } source || !ReferenceEquals(source, run.BaseRoom)
            || !IsCompletedSource(source) || CombatManager.Instance.IsInProgress
            || !Runtimes.TryGetValue(run, out BoundaryRuntime? runtime) || runtime.Frozen
            || Volatile.Read(ref runtime.Restoring) != 0 || Volatile.Read(ref runtime.RewardsProceeding) != 0
            || !ReferenceEquals(runtime.OpenedSource, source)) return false;

        NMapScreen? map = NMapScreen.Instance;
        bool ownLock = allowOwnTravelLock && ReferenceEquals(runtime.EnteringSource, source);
        if (map is null || !ReferenceEquals(runtime.OpenedMap, map) || !map.IsOpen || map.IsTraveling
            || (!map.IsTravelEnabled && !ownLock)
            || (!ownLock && runtime.EnteringSource is not null)
            || ActiveScreenContext.Instance?.IsCurrent(map) != true) return false;

        NOverlayStack? overlays = NOverlayStack.Instance;
        if (overlays is null) return false;
        int count = overlays.ScreenCount;
        NRewardsScreen? rewards = count == 1 ? overlays.Peek() as NRewardsScreen : null;
        bool complete = rewards?.IsComplete == true
            && NativeAccess.GetRewardsSet(rewards) is { } set
            && manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set);
        return RewardsPermitBoundary(count, rewards is not null, complete,
            ReferenceEquals(runtime.RewardsReleasedSource, source), source is CombatRoom);
    }

    internal static bool RewardsPermitBoundary(int overlayCount, bool rewardsOverlay,
        bool rewardsCompleted, bool sourceAcknowledged, bool sourceIsCombat)
    {
        if (overlayCount is < 0 or > 1
            || overlayCount == 1 && (!rewardsOverlay || !rewardsCompleted && !sourceAcknowledged)) return false;
        // EndCombat enables travel before mounting rewards. Zero overlays alone
        // therefore cannot establish that a completed combat's rewards are done.
        return !sourceIsCombat || sourceAcknowledged
            || overlayCount == 1 && rewardsOverlay && rewardsCompleted;
    }

    private static bool IsCompletedSource(AbstractRoom source) => source switch
    {
        CombatRoom combat => combat.IsPreFinished && combat.RoomType != RoomType.Boss && !combat.IsVictoryRoom,
        EventRoom routeEvent => routeEvent.LocalMutableEvent.IsFinished,
        MapRoom => true,
        // These rooms use native travel availability rather than IsPreFinished
        // (the base implementation is always false). Modal/reward guards still apply.
        MerchantRoom or RestSiteRoom or TreasureRoom => true,
        _ => false,
    };

    internal static async Task<bool> EnterAsync(RunManager manager, RunState run, EventModel canonical)
    {
        if (!IsRegisteredEvent(canonical) || !IsCurrentRun(manager, run) || !IsSafe(run)
            || run.CurrentRoom is not { } source) return false;
        BoundaryRuntime runtime = Runtimes.GetValue(run, static _ => new());
        if (runtime.EnteringSource is not null) return false;
        runtime.EnteringSource = source;
        NMapScreen map = NMapScreen.Instance!;
        try
        {
            EventRoom room = new(canonical);
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Freeze(run, "Route event run state is unavailable.");
                return false;
            }
            map.SetTravelEnabled(false);
            if (!await PersistEventAsync(run, shared, room)
                || !ReferenceEquals(run.CurrentRoom, source) || !IsSafe(run, allowOwnTravelLock: true))
            {
                Freeze(run, "Route event entry persistence or source confirmation is unknown.");
                return false;
            }
            map.Close(animateOut: false);
            // Native entry clears terminal reward overlays only on its fade path.
            // A completed or explicitly skipped rewards screen may still be stacked.
            await manager.EnterRoomWithoutExitingCurrentRoom(room, fadeToBlack: true);
            bool entered = IsCurrentRun(manager, run) && ReferenceEquals(run.CurrentRoom, room)
                && room.LocalMutableEvent.GetType() == canonical.GetType();
            if (!entered && IsCurrentRun(manager, run))
                Freeze(run, "The native route event entry did not retain its expected room identity.");
            return entered;
        }
        catch (Exception exception)
        {
            Freeze(run, $"Route event entry failed: {exception}");
            return false;
        }
        finally
        {
            runtime.EnteringSource = null;
        }
    }

    internal static async Task<bool> PersistEventAsync(
        RunState run, PhilosophyRunState shared, EventRoom? expectedRoom = null)
    {
        RunManager manager = RunManager.Instance;
        if (!IsCurrentRun(manager, run) || !PhilosophyRunStateService.TryGet(run, out var current)
            || !ReferenceEquals(current, shared) || !PhilosophyRunStateRitsuStore.TryBind(run, shared)
            || !PhilosophyRunStateRitsuStore.IsAuthoritative(run, shared)) return false;
        EventRoom? room = expectedRoom ?? run.CurrentRoom as EventRoom;
        if (room is null || !IsRegisteredEvent(room.CanonicalEvent)) return false;
        string expected = PhilosophyRunStateCodec.Encode(shared);
        await SaveManager.Instance.SaveRun(room);
        if (!IsCurrentRun(manager, run)
            || PhilosophyRunStateCodec.Encode(shared) != expected) return false;

        SaveManager saveManager = SaveManager.Instance;
        RunSaveManager runSaveManager = NativeAccess.GetRunSaveManager(saveManager);
        string? json = NativeAccess.GetSaveStore(saveManager).ReadFile(
            NativeAccess.GetCurrentRunSavePath(runSaveManager));
        if (json is null) return false;
        PhilosophyRunStateRitsuSaveReadResult read = PhilosophyRunStateRitsuSaveReader.Read(json);
        if (read.Classification != PhilosophyRunStateRitsuSaveClassification.Current
            || !string.Equals(read.EncodedState, expected, StringComparison.Ordinal)) return false;
        ReadSaveResult<SerializableRun> native = saveManager.LoadRunSave();
        return native.Status == ReadSaveStatus.Success && native.SaveData is { } saved
            && MatchesSavedRoom(saved, room);
    }

    internal static bool MatchesSavedRoom(SerializableRun save, EventRoom expectedRoom) =>
        save.PreFinishedRoom is { RoomType: RoomType.Event } saved
        && Equals(saved.EventId, expectedRoom.CanonicalEvent.Id)
        && saved.IsPreFinished == expectedRoom.IsPreFinished;

    internal static bool TryGetCurrentRegisteredRoom(RunManager manager, out EventRoom? room)
    {
        room = null;
        if (!manager.IsInProgress || manager.DebugOnlyGetState() is not { } run
            || !IsCurrentRun(manager, run) || run.IsGameOver
            || run.CurrentRoom is not EventRoom candidate || !IsRegisteredEvent(candidate.CanonicalEvent)
            || candidate.LocalMutableEvent.GetType() != candidate.CanonicalEvent.GetType()
            || !(run.CurrentRoomCount == 1 && ReferenceEquals(run.BaseRoom, candidate)
                || run.CurrentRoomCount == 2 && run.BaseRoom is not null)) return false;
        // Closed events remain the exact current native room while their map is
        // open. Preserve them too, until native travel actually replaces the room.
        room = candidate;
        return true;
    }

    internal static void MapOpened(NMapScreen map)
    {
        RunManager manager = RunManager.Instance;
        if (!manager.IsInProgress || manager.DebugOnlyGetState() is not { } run
            || !IsCurrentRun(manager, run)) return;
        BoundaryRuntime runtime = Runtimes.GetValue(run, static _ => new());
        runtime.OpenedSource = run.CurrentRoom;
        runtime.OpenedMap = map;
        _ = DispatchObservedAsync(run);
    }

    // Zeno resumes its native map before its separate close checkpoint commits.
    // Preserve the close notification without starting another event at the map.
    internal static Task AfterZenoClosedAsync(RunState run) => DispatchObservedAsync(run);

    internal static RouteEventMapBoundaryScope? BeginRestore(RunManager manager)
    {
        if (!manager.IsInProgress || manager.DebugOnlyGetState() is not { } run) return null;
        Interlocked.Increment(ref Runtimes.GetValue(run, static _ => new()).Restoring);
        return new(run, null);
    }

    internal static async Task CompleteRestoreAsync(Task native, RouteEventMapBoundaryScope? scope)
    {
        try { await native; }
        finally
        {
            if (scope is not null)
                Interlocked.Decrement(ref Runtimes.GetValue(scope.Run, static _ => new()).Restoring);
        }
        if (scope is null) return;
        if (NMapScreen.Instance is { IsOpen: true } map
            && IsCurrentRun(RunManager.Instance, scope.Run))
        {
            BoundaryRuntime runtime = Runtimes.GetValue(scope.Run, static _ => new());
            runtime.OpenedSource = scope.Run.CurrentRoom;
            runtime.OpenedMap = map;
        }
        await DispatchObservedAsync(scope.Run);
    }

    internal static RouteEventMapBoundaryScope? BeginRewardsProceed(RunManager manager)
    {
        if (!manager.IsInProgress || manager.DebugOnlyGetState() is not { } run) return null;
        Interlocked.Increment(ref Runtimes.GetValue(run, static _ => new()).RewardsProceeding);
        AbstractRoom? source = run.CurrentRoomCount == 1
            && ReferenceEquals(run.CurrentRoom, run.BaseRoom)
            && run.CurrentRoom is CombatRoom { IsPreFinished: true } combat ? combat : null;
        return new(run, source);
    }

    internal static async Task CompleteRewardsProceedAsync(Task native, RouteEventMapBoundaryScope? scope)
    {
        try { await native; }
        finally
        {
            if (scope is not null)
                Interlocked.Decrement(ref Runtimes.GetValue(scope.Run, static _ => new()).RewardsProceeding);
        }
        if (scope?.Source is not { } source || !IsCurrentRun(RunManager.Instance, scope.Run)
            || !ReferenceEquals(scope.Run.CurrentRoom, source)) return;
        Runtimes.GetValue(scope.Run, static _ => new()).RewardsReleasedSource = source;
        await DispatchObservedAsync(scope.Run);
    }

    // Map/reward/restore observations never manufacture an event room. A saved
    // room restores through the native stack; all new non-opening claims happen
    // only in ModifyNextEvent after a real Unknown node has rolled Event.
    private static Task DispatchObservedAsync(RunState run) => Task.CompletedTask;

    private static bool IsCurrentRun(RunManager manager, RunState run) =>
        manager.IsInProgress && manager.ShouldSave && run.Players.Count == 1
        && manager.NetService.Type == NetGameType.Singleplayer
        && ReferenceEquals(manager.DebugOnlyGetState(), run);

    internal static bool IsActiveEvent(RunState run, EventModel local, int actIndex) =>
        IsCurrentRun(RunManager.Instance, run) && !run.IsGameOver && run.CurrentActIndex == actIndex
        && run.CurrentRoom is EventRoom room && ReferenceEquals(room.LocalMutableEvent, local)
        && (run.CurrentRoomCount == 1 && ReferenceEquals(run.BaseRoom, room)
            || run.CurrentRoomCount == 2 && run.BaseRoom is not null);

    internal static void Freeze(RunState run, string reason)
    {
        Runtimes.GetValue(run, static _ => new()).Frozen = true;
        if (RunManager.Instance.IsInProgress && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run))
            NMapScreen.Instance?.SetTravelEnabled(false);
        Log.Error($"[STS2Philosophers] {reason} Map travel remains frozen until reload.");
    }

    private sealed class BoundaryRuntime
    {
        internal AbstractRoom? OpenedSource;
        internal NMapScreen? OpenedMap;
        internal AbstractRoom? RewardsReleasedSource;
        internal AbstractRoom? EnteringSource;
        internal int Restoring;
        internal int RewardsProceeding;
        internal bool Frozen;
    }

    private static class NativeAccess
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_rewardsSet")]
        internal static extern ref RewardsSet GetRewardsSet(NRewardsScreen screen);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_runSaveManager")]
        internal static extern ref RunSaveManager GetRunSaveManager(SaveManager manager);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_saveStore")]
        internal static extern ref ISaveStore GetSaveStore(SaveManager manager);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CurrentRunSavePath")]
        internal static extern string GetCurrentRunSavePath(RunSaveManager manager);
    }
}
