namespace STS2Philosophers;

internal interface IZenoAssentBoundaryStateHost
{
    ZenoRouteFeatureState CurrentState { get; }

    Task<bool> CommitOutcomeAsync(ZenoAssentOutcome outcome);
}

internal sealed class ZenoAssentBoundaryStateHost(
    ZenoRoutePersistenceRuntime runtime,
    ZenoRouteValidationCatalog catalog,
    string eventInstanceId) : IZenoAssentBoundaryStateHost
{
    public ZenoRouteFeatureState CurrentState => runtime.CurrentState;

    public async Task<bool> CommitOutcomeAsync(ZenoAssentOutcome outcome)
    {
        ZenoRouteFeatureState current = runtime.CurrentState;
        if (current.Route is not { } route ||
            string.IsNullOrWhiteSpace(eventInstanceId) ||
            !string.Equals(route.EventInstanceId, eventInstanceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (route.Stage == ZenoRouteStage.OutcomeCommitted)
        {
            return current.PendingOperation is null && route.Outcome == outcome;
        }

        if (route.Stage != ZenoRouteStage.EventActive || current.PendingOperation is not null)
        {
            return false;
        }

        string? currentStatementAfterId = outcome switch
        {
            ZenoAssentOutcome.Keep => route.Material?.CurrentStatementId,
            ZenoAssentOutcome.Narrow => route.Material?.NarrowStatementId,
            ZenoAssentOutcome.Withdraw or
            ZenoAssentOutcome.NoReassent or
            ZenoAssentOutcome.NoAssent => null,
            _ => null,
        };
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareOutcome(
            current,
            route.Revision,
            outcome,
            currentStatementAfterId,
            catalog);
        if (!prepared.IsAccepted)
        {
            return false;
        }

        ZenoRoutePersistenceCoordinationResult result = await runtime.ExecuteAsync(prepared);
        ZenoRouteFeatureState latest = runtime.CurrentState;
        ZenoRouteState? committed = latest.Route;
        return result.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
            latest.PendingOperation is null &&
            committed?.Stage == ZenoRouteStage.OutcomeCommitted &&
            committed.Outcome == outcome &&
            string.Equals(committed.EventInstanceId, eventInstanceId, StringComparison.Ordinal);
    }
}
