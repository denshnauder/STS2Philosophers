using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal enum PhilosophyRunStateRitsuRestoreStatus
{
    Missing,
    Current,
    Invalid,
}

internal static class PhilosophyRunStateRitsuStore
{
    private static RunSavedData<PhilosophyRunStateRitsuPayload>? _slot;
    private static int _initialized;
    private static readonly ConditionalWeakTable<SerializableRun, PhilosophyRunStateRitsuSaveReadResult> LoadedSaves = new();
    private static readonly ConditionalWeakTable<RunState, PhilosophyRunStateRitsuSaveReadResult> LoadedRuns = new();
    private static readonly ConditionalWeakTable<RunState, object> IsolatedRuns = new();

    // Capture the exact JSON passed to the native migration/deserialization call,
    // not a later read of current_run.save. Ritsu's load hook rereads the primary
    // file even after native backup recovery, so that case cannot be authoritative.
    internal static void ObserveLoadedSave(SerializableRun save, string content, string sourcePath)
    {
        LoadedSaves.Remove(save);
        LoadedSaves.Add(save, sourcePath.EndsWith(".backup", StringComparison.OrdinalIgnoreCase)
            ? new(PhilosophyRunStateRitsuSaveClassification.Invalid, null, false,
                "The native run was recovered from backup, while RitsuLib rereads the primary file.")
            : PhilosophyRunStateRitsuSaveReader.Read(content));
    }

    internal static void ObserveRestoredRun(SerializableRun save, RunState run)
    {
        LoadedRuns.Remove(run);
        LoadedRuns.Add(run, LoadedSaves.TryGetValue(save, out var snapshot)
            ? snapshot : new(PhilosophyRunStateRitsuSaveClassification.Invalid, null, false,
                "The loaded run has no verified native JSON snapshot."));
    }

    // Bind the exact native store read to the final run DTO before
    // FromSerializable runs. The generic migration hook did not execute in
    // the live host, leaving every resumed run without a source receipt.
    internal static void ObserveNativeRunLoad(
        ReadSaveResult<SerializableRun> result, string? content, string? sourcePath)
    {
        if (result.SaveData is not { } save) return;
        if (result.Status != ReadSaveStatus.Success)
        {
            LoadedSaves.Remove(save);
            LoadedSaves.Add(save, new(PhilosophyRunStateRitsuSaveClassification.Invalid,
                null, false, "The native run required save recovery before loading."));
            return;
        }
        if (LoadedSaves.TryGetValue(save, out _)) return;
        if (content is null || sourcePath is null)
        {
            LoadedSaves.Add(save, new(PhilosophyRunStateRitsuSaveClassification.Invalid,
                null, false, "The native run load did not expose its JSON input."));
            return;
        }

        ObserveLoadedSave(save, content, sourcePath);
    }

    internal static bool IsIsolated(RunState run) => IsolatedRuns.TryGetValue(run, out _);

    internal static void Isolate(RunState run) => IsolatedRuns.GetValue(run, static _ => new object());

