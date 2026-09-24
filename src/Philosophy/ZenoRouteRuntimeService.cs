using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal static class ZenoRouteRuntimeService
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<RunState, ZenoRoutePersistenceRuntime> Runtimes = new();

    public static bool TryGetOrCreate(
        RunState runState,
        ZenoRouteValidationCatalog catalog,
        out ZenoRoutePersistenceRuntime? runtime)
    {
        lock (Gate)
        {
            PhilosophyRunState sharedState = PhilosophyRunStateService.GetOrCreate(runState);
            if (ZenoRoutePersistenceRuntime.TryRecoverIsolatedSharedState(
                    sharedState,
                    catalog,
                    out PhilosophyRunState? recoveredState) &&
                recoveredState is not null)
            {
                PhilosophyRunStateService.Restore(runState, recoveredState);
                sharedState = recoveredState;
            }

            if (Runtimes.TryGetValue(runState, out ZenoRoutePersistenceRuntime? existing))
            {
                if (existing.Owns(sharedState, catalog))
                {
                    runtime = existing;
                    return true;
                }

                runtime = null;
                return false;
            }

            if (!ZenoRoutePersistenceRuntime.TryCreate(
                    sharedState,
                    catalog,
                    new ZenoRouteGamePersistenceAdapter(runState, sharedState, catalog),
                    out runtime) ||
                runtime is null)
            {
                return false;
            }

            Runtimes.Add(runState, runtime);
            return true;
        }
    }

    internal static void Remove(RunState runState)
    {
        lock (Gate)
        {
            Runtimes.Remove(runState);
        }
    }
}
