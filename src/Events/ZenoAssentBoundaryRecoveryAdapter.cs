using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal sealed class ZenoAssentBoundaryRecoveryAdapter(RunState runState)
    : IZenoRouteRecoveryActionExecutor
{
    public Task<bool> ExecuteAsync(
        ZenoRouteRecoveryPlan plan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool restored = runState.CurrentRoom is EventRoom eventRoom &&
            eventRoom.LocalMutableEvent is ZenoAssentBoundary zenoEvent &&
            zenoEvent.RestoreFromCurrentState(plan);
        return Task.FromResult(restored);
    }
}
