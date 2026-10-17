using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Logging;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal static class PhilosophyRunStateService
{
    private static readonly ConditionalWeakTable<RunState, PhilosophyRunState> States = new();
    private static readonly ConditionalWeakTable<RunState, ResolutionMarker> ResolvedRuns = new();
    private static readonly ConditionalWeakTable<RunState, List<string>> LoadedLegacyEntries = new();

    internal static bool IsAvailable(RunState runState)
    {
        Resolve(runState);
        return !PhilosophyRunStateRitsuStore.IsIsolated(runState);
    }

    internal static void PreserveLoadedLegacyEntries(RunState runState, IEnumerable<string> entries)
    {
        LoadedLegacyEntries.Remove(runState);
        LoadedLegacyEntries.Add(runState, entries.ToList());
    }

    internal static IReadOnlyList<string> GetIsolatedLegacyEntries(RunState runState) =>
        LoadedLegacyEntries.TryGetValue(runState, out var entries) ? entries : [];

    public static PhilosophyRunState GetOrCreate(RunState runState)
    {
        Resolve(runState);
        return States.GetValue(runState, static _ => new PhilosophyRunState());
    }

    public static bool TryGet(RunState runState, out PhilosophyRunState? state)
    {
        Resolve(runState);
        if (PhilosophyRunStateRitsuStore.IsIsolated(runState))
        {
            state = null;
            return false;
        }
        return States.TryGetValue(runState, out state);
    }

    public static void Restore(RunState runState, PhilosophyRunState state)
    {
        if (PhilosophyRunStateRitsuStore.IsIsolated(runState)) return;
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
        if (!IsAvailable(runState)) return new GeneratedCandidates();
        return PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(
            GetOrCreate(runState),
            runState.Rng.Seed);
    }

    public static void RecordCurrentDoctrine(
        RunState runState,
        ThinkerProposal proposal)
    {
        if (!IsAvailable(runState)) return;
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
        if (!IsAvailable(runState)) return SocratesVirtueRecordWriteResult.Rejected;
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
        if (!IsAvailable(runState)) return SocratesVirtueRecordWriteResult.Rejected;
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
        if (!IsAvailable(runState)) return false;
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
                PhilosophyRunStateRitsuStore.Isolate(runState);
                ReplaceState(runState, new PhilosophyRunState());
                Log.Error("[STS2Philosophers] RitsuLib philosophy run data is invalid; preserving it without overwrite.");
            }
            else
            {
                if (!PhilosophyRunStateRitsuPayload.CanPersist(state))
                    PhilosophyRunStateRitsuStore.Isolate(runState);
                else
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
