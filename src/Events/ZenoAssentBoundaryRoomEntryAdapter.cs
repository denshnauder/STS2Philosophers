using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal sealed class ZenoAssentBoundaryRoomEntryAdapter
{
    private readonly ZenoAssentBoundaryRoomEntry<ZenoAssentBoundary> _entry;

    public ZenoAssentBoundaryRoomEntryAdapter(
        RunManager runManager,
        RunState runState,
        ZenoRoutePersistenceRuntime runtime,
        ZenoRouteValidationCatalog catalog,
        IZenoRouteRoomDestinationReader destinationReader)
    {
        _entry = new ZenoAssentBoundaryRoomEntry<ZenoAssentBoundary>(
            runtime,
            catalog,
            new GameGateway(
                runManager,
                runState,
                runtime,
                catalog,
                destinationReader),
            routeEvent =>
                ((IZenoRouteEventSceneIdentity)routeEvent).RouteEventInstanceId);
    }

    public Task<ZenoAssentBoundaryRoomEntryStatus> EnterAsync(
        ZenoAssentBoundaryEstablishmentResult<ZenoAssentBoundary> establishment,
        CancellationToken cancellationToken = default) =>
        _entry.EnterAsync(establishment, cancellationToken);

    private sealed class GameGateway(
        RunManager runManager,
        RunState runState,
        ZenoRoutePersistenceRuntime runtime,
        ZenoRouteValidationCatalog catalog,
        IZenoRouteRoomDestinationReader destinationReader)
        : IZenoAssentBoundaryRoomEntryGateway<ZenoAssentBoundary>
    {
        private readonly ZenoRouteRunSceneProbe _sceneProbe =
            new(runState, destinationReader);

        public ZenoRouteRoomStackSnapshot ReadSnapshot()
        {
            if (!runManager.IsInProgress ||
                !ReferenceEquals(runManager.DebugOnlyGetState(), runState))
            {
                return new ZenoRouteRoomStackSnapshot(
                    false,
                    0,
                    new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.None),
                    new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.None),
                    false);
            }

            return _sceneProbe.ReadSnapshot();
        }

        public async Task EnterAsync(
            ZenoAssentBoundary routeEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string eventInstanceId =
                ((IZenoRouteEventSceneIdentity)routeEvent).RouteEventInstanceId ??
                throw new InvalidOperationException(
                    "The ready Zeno event must retain its persisted instance identity.");

            EventRoom eventRoom = new(routeEvent)
            {
                OnStart = mutableEvent =>
                {
                    if (mutableEvent is not ZenoAssentBoundary localEvent)
                    {
                        throw new InvalidOperationException(
                            "The native event room created an unexpected local event model.");
                    }

                    localEvent.Configure(
                        eventInstanceId,
                        new ZenoAssentBoundaryStateHost(runtime, catalog, eventInstanceId),
                        catalog);
                },
            };
            await runManager.EnterRoomWithoutExitingCurrentRoom(
                eventRoom,
                fadeToBlack: true);
        }
    }
}
