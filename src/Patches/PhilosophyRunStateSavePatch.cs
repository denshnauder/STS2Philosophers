using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using System.Reflection;

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
    private static void Prefix(RunManager __instance)
    {
        RunState? runState = __instance.DebugOnlyGetState();
        if (runState is not null)
        {
            if (PhilosophyRunStateService.TryGet(runState, out var state) && state is not null)
                PhilosophyRunStateRitsuStore.TryBind(runState, state);
        }
    }

    private static void Postfix(RunManager __instance, ref SerializableRun __result)
    {
        List<string> existingEntries = __result.EventsSeen
            .Where(PhilosophyRunStateSaveMarker.IsCategoryMarker)
            .Select(marker => marker.Entry)
            .ToList();
        __result.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsCategoryMarker);
        IReadOnlyList<string> entries = existingEntries;
        RunState? runState = __instance.DebugOnlyGetState();
        if (runState is not null && !PhilosophyRunStateService.IsAvailable(runState))
            entries = PhilosophyRunStateService.GetIsolatedLegacyEntries(runState);
        if (runState is not null
            && PhilosophyRunStateService.TryGet(runState, out PhilosophyRunState? state)
            && state is not null)
        {
            entries = PhilosophyRunStateRitsuStore.IsAuthoritative(runState, state)
                ? []
                : PhilosophyRunStateMarkerCarrier.GetEntriesForSave(state);
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
    internal sealed record LoadedMarkers(PhilosophyRunStateMarkerRestoreResult Restore, IReadOnlyList<string> Entries);

    private static void Prefix(
        SerializableRun save,
        out LoadedMarkers __state)
    {
        List<string> entries = save.EventsSeen
            .Where(PhilosophyRunStateSaveMarker.IsCategoryMarker)
            .Select(marker => marker.Entry)
            .ToList();
        save.EventsSeen.RemoveAll(PhilosophyRunStateSaveMarker.IsCategoryMarker);
        var restore = PhilosophyRunStateMarkerCarrier.Restore(entries);
        __state = new(restore, entries);
        if (restore.IsIsolated)
        {
            Log.Error(
                $"[STS2Philosophers] Isolated philosophy run state markers: " +
                $"classification={restore.Classification}, count={restore.MarkerCount}.");
        }
    }

    private static void Postfix(
        SerializableRun save,
        RunState __result,
        LoadedMarkers __state)
    {
        PhilosophyRunStateRitsuStore.ObserveRestoredRun(save, __result);
        PhilosophyRunStateService.PreserveLoadedLegacyEntries(__result, __state.Entries);
        if (__state.Restore.State is not null)
        {
            PhilosophyRunStateService.RestoreFromLegacyMarker(__result, __state.Restore.State);
        }
    }
}

// Capture the exact bytes returned to the native loader. The game's generic
// migration method can share a runtime body with other save types; Harmony's
// closed-generic patch is not invoked reliably on that body.
[HarmonyPatch]
internal static class PhilosophyRunStateSaveReadPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(ISaveStore).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ISaveStore).IsAssignableFrom(type))
            .Select(type => AccessTools.DeclaredMethod(type, nameof(ISaveStore.ReadFile), [typeof(string)]))
            .Where(method => method is not null)!;

    private static void Postfix(string __0, string __result) =>
        PhilosophyRunStateNativeLoadCapture.ObserveInput(__0, __result);
}

// Scope file reads to the non-generic native run loader. If native falls back
// to a backup, the last successfully read path is that backup and stays unsafe.
internal sealed class PhilosophyRunStateNativeLoadCapture
{
    [ThreadStatic] private static PhilosophyRunStateNativeLoadCapture? _active;

    private PhilosophyRunStateNativeLoadCapture(PhilosophyRunStateNativeLoadCapture? previous) =>
        Previous = previous;

    internal PhilosophyRunStateNativeLoadCapture? Previous { get; }
    internal string? SourcePath { get; private set; }
    internal string? Content { get; private set; }

    internal static PhilosophyRunStateNativeLoadCapture Begin() =>
        _active = new PhilosophyRunStateNativeLoadCapture(_active);

    internal static void ObserveInput(string sourcePath, string content)
    {
        if (_active is { } capture)
        {
            capture.SourcePath = sourcePath;
            capture.Content = content;
        }
    }

    internal static void End(PhilosophyRunStateNativeLoadCapture? capture)
    {
        if (capture is not null) _active = capture.Previous;
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
internal static class PhilosophyRunStateNativeRunLoadPatch
{
    private static void Prefix(out PhilosophyRunStateNativeLoadCapture __state) =>
        __state = PhilosophyRunStateNativeLoadCapture.Begin();

    private static void Postfix(
        ReadSaveResult<SerializableRun> __result,
        PhilosophyRunStateNativeLoadCapture __state) =>
        PhilosophyRunStateRitsuStore.ObserveNativeRunLoad(
            __result, __state.Content, __state.SourcePath);

    private static Exception? Finalizer(
        Exception? __exception,
        PhilosophyRunStateNativeLoadCapture? __state)
    {
        PhilosophyRunStateNativeLoadCapture.End(__state);
        return __exception;
    }
}

// The native SaveRun catches write exceptions and otherwise completes normally.
// Reject before that catch when preserving the loaded record cannot be proven.
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveRun), [typeof(AbstractRoom), typeof(bool)])]
internal static class PhilosophyRunStateUnsafeSaveGuardPatch
{
    private static bool Prefix(ref Task __result)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (run is null || PhilosophyRunStateService.IsAvailable(run)
            || !PhilosophyRunStateRitsuStore.TryGetUnsafeSaveReason(run, out string? reason)) return true;
        string message = $"[STS2Philosophers] Refused to save this isolated run; original save files remain unchanged. {reason}";
        Log.Error(message);
        __result = Task.FromException(new IOException(message));
        return false;
    }
}
