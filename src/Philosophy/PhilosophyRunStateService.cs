using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal static class PhilosophyRunStateService
{
    private static readonly ConditionalWeakTable<RunState, PhilosophyRunState> States = new();

    public static PhilosophyRunState GetOrCreate(RunState runState)
    {
        return States.GetValue(runState, _ => new PhilosophyRunState());
    }

    public static bool TryGet(RunState runState, out PhilosophyRunState? state)
    {
        return States.TryGetValue(runState, out state);
    }

    public static void Restore(RunState runState, PhilosophyRunState state)
    {
        States.Remove(runState);
        States.Add(runState, state);
    }

    public static GeneratedCandidates GetOrGenerateActOneCandidates(RunState runState)
    {
        return PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(
            GetOrCreate(runState),
            runState.Rng.Seed);
    }

    public static void RecordCurrentDoctrine(
        RunState runState,
        string thinkerId,
        string doctrineId)
    {
        PhilosophyRunState state = GetOrCreate(runState);
        state.RecordCurrentDoctrine(thinkerId, doctrineId);
        state.GetOrCreateActBehaviorState(runState.CurrentActIndex);
    }
}
