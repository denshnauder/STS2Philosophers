using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace STS2Philosophers;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class ZenoRouteMapSafeBoundaryPatch
{
    private static void Postfix(NMapScreen __instance)
    {
        RunManager runManager = RunManager.Instance;
        if (DiogenesStaySwitchEntrySession.Get(runManager)?.HasQueuedLegacyExit == true)
        {
            __instance.SetTravelEnabled(false);
            return;
        }
        if (!ZenoRouteNativeTriggerRegistry.TryGet(runManager, out ZenoRouteNativeTriggerSession? session) ||
            session is null ||
            runManager.DebugOnlyGetState() is not { } runState ||
            runState.CurrentRoom is not { } currentRoom ||
            runState.BaseRoom is not { } baseRoom)
        {
            return;
        }

        if (session.HasPendingClose && session.EffectiveEventInstanceId is { } closingEventId)
        {
            __instance.SetTravelEnabled(false);
            _ = ObserveCloseRecoveryAsync(
                session.CloseAndResumeAsync(closingEventId));
            return;
        }

        if (!session.CanIntercept && !session.HasIntervalRecovery)
        {
            return;
        }

        PhilosophyRunStateService.TryGet(runState, out PhilosophyRunState? shared);
        if (shared?.DiogenesStaySwitchEntry is { Closed: false })
        {
            return;
        }

        ZenoRouteObservedEventKind eventKind = currentRoom is EventRoom eventRoom
            ? eventRoom.LocalMutableEvent is IZenoRouteEventSceneIdentity identity
                ? identity.RouteEventKind
                : ZenoRouteObservedEventKind.Unrelated
            : ZenoRouteObservedEventKind.None;
        if (!ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                new ZenoRouteNativeMapBoundary(
                    runState.CurrentActIndex,
                    runState.CurrentRoomCount,
                    currentRoom.Id,
                    currentRoom.IsPreFinished,
                    currentRoom is MapRoom,
                    eventKind),
                out ZenoRouteNativeTriggerPlan? plan,
                shared?.DiogenesStaySwitchEntry?.SourceRoomId) ||
            plan is null)
        {
            return;
        }

        if (!session.CanResumeLegacyClaim)
        {
            // Observe the finished intervening room, but never claim/open an
            // event while displaying this map. Only a later real ? can do that.
            if ((session.NeedsIntervalObservation && plan.Context.CompletedRoomReceiptId is not null
                    || session.HasIntervalRecovery) && __instance.IsTravelEnabled)
            {
                __instance.SetTravelEnabled(false);
                _ = ObserveIntervalAsync(__instance, runState, currentRoom, session, plan.Context.CompletedRoomReceiptId);
            }
            return;
        }

        __instance.SetTravelEnabled(false);
        _ = ObserveMapTriggerAsync(
            __instance,
            session.TriggerAsync(plan, baseRoom));
    }

    private static async Task ObserveIntervalAsync(NMapScreen map, RunState run, AbstractRoom source,
        ZenoRouteNativeTriggerSession session, string? receipt)
    {
        try
        {
            if (!await session.ObserveIntervalAsync(receipt))
            {
                RouteEventMapBoundary.Freeze(run, "Zeno's actual intervening room has not been durably confirmed.");
                return;
            }
            if (ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
                && ReferenceEquals(run.CurrentRoom, source) && map.IsOpen && !map.IsTraveling)
                map.SetTravelEnabled(true);
        }
        catch (Exception error) { RouteEventMapBoundary.Freeze(run, $"Zeno interval recovery failed: {error}"); }
    }

    private static async Task ObserveCloseRecoveryAsync(Task<bool> recovery)
    {
        try
        {
            if (!await recovery)
            {
                Log.Error("[STS2Philosophers] Zeno closing recovery remains blocked; map travel stays disabled.");
            }
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Zeno closing recovery failed; map travel stays disabled: {exception}");
        }
    }

    private static async Task ObserveMapTriggerAsync(
        NMapScreen mapScreen,
        Task<ZenoRouteNativeTriggerStatus> trigger)
    {
        try
        {
            ZenoRouteNativeTriggerStatus status = await trigger;
            if (status == ZenoRouteNativeTriggerStatus.NotHandled)
            {
                mapScreen.SetTravelEnabled(true);
            }
            else if (status == ZenoRouteNativeTriggerStatus.Blocked)
            {
                Log.Error("[STS2Philosophers] Zeno map trigger is blocked; map travel remains disabled for safe recovery.");
            }
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Zeno map trigger failed; map travel remains disabled: {exception}");
        }
    }
}

