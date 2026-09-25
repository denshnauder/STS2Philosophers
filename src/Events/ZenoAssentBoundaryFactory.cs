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
