using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace STS2Philosophers;

// Prepare only a room that native travel/load has already selected. MapRoom
// entry and loaded maps never create an additional Diogenes room.
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
