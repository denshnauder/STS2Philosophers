using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

internal static class PhilosophyRunStateSaveMarker
{
    public const string Category = "STS2PhilosophersRunState";
    public const string VersionPrefix = PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix;

    public static bool IsCategoryMarker(ModelId id)
    {
        return string.Equals(id.Category, Category, StringComparison.Ordinal);
    }

    public static ModelId FromEntry(string entry)
    {
        return new ModelId(Category, entry);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class PhilosophyRunStateToSavePatch
{
    private static void Postfix(RunManager __instance, ref SerializableRun __result)
    {
        List<string> existingEntries = __result.EventsSeen
            .Where(PhilosophyRunStateSaveMarker.IsCategoryMarker)
            .Select(marker => marker.Entry)
            .ToList();
        __result.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsCategoryMarker);
        IReadOnlyList<string> entries = existingEntries;
        RunState? runState = __instance.DebugOnlyGetState();
        if (runState is not null
            && PhilosophyRunStateService.TryGet(runState, out PhilosophyRunState? state)
            && state is not null)
        {
            entries = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(state);
        }

        foreach (string entry in entries)
        {
            __result.EventsSeen.Add(PhilosophyRunStateSaveMarker.FromEntry(entry));
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class PhilosophyRunStateFromSavePatch
{
    private static void Prefix(
        SerializableRun save,
        out PhilosophyRunStateMarkerRestoreResult __state)
    {
        List<string> entries = save.EventsSeen
            .Where(PhilosophyRunStateSaveMarker.IsCategoryMarker)
            .Select(marker => marker.Entry)
            .ToList();
        save.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsCategoryMarker);
        __state = PhilosophyRunStateMarkerCarrier.Restore(entries);
        if (__state.IsIsolated)
        {
            Log.Error(
                $"[STS2Philosophers] Isolated philosophy run state markers: " +
                $"classification={__state.Classification}, count={__state.MarkerCount}.");
        }
    }

    private static void Postfix(
        RunState __result,
        PhilosophyRunStateMarkerRestoreResult __state)
    {
        if (__state.State is not null)
        {
            PhilosophyRunStateService.Restore(__result, __state.State);
        }
    }
}
