namespace STS2Philosophers;

internal sealed record ZenoAssentBoundaryCreationPlan(string EventInstanceId);

internal static class ZenoAssentBoundaryCreationPolicy
{
    public static bool TryCreatePlan(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog,
        out ZenoAssentBoundaryCreationPlan? plan)
    {
        plan = null;
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) ||
            state.PendingOperation is not null ||
            state.Route is not { Stage: ZenoRouteStage.OpeningClaimed } route ||
            string.IsNullOrWhiteSpace(route.EventInstanceId))
        {
            return false;
        }

        plan = new ZenoAssentBoundaryCreationPlan(route.EventInstanceId);
        return true;
    }
}

internal sealed class ZenoAssentBoundaryCreationCache<TEvent>
    where TEvent : class
{
    private string? _eventInstanceId;
    private TEvent? _event;

    public bool TryGetOrCreate(
        ZenoAssentBoundaryCreationPlan plan,
        Func<TEvent> create,
        out TEvent? routeEvent)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(create);

        if (_event is not null)
        {
            routeEvent = string.Equals(
                    _eventInstanceId,
                    plan.EventInstanceId,
                    StringComparison.Ordinal)
                ? _event
                : null;
            return routeEvent is not null;
        }

        routeEvent = create() ?? throw new InvalidOperationException(
            "The Zeno event model factory returned no mutable event instance.");
        _eventInstanceId = plan.EventInstanceId;
        _event = routeEvent;
        return true;
    }
}