    internal static bool TryGetUnsafeSaveReason(RunState run, out string? reason)
    {
        reason = null;
        if (!IsIsolated(run) || !LoadedRuns.TryGetValue(run, out var snapshot)
            || snapshot.CanPreserveRaw) return false;
        reason = snapshot.UnsafeSaveReason
            ?? "RitsuLib cannot preserve the malformed philosophy save container or non-object slot.";
        return true;
    }

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(PhilosophyRunStateRitsuContract.ModId))
        {
            _slot = RitsuLibFramework.GetRunSavedDataStore(PhilosophyRunStateRitsuContract.ModId).Register(
                key: PhilosophyRunStateRitsuContract.SlotKey,
                defaultFactory: static () => new PhilosophyRunStateRitsuPayload(),
                options: new RunSavedDataOptions
                {
                    SchemaVersion = PhilosophyRunStateRitsuPayload.CurrentSchemaVersion,
                    // Unvalidated imported payloads must remain untouched in the
                    // framework's preserved document, including typed import failures.
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                });
        }
    }

    internal static PhilosophyRunStateRitsuRestoreStatus TryRestore(
        RunState runState,
        out PhilosophyRunState? state)
    {
        state = null;
        LoadedRuns.TryGetValue(runState, out var snapshot);
        if (IsIsolated(runState) || snapshot?.Classification == PhilosophyRunStateRitsuSaveClassification.Invalid)
        {
            return PhilosophyRunStateRitsuRestoreStatus.Invalid;
        }
        if (_slot is null || !_slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? payload))
        {
            if (snapshot?.Classification == PhilosophyRunStateRitsuSaveClassification.Current)
                BlockUnmatchedSnapshot(runState, snapshot);
            return snapshot?.Classification == PhilosophyRunStateRitsuSaveClassification.Current
                ? PhilosophyRunStateRitsuRestoreStatus.Invalid : PhilosophyRunStateRitsuRestoreStatus.Missing;
        }

        if (snapshot is not null && (snapshot.Classification != PhilosophyRunStateRitsuSaveClassification.Current
            || !string.Equals(snapshot.EncodedState, payload.EncodedState, StringComparison.Ordinal)))
        {
            BlockUnmatchedSnapshot(runState, snapshot);
            return PhilosophyRunStateRitsuRestoreStatus.Invalid;
        }

        if (!payload.TryDecode(out state) || state is null)
        {
            return PhilosophyRunStateRitsuRestoreStatus.Invalid;
        }

        payload.BindLiveState(state);
        return PhilosophyRunStateRitsuRestoreStatus.Current;
    }

    internal static bool TryBind(RunState runState, PhilosophyRunState state)
    {
        if (_slot is null || IsIsolated(runState) || !PhilosophyRunStateRitsuPayload.CanPersist(state))
        {
            return false;
        }

        if (LoadedRuns.TryGetValue(runState, out var snapshot)
            && (snapshot.Classification == PhilosophyRunStateRitsuSaveClassification.Invalid
                || snapshot.Classification == PhilosophyRunStateRitsuSaveClassification.Current
                    && !_slot.TryGet(runState, out _)))
        {
            if (snapshot.Classification == PhilosophyRunStateRitsuSaveClassification.Current)
                BlockUnmatchedSnapshot(runState, snapshot);
            Isolate(runState);
            return false;
        }

        if (_slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? existing))
        {
            if (existing.LiveState is null && snapshot is not null
                && (snapshot.Classification != PhilosophyRunStateRitsuSaveClassification.Current
                    || !string.Equals(snapshot.EncodedState, existing.EncodedState, StringComparison.Ordinal)))
            {
                BlockUnmatchedSnapshot(runState, snapshot);
                Isolate(runState);
                return false;
            }
            if (existing.LiveState is null &&
                !existing.TryDecode(out _))
            {
                Log.Error("[STS2Philosophers] Refused to overwrite invalid RitsuLib philosophy run data.");
                return false;
            }

            existing.BindLiveState(state);
            if (state.HasData) _slot.Set(runState, existing);
            return true;
        }

        if (!state.HasData) return false;
        _slot.Set(runState, PhilosophyRunStateRitsuPayload.Bind(state));
        return true;
    }

    internal static bool IsAuthoritative(RunState runState, PhilosophyRunState state)
    {
        return !IsIsolated(runState) && _slot is not null &&
               _slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? payload) &&
               ReferenceEquals(payload.LiveState, state) &&
               PhilosophyRunStateRitsuPayload.CanPersist(state);
    }

    private static void BlockUnmatchedSnapshot(RunState run, PhilosophyRunStateRitsuSaveReadResult snapshot)
    {
        LoadedRuns.Remove(run);
        LoadedRuns.Add(run, snapshot with { CanPreserveRaw = false,
            UnsafeSaveReason = "RitsuLib's imported slot does not match the native loaded snapshot." });
    }
}
