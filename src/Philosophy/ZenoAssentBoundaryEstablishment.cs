namespace STS2Philosophers;

internal enum ZenoAssentBoundaryEstablishmentStatus
{
    Ready,
    PreparationReleased,
    Frozen,
    Rejected,
}

internal sealed record ZenoAssentBoundaryEstablishmentResult<TEvent>(
    ZenoAssentBoundaryEstablishmentStatus Status,
    TEvent? RouteEvent)
    where TEvent : class
{
    public bool IsReady => Status == ZenoAssentBoundaryEstablishmentStatus.Ready && RouteEvent is not null;
}

internal static class ZenoAssentBoundaryEstablishment
{
    public static async Task<ZenoAssentBoundaryEstablishmentResult<TEvent>> ExecuteAsync<TEvent>(
        ZenoRoutePersistenceRuntime runtime,
        ZenoRouteValidationCatalog catalog,
        ZenoAssentBoundaryCreationCache<TEvent> cache,
        Func<ZenoAssentBoundaryCreationPlan, TEvent> create,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(create);

        if (!runtime.Owns(catalog))
        {
            return Result<TEvent>(ZenoAssentBoundaryEstablishmentStatus.Rejected);
        }

        ZenoRouteFeatureState current = runtime.CurrentState;
        if (current.PendingOperation is not null || runtime.PendingRequestId is not null)
        {
            return Result<TEvent>(
                current.PendingOperation?.Kind == ZenoRouteOperationKind.EventEstablished ||
                current.Route?.Stage == ZenoRouteStage.EventActive
                    ? ZenoAssentBoundaryEstablishmentStatus.Frozen
                    : ZenoAssentBoundaryEstablishmentStatus.Rejected);
        }

        if (current.Route is { Stage: ZenoRouteStage.EventActive } active)
        {
            return active.EventInstanceId is not null &&
                   cache.TryGet(active.EventInstanceId, out TEvent? existing)
                ? new ZenoAssentBoundaryEstablishmentResult<TEvent>(
                    ZenoAssentBoundaryEstablishmentStatus.Ready,
                    existing)
                : Result<TEvent>(ZenoAssentBoundaryEstablishmentStatus.Rejected);
        }

        if (!ZenoAssentBoundaryCreationPolicy.TryCreatePlan(
                current,
                catalog,
                out ZenoAssentBoundaryCreationPlan? plan) ||
            plan is null ||
            !cache.TryGetOrCreate(plan, () => create(plan), out TEvent? routeEvent) ||
            routeEvent is null ||
            current.Route is not { } opening)
        {
            return Result<TEvent>(ZenoAssentBoundaryEstablishmentStatus.Rejected);
        }

        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareEventEstablished(
            current,
            opening.Revision,
            catalog);
        if (!prepared.IsAccepted)
        {
            return Result<TEvent>(ZenoAssentBoundaryEstablishmentStatus.Rejected);
        }

        ZenoRoutePersistenceCoordinationResult persistence = await runtime.ExecuteAsync(
            prepared,
            cancellationToken);
        ZenoRouteFeatureState latest = runtime.CurrentState;
        if (persistence.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
            runtime.PendingRequestId is null &&
            latest.PendingOperation is null &&
            latest.Route is { Stage: ZenoRouteStage.EventActive } established &&
            string.Equals(
                established.EventInstanceId,
                plan.EventInstanceId,
                StringComparison.Ordinal))
        {
            return new ZenoAssentBoundaryEstablishmentResult<TEvent>(
                ZenoAssentBoundaryEstablishmentStatus.Ready,
                routeEvent);
        }

        return Result<TEvent>(persistence.Status switch
        {
            ZenoRoutePersistenceCoordinationStatus.PreparationReleased =>
                ZenoAssentBoundaryEstablishmentStatus.PreparationReleased,
            ZenoRoutePersistenceCoordinationStatus.Frozen =>
                ZenoAssentBoundaryEstablishmentStatus.Frozen,
            _ => ZenoAssentBoundaryEstablishmentStatus.Rejected,
        });
    }

    private static ZenoAssentBoundaryEstablishmentResult<TEvent> Result<TEvent>(
        ZenoAssentBoundaryEstablishmentStatus status)
        where TEvent : class =>
        new(status, null);
}
