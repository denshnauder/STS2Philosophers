using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

internal static class PhilosophyRunStateSaveMarker
{
    public const string Category = "STS2PhilosophersRunState";
    public const string VersionPrefix = "V1_";

    public static bool IsMarker(ModelId id)
    {
        return string.Equals(id.Category, Category, StringComparison.Ordinal)
            && id.Entry.StartsWith(VersionPrefix, StringComparison.Ordinal);
    }

    public static ModelId Encode(PhilosophyRunState state)
    {
        return new ModelId(Category, $"{VersionPrefix}{PhilosophyRunStateCodec.Encode(state)}");
    }

    public static PhilosophyRunState Decode(ModelId marker)
    {
        return PhilosophyRunStateCodec.Decode(marker.Entry[VersionPrefix.Length..]);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class PhilosophyRunStateToSavePatch
{
    private static void Postfix(RunManager __instance, ref SerializableRun __result)
    {
        __result.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsMarker);
        RunState? runState = __instance.DebugOnlyGetState();
        if (runState is not null
            && PhilosophyRunStateService.TryGet(runState, out PhilosophyRunState? state)
            && state is { HasData: true })
        {
            __result.EventsSeen.Add(PhilosophyRunStateSaveMarker.Encode(state));
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class PhilosophyRunStateFromSavePatch
{
    private static void Prefix(SerializableRun save, out PhilosophyRunState? __state)
    {
        __state = null;
        ModelId? marker = save.EventsSeen.LastOrDefault(PhilosophyRunStateSaveMarker.IsMarker);
        save.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsMarker);
        if (marker is null)
        {
            return;
        }

        try
        {
            __state = PhilosophyRunStateSaveMarker.Decode(marker);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Ignoring an invalid philosophy run state marker: {exception}");
        }
    }

    private static void Postfix(RunState __result, PhilosophyRunState? __state)
    {
        if (__state is not null)
        {
            PhilosophyRunStateService.Restore(__result, __state);
        }
    }
}