[HarmonyPatch]
internal static class ZenoRouteRestoredEventRoomPatch
{
    private static readonly AsyncLocal<int> BypassDepth = new();

    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(EventRoom),
            "EnterInternal",
            [typeof(IRunState), typeof(bool)])
        ?? throw new MissingMethodException(
            typeof(EventRoom).FullName,
            "EnterInternal(IRunState, bool)");

    private static bool Prefix(
        EventRoom __instance,
        IRunState runState,
        bool isRestoringRoomStackBase,
        ref Task __result)
    {
        if (BypassDepth.Value > 0 || __instance.CanonicalEvent is not ZenoAssentBoundary) return true;
        if (runState is not RunState concreteRunState ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), concreteRunState) ||
            !ZenoRouteNativeTriggerRegistry.TryGet(
                RunManager.Instance,
                out ZenoRouteNativeTriggerSession? session) ||
            session is null)
        {
            Log.Error("[STS2Philosophers] A selected Zeno room has no matching current transaction; entry remains blocked.");
            __result = Task.CompletedTask;
            return false;
        }

        __result = PrepareAndEnterAsync(
            __instance,
            runState,
            isRestoringRoomStackBase,
            session);
        return false;
    }

    private static async Task PrepareAndEnterAsync(
        EventRoom eventRoom,
        IRunState runState,
        bool isRestoringRoomStackBase,
        ZenoRouteNativeTriggerSession session)
    {
        if (!await session.PrepareRestoredEventRoomAsync(eventRoom))
        {
            Log.Error("[STS2Philosophers] A restored Zeno event could not bind its persisted runtime; entry stays blocked.");
            return;
        }

        BypassDepth.Value++;
        try
        {
            await eventRoom.EnterInternal(runState, isRestoringRoomStackBase);
        }
        finally
        {
            BypassDepth.Value--;
        }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
internal static class ZenoRouteBossGatePatch
{
    private static readonly AsyncLocal<int> BypassDepth = new();

    private static bool Prefix(RunManager __instance, MapCoord __0, ref Task __result)
    {
        if (BypassDepth.Value > 0 ||
            !ZenoRouteNativeTriggerRegistry.TryGet(__instance, out ZenoRouteNativeTriggerSession? session) ||
            session is null ||
            !session.CanResumeLegacyClaim ||
            __instance.DebugOnlyGetState() is not { } runState ||
            runState.Map is not { } map ||
            runState.BaseRoom is not { } baseRoom ||
            map.GetPoint(__0) is not { PointType: MapPointType.Boss } ||
            !ZenoRouteNativeTriggerPolicy.TryCreateBossPlan(
                runState.CurrentActIndex,
                runState.CurrentRoomCount,
                __0.row,
                __0.col,
                out ZenoRouteNativeTriggerPlan? plan) ||
            plan is null)
        {
            return true;
        }

        NMapScreen.Instance?.SetTravelEnabled(false);
        __result = TriggerBeforeBossAsync(
            __instance,
            __0,
            session,
            plan,
            baseRoom);
        return false;
    }

    private static async Task TriggerBeforeBossAsync(
        RunManager runManager,
        MapCoord coord,
        ZenoRouteNativeTriggerSession session,
        ZenoRouteNativeTriggerPlan plan,
        AbstractRoom destinationRoom)
    {
        ZenoRouteNativeTriggerStatus status = await session.TriggerAsync(plan, destinationRoom);
        if (status == ZenoRouteNativeTriggerStatus.Entered)
        {
            return;
        }

        if (status == ZenoRouteNativeTriggerStatus.Blocked)
        {
            Log.Error("[STS2Philosophers] Zeno Boss gate is blocked before native travel; the Boss coordinate was not consumed.");
            return;
        }

        BypassDepth.Value++;
        try
        {
            await runManager.EnterMapCoord(coord);
        }
        finally
        {
            BypassDepth.Value--;
        }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
internal static class ZenoRouteActExitGatePatch
{
    private static readonly AsyncLocal<int> BypassDepth = new();

    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (BypassDepth.Value > 0 ||
            !ZenoRouteNativeTriggerRegistry.TryGet(__instance, out ZenoRouteNativeTriggerSession? session) ||
            session is null ||
            !session.CanResumeLegacyClaim ||
            __instance.DebugOnlyGetState() is not { } runState ||
            runState.CurrentRoom is not { } currentRoom ||
            runState.BaseRoom is not { } baseRoom ||
            !ZenoRouteNativeTriggerPolicy.TryCreateActExitPlan(
                runState.CurrentActIndex,
                runState.CurrentRoomCount,
                currentRoom.Id,
                out ZenoRouteNativeTriggerPlan? plan) ||
            plan is null)
        {
            return true;
        }

        __result = TriggerBeforeActExitAsync(
            __instance,
            session,
            plan,
            baseRoom);
        return false;
    }

    private static async Task TriggerBeforeActExitAsync(
        RunManager runManager,
        ZenoRouteNativeTriggerSession session,
        ZenoRouteNativeTriggerPlan plan,
        AbstractRoom destinationRoom)
    {
        ZenoRouteNativeTriggerStatus status = await session.TriggerAsync(plan, destinationRoom);
        if (status == ZenoRouteNativeTriggerStatus.Entered)
        {
            return;
        }

        if (status == ZenoRouteNativeTriggerStatus.Blocked)
        {
            Log.Error("[STS2Philosophers] Zeno act-exit gate is blocked before native transition; the act was not advanced.");
            return;
        }

        BypassDepth.Value++;
        try
        {
            await runManager.EnterNextAct();
        }
        finally
        {
            BypassDepth.Value--;
        }
    }
}
