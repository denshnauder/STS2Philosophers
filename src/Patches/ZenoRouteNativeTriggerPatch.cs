using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class ZenoRouteMapSafeBoundaryPatch
{
    private static void Postfix(NMapScreen __instance)
    {
        RunManager runManager = RunManager.Instance;
        if (!ZenoRouteNativeTriggerRegistry.TryGet(runManager, out ZenoRouteNativeTriggerSession? session) ||
            session is null ||
            !session.CanIntercept ||
            runManager.DebugOnlyGetState() is not { } runState ||
            runState.CurrentRoom is not { } currentRoom ||
            runState.BaseRoom is not { } baseRoom)
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
                out ZenoRouteNativeTriggerPlan? plan) ||
            plan is null)
        {
            return;
        }

        __instance.SetTravelEnabled(false);
        _ = ObserveMapTriggerAsync(
            __instance,
            session.TriggerAsync(plan, baseRoom));
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

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
internal static class ZenoRouteBossGatePatch
{
    private static readonly AsyncLocal<int> BypassDepth = new();

    private static bool Prefix(RunManager __instance, MapCoord __0, ref Task __result)
    {
        if (BypassDepth.Value > 0 ||
            !ZenoRouteNativeTriggerRegistry.TryGet(__instance, out ZenoRouteNativeTriggerSession? session) ||
            session is null ||
            !session.CanIntercept ||
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
            !session.CanIntercept ||
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
