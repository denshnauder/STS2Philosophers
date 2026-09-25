namespace STS2Philosophers;

internal enum ZenoAssentBoundaryRoomEntryDecision
{
    Enter,
    AlreadyEntered,
    Reject,
}

internal enum ZenoAssentBoundaryRoomEntryStatus
{
    Entered,
    AlreadyEntered,
    Rejected,
    Failed,
}

internal interface IZenoAssentBoundaryRoomEntryGateway<in TEvent>
    where TEvent : class
{
    ZenoRouteRoomStackSnapshot ReadSnapshot();

    Task EnterAsync(TEvent routeEvent, CancellationToken cancellationToken = default);
}

internal static class ZenoAssentBoundaryRoomEntryPolicy
{
    public static ZenoAssentBoundaryRoomEntryDecision Decide(
        ZenoRouteFeatureState state,
        string? pendingRequestId,
        string eventInstanceId,
        ZenoRouteValidationCatalog catalog,
        ZenoRouteRoomStackSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(eventInstanceId) ||
            pendingRequestId is not null ||
            state.PendingOperation is not null ||
            state.Route is not { Stage: ZenoRouteStage.EventActive } route ||
            !string.Equals(route.EventInstanceId, eventInstanceId, StringComparison.Ordinal) ||
            !ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return ZenoAssentBoundaryRoomEntryDecision.Reject;
        }

        ZenoRouteRecoveryScene scene = ZenoRouteRecoverySceneClassifier.Classify(
            state,
            snapshot);
        if (snapshot.RoomCount == 1 &&
            snapshot.CurrentIsBaseRoom &&
            snapshot.CurrentRoom.EventKind == ZenoRouteObservedEventKind.None &&
            scene.EventPresence == ZenoRouteEventPresence.None &&
            scene.DestinationPresence == ZenoRouteDestinationPresence.Matching)
        {
            return ZenoAssentBoundaryRoomEntryDecision.Enter;
        }

        if (snapshot.RoomCount == 2 &&
            !snapshot.CurrentIsBaseRoom &&
            snapshot.CurrentRoom.EventKind == ZenoRouteObservedEventKind.Zeno &&
            string.Equals(
                snapshot.CurrentRoom.EventInstanceId,
                eventInstanceId,
                StringComparison.Ordinal) &&
            snapshot.BaseRoom.EventKind == ZenoRouteObservedEventKind.None &&
            scene.EventPresence == ZenoRouteEventPresence.MatchingZeno &&
            scene.DestinationPresence == ZenoRouteDestinationPresence.Pending)
        {
            return ZenoAssentBoundaryRoomEntryDecision.AlreadyEntered;
        }

        return ZenoAssentBoundaryRoomEntryDecision.Reject;
    }
}

internal sealed class ZenoAssentBoundaryRoomEntry<TEvent>(
    ZenoRoutePersistenceRuntime runtime,
    ZenoRouteValidationCatalog catalog,
    IZenoAssentBoundaryRoomEntryGateway<TEvent> gateway,
    Func<TEvent, string?> readEventInstanceId)
    where TEvent : class
{
    public Task<ZenoAssentBoundaryRoomEntryStatus> EnterAsync(
        ZenoAssentBoundaryEstablishmentResult<TEvent> establishment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(establishment);

        if (!runtime.Owns(catalog) ||
            !establishment.IsReady ||
            establishment.RouteEvent is not { } routeEvent ||
            readEventInstanceId(routeEvent) is not { } eventInstanceId ||
            string.IsNullOrWhiteSpace(eventInstanceId))
        {
            return Task.FromResult(ZenoAssentBoundaryRoomEntryStatus.Rejected);
        }

        return runtime.ExecuteExclusiveAsync(
            (state, pendingRequestId, token) => EnterLockedAsync(
                state,
                pendingRequestId,
                routeEvent,
                eventInstanceId,
                token),
            cancellationToken);
    }

    private async Task<ZenoAssentBoundaryRoomEntryStatus> EnterLockedAsync(
        ZenoRouteFeatureState state,
        string? pendingRequestId,
        TEvent routeEvent,
        string eventInstanceId,
        CancellationToken cancellationToken)
    {
        ZenoAssentBoundaryRoomEntryDecision before;
        try
        {
            before = ZenoAssentBoundaryRoomEntryPolicy.Decide(
                state,
                pendingRequestId,
                eventInstanceId,
                catalog,
                gateway.ReadSnapshot());
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ZenoAssentBoundaryRoomEntryStatus.Failed;
        }

        if (before == ZenoAssentBoundaryRoomEntryDecision.AlreadyEntered)
        {
            return ZenoAssentBoundaryRoomEntryStatus.AlreadyEntered;
        }

        if (before != ZenoAssentBoundaryRoomEntryDecision.Enter)
        {
            return ZenoAssentBoundaryRoomEntryStatus.Rejected;
        }

        try
        {
            await gateway.EnterAsync(routeEvent, cancellationToken);
            return ZenoAssentBoundaryRoomEntryPolicy.Decide(
                    runtime.CurrentState,
                    runtime.PendingRequestId,
                    eventInstanceId,
                    catalog,
                    gateway.ReadSnapshot()) == ZenoAssentBoundaryRoomEntryDecision.AlreadyEntered
                ? ZenoAssentBoundaryRoomEntryStatus.Entered
                : ZenoAssentBoundaryRoomEntryStatus.Failed;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ZenoAssentBoundaryRoomEntryStatus.Failed;
        }
    }
}
