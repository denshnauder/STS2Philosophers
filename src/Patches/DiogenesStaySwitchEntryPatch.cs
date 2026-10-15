using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace STS2Philosophers;

[HarmonyPatch]
internal static class DiogenesStaySwitchActEntryPatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
        typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)])
        ?? throw new MissingMethodException("RunManager.EnterRoomInternal");

    private static void Postfix(AbstractRoom room, bool isRestoringRoomStackBase, ref Task __result)
    {
        if (room is MapRoom && !isRestoringRoomStackBase)
        {
            __result = ContinueAsync(__result);
        }
    }

    internal static async Task ContinueAsync(Task nativeEntry)
    {
        await nativeEntry;
        try
        {
            if (DiogenesStaySwitchEntrySession.Get(RunManager.Instance) is { } session)
            {
                await session.EnterAtActMapAsync();
            }
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Diogenes entry remains blocked for recovery: {exception}");
        }
    }
}

// Run after the *whole* native restore, not while its base MapRoom is being restored.
// Otherwise a saved nested event would acquire a duplicate overlay.
[HarmonyPatch]
internal static class DiogenesStaySwitchLoadedMapPatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
        typeof(RunManager), "LoadIntoLatestMapCoord", [typeof(AbstractRoom)])
        ?? throw new MissingMethodException("RunManager.LoadIntoLatestMapCoord");

    private static void Postfix(ref Task __result) =>
        __result = DiogenesStaySwitchActEntryPatch.ContinueAsync(__result);
}

[HarmonyPatch]
internal static class DiogenesStaySwitchRestoredRoomPatch
{
    private static readonly AsyncLocal<int> Bypass = new();
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
        typeof(EventRoom), "EnterInternal", [typeof(IRunState), typeof(bool)])
        ?? throw new MissingMethodException("EventRoom.EnterInternal");

    private static bool Prefix(EventRoom __instance, IRunState runState,
        bool isRestoringRoomStackBase, ref Task __result)
    {
        if (Bypass.Value > 0 || __instance.CanonicalEvent is not DiogenesStaySwitch)
        {
            return true;
        }
        __result = EnterAsync(__instance, runState, isRestoringRoomStackBase);
        return false;
    }

    private static async Task EnterAsync(EventRoom room, IRunState run, bool restoring)
    {
        NMapScreen.Instance?.SetTravelEnabled(false);
        DiogenesStaySwitchEntrySession? session = DiogenesStaySwitchEntrySession.Get(RunManager.Instance);
        if (session is null || !await session.PrepareEventRoomAsync(room))
        {
            Log.Error("[STS2Philosophers] Restored Diogenes identity/transaction is unavailable; entry stays blocked.");
            return;
        }
        Bypass.Value++;
        try
        {
            await room.EnterInternal(run, restoring);
        }
        finally
        {
            Bypass.Value--;
        }
    }
}
