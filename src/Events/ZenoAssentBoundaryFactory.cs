using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

internal sealed class ZenoAssentBoundaryFactory(
    ZenoRoutePersistenceRuntime runtime,
    ZenoRouteValidationCatalog catalog)
{
    private readonly ZenoAssentBoundaryCreationCache<ZenoAssentBoundary> _cache = new();

    public bool TryGetOrCreate(out ZenoAssentBoundary? routeEvent)
    {
        routeEvent = null;
        if (!runtime.Owns(catalog) ||
            !ZenoAssentBoundaryCreationPolicy.TryCreatePlan(
                runtime.CurrentState,
                catalog,
                out ZenoAssentBoundaryCreationPlan? plan) ||
            plan is null)
        {
            return false;
        }

        return _cache.TryGetOrCreate(
            plan,
            () => CreateConfiguredEvent(plan),
            out routeEvent);
    }

    public Task<ZenoAssentBoundaryEstablishmentResult<ZenoAssentBoundary>> EstablishAsync(
        CancellationToken cancellationToken = default)
    {
        if (runtime.CurrentState is
            {
                PendingOperation: null,
                Route: { Stage: ZenoRouteStage.EventActive, EventInstanceId: { } eventInstanceId },
            } &&
            _cache.TryGetOrCreate(
                new ZenoAssentBoundaryCreationPlan(eventInstanceId),
                () => CreateConfiguredEvent(new ZenoAssentBoundaryCreationPlan(eventInstanceId)),
                out ZenoAssentBoundary? restored) &&
            restored is not null)
        {
            return Task.FromResult(
                new ZenoAssentBoundaryEstablishmentResult<ZenoAssentBoundary>(
                    ZenoAssentBoundaryEstablishmentStatus.Ready,
                    restored));
        }

        return ZenoAssentBoundaryEstablishment.ExecuteAsync(
            runtime,
            catalog,
            _cache,
            CreateConfiguredEvent,
            cancellationToken);
    }

    private ZenoAssentBoundary CreateConfiguredEvent(ZenoAssentBoundaryCreationPlan plan)
    {
        ZenoAssentBoundary routeEvent =
            (ZenoAssentBoundary)ModelDb.Event<ZenoAssentBoundary>().ToMutable();
        routeEvent.Configure(
            plan.EventInstanceId,
            new ZenoAssentBoundaryStateHost(runtime, catalog, plan.EventInstanceId),
            catalog);
        return routeEvent;
    }
}
