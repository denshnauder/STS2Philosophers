using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch]
internal static class PhilosophersGazeSaveRecoveryPatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
        typeof(RunManager), "LoadIntoLatestMapCoord", [typeof(AbstractRoom)])
        ?? throw new MissingMethodException("RunManager.LoadIntoLatestMapCoord(AbstractRoom)");

    private static void Prefix(RunManager __instance, ref AbstractRoom? preFinishedRoom) =>
        PhilosophersGazeSaveRecovery.TryRestoreLegacyRoom(__instance, ref preFinishedRoom);
}
