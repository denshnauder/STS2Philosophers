namespace STS2Philosophers;

internal enum ZenoRouteObservedEventKind
{
    None,
    Unrelated,
    Diogenes,
    Zeno,
}

internal sealed record ZenoRouteObservedDestination(
    ZenoResumeDestination Kind,
    string Id);

internal sealed record ZenoRouteObservedRoom(
    ZenoRouteObservedEventKind EventKind,
    string? EventInstanceId = null,
    ZenoRouteObservedDestination? Destination = null);

internal sealed record ZenoRouteRoomStackSnapshot(
    bool IsRunTerminated,
    int RoomCount,
    ZenoRouteObservedRoom CurrentRoom,
    ZenoRouteObservedRoom BaseRoom,
    bool CurrentIsBaseRoom);

internal static class ZenoRouteRecoverySceneClassifier
{
    public static ZenoRouteRecoveryScene Classify(
        ZenoRouteFeatureState state,
        ZenoRouteRoomStackSnapshot snapshot)
    {
        ZenoRouteState? effectiveRoute = state.PendingOperation?.Candidate ?? state.Route;
        if (effectiveRoute is null || snapshot.RoomCount <= 0 || snapshot.RoomCount > 2 ||
            !Enum.IsDefined(snapshot.CurrentRoom.EventKind) ||
            !Enum.IsDefined(snapshot.BaseRoom.EventKind))
        {
            return Conflict();
        }

        if (snapshot.IsRunTerminated)
        {
            return effectiveRoute.Stage == ZenoRouteStage.RunTerminated
                ? new ZenoRouteRecoveryScene(
                    ZenoRouteEventPresence.None,
                    ZenoRouteDestinationPresence.NotApplicable)
                : Conflict();
        }

        IReadOnlyList<ZenoRouteObservedRoom> rooms = snapshot.CurrentIsBaseRoom
            ? [snapshot.CurrentRoom]
            : [snapshot.CurrentRoom, snapshot.BaseRoom];
        bool closingPending = state.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed;
        ZenoRouteEventPresence eventPresence = ClassifyEvent(effectiveRoute, rooms, closingPending);
        if (eventPresence == ZenoRouteEventPresence.Conflicting)
        {
            return Conflict();
        }

        ZenoRouteDestinationPresence destinationPresence =
            ClassifyDestination(effectiveRoute, eventPresence, rooms, closingPending);
        return new ZenoRouteRecoveryScene(eventPresence, destinationPresence);
    }

    private static ZenoRouteEventPresence ClassifyEvent(
        ZenoRouteState route,
        IReadOnlyList<ZenoRouteObservedRoom> rooms,
        bool closingPending)
    {
        ZenoRouteObservedRoom[] routeEvents = rooms
            .Where(room => room.EventKind is ZenoRouteObservedEventKind.Diogenes or ZenoRouteObservedEventKind.Zeno)
            .ToArray();
        bool hasUnrelatedEvent = rooms.Any(room => room.EventKind == ZenoRouteObservedEventKind.Unrelated);
        bool requiresPausedZeno = route.Stage is
            ZenoRouteStage.OpeningClaimed or
            ZenoRouteStage.EventActive or
            ZenoRouteStage.OutcomeCommitted || closingPending;

        if (routeEvents.Length > 1 || (hasUnrelatedEvent && requiresPausedZeno))
        {
            return ZenoRouteEventPresence.Conflicting;
        }

        if (routeEvents.Length == 0)
        {
            return ZenoRouteEventPresence.None;
        }

        ZenoRouteObservedRoom observed = routeEvents[0];
        return observed.EventKind switch
        {
            ZenoRouteObservedEventKind.Diogenes => ZenoRouteEventPresence.Diogenes,
            ZenoRouteObservedEventKind.Zeno when
                route.EventInstanceId is not null &&
                string.Equals(
                    observed.EventInstanceId,
                    route.EventInstanceId,
                    StringComparison.Ordinal) => ZenoRouteEventPresence.MatchingZeno,
            _ => ZenoRouteEventPresence.Conflicting,
        };
    }

    private static ZenoRouteDestinationPresence ClassifyDestination(
        ZenoRouteState route,
        ZenoRouteEventPresence eventPresence,
        IReadOnlyList<ZenoRouteObservedRoom> rooms,
        bool closingPending)
    {
        if (route.ResumeDestination is null || route.ResumeDestinationId is null)
        {
            return ZenoRouteDestinationPresence.NotApplicable;
        }

        if (route.Stage == ZenoRouteStage.ZenoClosed && !closingPending)
        {
            return ZenoRouteDestinationPresence.Matching;
        }

        if (eventPresence == ZenoRouteEventPresence.MatchingZeno)
        {
            return ZenoRouteDestinationPresence.Pending;
        }

        ZenoRouteObservedDestination[] observed = rooms
            .Where(room => room.Destination is not null)
            .Select(room => room.Destination!)
            .Distinct()
            .ToArray();
        if (observed.Any(destination =>
                destination.Kind == route.ResumeDestination &&
                string.Equals(destination.Id, route.ResumeDestinationId, StringComparison.Ordinal)))
        {
            return ZenoRouteDestinationPresence.Matching;
        }

        return observed.Length == 0
            ? ZenoRouteDestinationPresence.Pending
            : ZenoRouteDestinationPresence.Conflicting;
    }

    private static ZenoRouteRecoveryScene Conflict() =>
        new(
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
}
