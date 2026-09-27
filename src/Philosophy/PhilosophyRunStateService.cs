using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Logging;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal static class PhilosophyRunStateService
{
    private static readonly ConditionalWeakTable<RunState, PhilosophyRunState> States = new();
    private static readonly ConditionalWeakTable<RunState, ResolutionMarker> ResolvedRuns = new();

    public static PhilosophyRunState GetOrCreate(RunState runState)
    {
        Resolve(runState);
        return States.GetValue(runState, static _ => new PhilosophyRunState());
    }

    public static bool TryGet(RunState runState, out PhilosophyRunState? state)
    {
        Resolve(runState);
        return States.TryGetValue(runState, out state);
    }

    public static void Restore(RunState runState, PhilosophyRunState state)
    {
        ReplaceState(runState, state);
        PhilosophyRunStateRitsuStore.TryBind(runState, state);
        MarkResolved(runState);
    }

    internal static void RestoreFromLegacyMarker(RunState runState, PhilosophyRunState state)
    {
        ReplaceState(runState, state);
        ResolvedRuns.Remove(runState);
    }

    public static GeneratedCandidates GetOrGenerateActOneCandidates(RunState runState)
    {
        return PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(
            GetOrCreate(runState),
            runState.Rng.Seed);
    }

    public static void RecordCurrentDoctrine(
        RunState runState,
        ThinkerProposal proposal)
    {
        PhilosophyRunState state = GetOrCreate(runState);
        state.RecordCurrentDoctrine(
            proposal.ThinkerId,
            proposal.DoctrineId,
            proposal.RouteTags);
        state.GetOrCreateActBehaviorState(runState.CurrentActIndex);
    }

    internal static SocratesVirtueRecordWriteResult OpenSocratesVirtueOpportunity(
        RunState runState,
        string opportunityId,
        string combatId)
    {
        return GetOrCreate(runState).OpenSocratesVirtueOpportunity(
            opportunityId,
            combatId,
            runState.CurrentActIndex);
    }

    internal static SocratesVirtueRecordWriteResult CommitSocratesVirtueAction(
        RunState runState,
        string opportunityId,
        SocratesVirtueAction action,
        int retainedHitPointLoss,
        int consumedPotionCount)
    {
        return GetOrCreate(runState).CommitSocratesVirtueAction(
            opportunityId,
            action,
            retainedHitPointLoss,
            consumedPotionCount);
    }

    internal static bool CancelSocratesVirtueOpportunity(
        RunState runState,
        string opportunityId)
    {
        return GetOrCreate(runState).CancelSocratesVirtueOpportunity(opportunityId);
    }

    internal static bool TryGetSocratesVirtueMaterial(
        RunState runState,
        out SocratesVirtueMaterial? material)
    {
        material = null;
        return TryGet(runState, out PhilosophyRunState? state)
            && state is not null
            && state.TryGetSocratesVirtueMaterial(out material);
    }

    private static void Resolve(RunState runState)
    {
        if (ResolvedRuns.TryGetValue(runState, out _))
        {
            return;
        }

        PhilosophyRunStateRitsuRestoreStatus status = PhilosophyRunStateRitsuStore.TryRestore(
            runState,
            out PhilosophyRunState? restored);
        if (status == PhilosophyRunStateRitsuRestoreStatus.Current && restored is not null)
        {
            ReplaceState(runState, restored);
        }
        else
        {
            PhilosophyRunState state = States.GetValue(runState, static _ => new PhilosophyRunState());
            if (status == PhilosophyRunStateRitsuRestoreStatus.Invalid)
            {
                Log.Error("[STS2Philosophers] RitsuLib philosophy run data is invalid; preserving it without overwrite.");
            }
            else
            {
                PhilosophyRunStateRitsuStore.TryBind(runState, state);
            }
        }

        MarkResolved(runState);
    }

    private static void ReplaceState(RunState runState, PhilosophyRunState state)
    {
        ZenoRouteRuntimeService.Remove(runState);
        States.Remove(runState);
        States.Add(runState, state);
    }

    private static void MarkResolved(RunState runState)
    {
        ResolvedRuns.Remove(runState);
        ResolvedRuns.Add(runState, new ResolutionMarker());
    }

    private sealed class ResolutionMarker;
}
