using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace STS2Philosophers;

internal static class RitsuRunPersistenceProbe
{
    private const string ModId = "STS2Philosophers";
    private const string SlotKey = "ENC68_RUN_PERSISTENCE_PROBE";
    private static RunSavedData<RitsuRunPersistenceProbeState>? _slot;
    private static int _initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            _slot = RitsuLibFramework.GetRunSavedDataStore(ModId).Register(
                key: SlotKey,
                defaultFactory: static () => new RitsuRunPersistenceProbeState(),
                options: new RunSavedDataOptions
                {
                    SchemaVersion = RitsuRunPersistenceProbeState.CurrentSchemaVersion,
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                });
        }
    }

    internal static bool TryWrite(RunState runState, out RitsuRunPersistenceProbeState? state)
    {
        if (_slot is null)
        {
            state = null;
            return false;
        }

        _slot.Modify(runState, static data => data.RecordWrite());
        state = _slot.Get(runState);
        return true;
    }

    internal static bool TryRead(RunState runState, out RitsuRunPersistenceProbeState? state)
    {
        if (_slot is null)
        {
            state = null;
            return false;
        }

        return _slot.TryGet(runState, out state);
    }
}

public sealed class RitsuRunPersistenceProbeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "ritsurunprobe";
    public override string Args => "<write|read>";
    public override string Description =>
        "Write or read the ENC68 RitsuLib run-save persistence probe.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        RunManager runManager = RunManager.Instance;
        if (issuingPlayer is null ||
            !runManager.IsInProgress ||
            runManager.DebugOnlyGetState() is not { } runState ||
            !ReferenceEquals(issuingPlayer.RunState, runState))
        {
            return new CmdResult(false, "A current run is required.");
        }

        if (args.Length != 1)
        {
            return Usage();
        }

        return args[0].ToLowerInvariant() switch
        {
            "write" => Write(runState),
            "read" => Read(runState),
            _ => Usage(),
        };
    }

    private static CmdResult Write(RunState runState)
    {
        if (!RitsuRunPersistenceProbe.TryWrite(runState, out RitsuRunPersistenceProbeState? state) ||
            state is null)
        {
            return new CmdResult(false, "The RitsuLib run-save probe was not initialized.");
        }

        return new CmdResult(
            true,
            $"ENC68 probe write {state.WriteCount} is ready. Save normally, return to the main menu, then Load and run 'ritsurunprobe read'.");
    }

    private static CmdResult Read(RunState runState)
    {
        if (!RitsuRunPersistenceProbe.TryRead(runState, out RitsuRunPersistenceProbeState? state) ||
            state is null)
        {
            return new CmdResult(false, "No ENC68 run-save probe is attached to this run.");
        }

        if (!state.IsValid())
        {
            return new CmdResult(
                false,
                $"ENC68 probe data is invalid: schema={state.SchemaVersion}, token={state.ProbeToken}, writes={state.WriteCount}.");
        }

        return new CmdResult(
            true,
            $"ENC68 probe restored: schema={state.SchemaVersion}, token={state.ProbeToken}, writes={state.WriteCount}.");
    }

    private static CmdResult Usage() =>
        new(false, "Usage: ritsurunprobe <write|read>");
}
