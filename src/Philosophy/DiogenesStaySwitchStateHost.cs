namespace STS2Philosophers;

internal interface IDiogenesStaySwitchStateHost
{
    ZenoRouteFeatureState CurrentState { get; }

    Task<bool> CommitStayAsync();

    Task<bool> CommitSwitchAsync();

    Task<bool> CloseAndResumeAsync();
}

internal sealed class DiogenesStaySwitchStateHost(
    ZenoRoutePersistenceRuntime runtime,
    ZenoRouteValidationCatalog catalog,
    ZenoAssentMaterialSnapshot switchMaterial,
    Func<CancellationToken, Task<bool>>? closeAndResume = null) : IDiogenesStaySwitchStateHost
{
    public ZenoRouteFeatureState CurrentState => runtime.CurrentState;

    public Task<bool> CommitStayAsync() =>
        CommitAsync(ZenoRouteOperationKind.Stay);

    public Task<bool> CommitSwitchAsync() =>
        CommitAsync(ZenoRouteOperationKind.Switch);

    public Task<bool> CloseAndResumeAsync() =>
        closeAndResume is null
            ? Task.FromResult(false)
            : closeAndResume(CancellationToken.None);

    private async Task<bool> CommitAsync(ZenoRouteOperationKind kind)
    {
        ZenoRouteFeatureState current = runtime.CurrentState;
        if (current.Route is not { } route)
        {
            return false;
        }

        ZenoRouteStage committedStage = kind == ZenoRouteOperationKind.Stay
            ? ZenoRouteStage.DiogenesClosed
            : ZenoRouteStage.WaitingInterval;
        if (route.Stage == committedStage)
        {
            return current.PendingOperation is null;
        }

        if (route.Stage != ZenoRouteStage.Unresolved || current.PendingOperation is not null)
        {
            return false;
        }

        ZenoRouteTransitionResult prepared = kind == ZenoRouteOperationKind.Stay
            ? ZenoRouteStateService.PrepareStay(current, route.Revision, catalog)
            : ZenoRouteStateService.PrepareSwitch(
                current,
                route.Revision,
                switchMaterial,
                catalog);
        if (!prepared.IsAccepted)
        {
            return false;
        }

        ZenoRoutePersistenceCoordinationResult result = await runtime.ExecuteAsync(prepared);
        ZenoRouteFeatureState latest = runtime.CurrentState;
        return result.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
            latest.PendingOperation is null &&
            latest.Route?.Stage == committedStage;
    }
}
