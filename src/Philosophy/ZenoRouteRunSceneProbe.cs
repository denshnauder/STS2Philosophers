using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal interface IZenoRouteEventSceneIdentity
{
    ZenoRouteObservedEventKind RouteEventKind { get; }
    string? RouteEventInstanceId { get; }
}

internal interface IZenoRouteRoomDestinationReader
{
    ZenoRouteObservedDestination? ReadDestination(AbstractRoom room);
}

internal sealed class ZenoRouteRunSceneProbe(
    RunState runState,
    IZenoRouteRoomDestinationReader destinationReader) : IZenoRouteRecoverySceneProbe
{
    public Task<ZenoRouteRecoveryScene> ReadAsync(
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AbstractRoom? currentRoom = runState.CurrentRoom;
        AbstractRoom? baseRoom = runState.BaseRoom;
        if (currentRoom is null || baseRoom is null)
        {
            return Task.FromResult(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Conflicting,
                ZenoRouteDestinationPresence.Conflicting));
        }

        ZenoRouteRoomStackSnapshot snapshot = new(
            runState.IsGameOver,
            runState.CurrentRoomCount,
            Observe(currentRoom),
            Observe(baseRoom),
            ReferenceEquals(currentRoom, baseRoom));
        return Task.FromResult(ZenoRouteRecoverySceneClassifier.Classify(state, snapshot));
    }

    private ZenoRouteObservedRoom Observe(AbstractRoom room)
    {
        ZenoRouteObservedDestination? destination = destinationReader.ReadDestination(room);
        if (room is not EventRoom eventRoom)
        {
            return new ZenoRouteObservedRoom(
                ZenoRouteObservedEventKind.None,
                Destination: destination);
        }

        EventModel localEvent = eventRoom.LocalMutableEvent;
        if (localEvent is not IZenoRouteEventSceneIdentity identity ||
            !Enum.IsDefined(identity.RouteEventKind) ||
            identity.RouteEventKind is ZenoRouteObservedEventKind.None or ZenoRouteObservedEventKind.Unrelated)
        {
            return new ZenoRouteObservedRoom(
                ZenoRouteObservedEventKind.Unrelated,
                Destination: destination);
        }

        return new ZenoRouteObservedRoom(
            identity.RouteEventKind,
            identity.RouteEventInstanceId,
            destination);
    }
}
