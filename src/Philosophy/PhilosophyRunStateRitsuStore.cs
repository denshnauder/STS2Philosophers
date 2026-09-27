using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

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
                    WritePolicy = RunSavedDataWritePolicy.WhenNonDefault,
                });
        }
    }

    internal static PhilosophyRunStateRitsuRestoreStatus TryRestore(
        RunState runState,
        out PhilosophyRunState? state)
    {
        state = null;
        if (_slot is null || !_slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? payload))
        {
            return PhilosophyRunStateRitsuRestoreStatus.Missing;
        }

        if (string.IsNullOrWhiteSpace(payload.EncodedState))
        {
            return PhilosophyRunStateRitsuRestoreStatus.Missing;
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
        if (_slot is null || !PhilosophyRunStateRitsuPayload.CanPersist(state))
        {
            return false;
        }

        if (_slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? existing))
        {
            if (existing.LiveState is null &&
                !string.IsNullOrWhiteSpace(existing.EncodedState) &&
                !existing.TryDecode(out _))
            {
                Log.Error("[STS2Philosophers] Refused to overwrite invalid RitsuLib philosophy run data.");
                return false;
            }

            existing.BindLiveState(state);
            _slot.Set(runState, existing);
            return true;
        }

        _slot.Set(runState, PhilosophyRunStateRitsuPayload.Bind(state));
        return true;
    }

    internal static bool IsAuthoritative(RunState runState, PhilosophyRunState state)
    {
        return _slot is not null &&
               _slot.TryGet(runState, out PhilosophyRunStateRitsuPayload? payload) &&
               ReferenceEquals(payload.LiveState, state) &&
               PhilosophyRunStateRitsuPayload.CanPersist(state);
    }
}
