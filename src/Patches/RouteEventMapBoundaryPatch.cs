using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch]
internal static class RouteEventMapBoundaryPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(NMapScreen), nameof(NMapScreen.Open), [typeof(bool)])
        ?? throw new MissingMethodException(typeof(NMapScreen).FullName, "Open(bool)");

    private static void Postfix(NMapScreen __instance) => RouteEventMapBoundary.MapOpened(__instance);
}

[HarmonyPatch]
internal static class RouteEventMapBoundaryLoadedMapPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), "LoadIntoLatestMapCoord", [typeof(AbstractRoom)])
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "LoadIntoLatestMapCoord(AbstractRoom)");

    private static void Prefix(RunManager __instance, out RouteEventMapBoundaryScope? __state) =>
        __state = RouteEventMapBoundary.BeginRestore(__instance);

    private static void Postfix(RouteEventMapBoundaryScope? __state, ref Task __result) =>
        __result = RouteEventMapBoundary.CompleteRestoreAsync(__result, __state);
}

// A terminal reward screen may stay in the overlay stack after explicit Skip.
// Its awaited native Proceed distinguishes that choice from the map hotkey.
[HarmonyPatch]
internal static class RouteEventMapBoundaryRewardsProceedPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen), Type.EmptyTypes)
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "ProceedFromTerminalRewardsScreen()");

    private static void Prefix(RunManager __instance, out RouteEventMapBoundaryScope? __state) =>
        __state = RouteEventMapBoundary.BeginRewardsProceed(__instance);

    private static void Postfix(RouteEventMapBoundaryScope? __state, ref Task __result) =>
        __result = RouteEventMapBoundary.CompleteRewardsProceedAsync(__result, __state);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class RouteEventMapBoundarySaveRoomPatch
{
    private static void Prefix(RunManager __instance, ref AbstractRoom? preFinishedRoom)
    {
        if (preFinishedRoom is null
            && RouteEventMapBoundary.TryGetCurrentRegisteredRoom(__instance, out EventRoom? room))
        {
            preFinishedRoom = room;
        }
    }
}
