namespace STS2Philosophers;

internal interface IZenoRouteRecoverySceneProbe
{
    Task<ZenoRouteRecoveryScene> ReadAsync(
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken = default);
}

internal interface IZenoRouteRecoveryActionExecutor
{
    Task<bool> ExecuteAsync(
        ZenoRouteRecoveryPlan plan,
        CancellationToken cancellationToken = default);
}

internal enum ZenoRouteRecoveryExecutionStatus
{
    Completed,
    Isolated,
    Frozen,
    PreparationReleased,
    PersistenceRejected,
    ExternalEffectBlocked,
    SceneUnavailable,
    ActionFailed,
}

internal sealed record ZenoRouteRecoveryExecutionResult(
    ZenoRouteRecoveryExecutionStatus Status,
    ZenoRouteRecoveryPlan? Plan,
    ZenoRoutePersistenceCoordinationResult? PersistenceResult = null);
