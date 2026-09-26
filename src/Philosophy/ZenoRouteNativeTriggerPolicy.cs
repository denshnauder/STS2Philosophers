namespace STS2Philosophers;

internal sealed record ZenoRouteNativeMapBoundary(
    int ActIndex,
    int RoomCount,
    int? RoomId,
    bool IsPreFinished,
    bool IsMapRoom,
    ZenoRouteObservedEventKind EventKind);

internal sealed record ZenoRouteNativeTriggerPlan(
    ZenoRouteSchedulingContext Context,
    ZenoRouteObservedDestination Destination);

internal static class ZenoRouteNativeTriggerPolicy
{
    public static bool TryCreateMapPlan(
        ZenoRouteNativeMapBoundary boundary,
        out ZenoRouteNativeTriggerPlan? plan)
    {
        plan = null;
        if (boundary.ActIndex != 2 ||
            boundary.RoomCount != 1 ||
            boundary.RoomId is not { } roomId ||
            roomId < 0 ||
            boundary.IsMapRoom ||
            !Enum.IsDefined(boundary.EventKind) ||
            boundary.EventKind == ZenoRouteObservedEventKind.Zeno)
        {
            return false;
        }

        string destinationId = $"MAP_ACT_{boundary.ActIndex}_ROOM_{roomId}";
        string? receiptId = boundary.IsPreFinished &&
                            boundary.EventKind is not ZenoRouteObservedEventKind.Diogenes
            ? $"COMPLETED_ACT_{boundary.ActIndex}_ROOM_{roomId}"
            : null;
        plan = new ZenoRouteNativeTriggerPlan(
            new ZenoRouteSchedulingContext(
                CompletedRoomReceiptId: receiptId,
                SafeBoundaryDestinationId: destinationId),
            new ZenoRouteObservedDestination(
                ZenoResumeDestination.Map,
                destinationId));
        return true;
    }

    public static bool TryCreateBossPlan(
        int actIndex,
        int roomCount,
        int row,
        int column,
        out ZenoRouteNativeTriggerPlan? plan)
    {
        plan = null;
        if (actIndex != 2 || roomCount != 1 || row < 0 || column < 0)
        {
            return false;
        }

        string destinationId = $"BOSS_ACT_{actIndex}_ROW_{row}_COL_{column}";
        plan = new ZenoRouteNativeTriggerPlan(
            new ZenoRouteSchedulingContext(BossDestinationId: destinationId),
            new ZenoRouteObservedDestination(
                ZenoResumeDestination.Boss,
                destinationId));
        return true;
    }

    public static bool TryCreateActExitPlan(
        int actIndex,
        int roomCount,
        int? roomId,
        out ZenoRouteNativeTriggerPlan? plan)
    {
        plan = null;
        if (actIndex != 2 || roomCount != 1 || roomId is not { } currentRoomId || currentRoomId < 0)
        {
            return false;
        }

        string destinationId = $"ACT_EXIT_{actIndex}_ROOM_{currentRoomId}";
        plan = new ZenoRouteNativeTriggerPlan(
            new ZenoRouteSchedulingContext(ActExitDestinationId: destinationId),
            new ZenoRouteObservedDestination(
                ZenoResumeDestination.ActExit,
                destinationId));
        return true;
    }
}
