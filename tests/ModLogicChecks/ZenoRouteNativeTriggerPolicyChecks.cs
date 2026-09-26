using STS2Philosophers;

internal static class ZenoRouteNativeTriggerPolicyChecks
{
    public static void Run()
    {
        ImmediateDiogenesReturnDoesNotCompleteTheInterval();
        FinishedNonRouteRoomCreatesOneStableReceipt();
        UnsafeMapScenesAreRejected();
        BossAndActExitUseDistinctFrozenDestinations();
        Console.WriteLine("Zeno native trigger policy checks passed: safe map receipts and frozen fallback destinations.");
    }

    private static void ImmediateDiogenesReturnDoesNotCompleteTheInterval()
    {
        Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                new ZenoRouteNativeMapBoundary(
                    2,
                    1,
                    41,
                    true,
                    false,
                    ZenoRouteObservedEventKind.Diogenes),
                out ZenoRouteNativeTriggerPlan? plan) &&
               plan is { } &&
               plan.Context.CompletedRoomReceiptId is null &&
               plan.Context.SafeBoundaryDestinationId == "MAP_ACT_2_ROOM_41",
            "Returning from the Switch event may expose the map destination but must not count as the later room.");
    }

    private static void FinishedNonRouteRoomCreatesOneStableReceipt()
    {
        ZenoRouteNativeMapBoundary combat = new(
            2,
            1,
            42,
            true,
            false,
            ZenoRouteObservedEventKind.None);
        Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(combat, out ZenoRouteNativeTriggerPlan? first) &&
               ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(combat, out ZenoRouteNativeTriggerPlan? repeated) &&
               first == repeated &&
               first?.Context.CompletedRoomReceiptId == "COMPLETED_ACT_2_ROOM_42" &&
               first.Destination.Kind == ZenoResumeDestination.Map,
            "A finished non-route room must produce one deterministic receipt and map destination.");

        Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                combat with
                {
                    RoomId = 43,
                    EventKind = ZenoRouteObservedEventKind.Unrelated,
                },
                out ZenoRouteNativeTriggerPlan? unrelatedEvent) &&
               unrelatedEvent?.Context.CompletedRoomReceiptId == "COMPLETED_ACT_2_ROOM_43",
            "A completed unrelated event may satisfy the interval without becoming a route event.");
    }

    private static void UnsafeMapScenesAreRejected()
    {
        ZenoRouteNativeMapBoundary valid = new(
            2,
            1,
            44,
            true,
            false,
            ZenoRouteObservedEventKind.None);
        ZenoRouteNativeMapBoundary[] invalid =
        [
            valid with { ActIndex = 1 },
            valid with { RoomCount = 2 },
            valid with { RoomId = null },
            valid with { IsMapRoom = true },
            valid with { EventKind = ZenoRouteObservedEventKind.Zeno },
        ];
        foreach (ZenoRouteNativeMapBoundary boundary in invalid)
        {
            Assert(!ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(boundary, out _),
                "Wrong acts, modal stacks, missing identities, map rooms and active Zeno rooms must not start an ordinary trigger.");
        }

        Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                valid with { IsPreFinished = false },
                out ZenoRouteNativeTriggerPlan? unfinished) &&
               unfinished?.Context.CompletedRoomReceiptId is null,
            "An unfinished room may expose a map screen but must not satisfy the completed-room interval.");
    }

    private static void BossAndActExitUseDistinctFrozenDestinations()
    {
        Assert(ZenoRouteNativeTriggerPolicy.TryCreateBossPlan(
                2,
                1,
                16,
                3,
                out ZenoRouteNativeTriggerPlan? boss) &&
               boss?.Context.BossDestinationId == "BOSS_ACT_2_ROW_16_COL_3" &&
               boss.Destination.Kind == ZenoResumeDestination.Boss,
            "The Boss gate must freeze the exact selected coordinate before native travel mutates it.");
        Assert(ZenoRouteNativeTriggerPolicy.TryCreateActExitPlan(
                2,
                1,
                45,
                out ZenoRouteNativeTriggerPlan? actExit) &&
               actExit?.Context.ActExitDestinationId == "ACT_EXIT_2_ROOM_45" &&
               actExit.Destination.Kind == ZenoResumeDestination.ActExit,
            "The act-exit gate must freeze a distinct destination before EnterNextAct commits it.");
        Assert(!ZenoRouteNativeTriggerPolicy.TryCreateBossPlan(2, 2, 16, 3, out _) &&
               !ZenoRouteNativeTriggerPolicy.TryCreateActExitPlan(1, 1, 45, out _),
            "Fallback gates must not run under another modal room or outside the approved act.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
