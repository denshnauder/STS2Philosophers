using STS2Philosophers;

internal static class ZenoAssentBoundaryTestEntryPolicyChecks
{
    public static void Run()
    {
        CreatesOnlyAnExplicitNoMaterialDevelopmentRoute();
        RejectsUnsafeOrExistingRoutes();
        Console.WriteLine("Zeno development entry checks passed: Act 3 safe boundary, explicit NoMaterial and no route overwrite.");
    }

    private static void CreatesOnlyAnExplicitNoMaterialDevelopmentRoute()
    {
        Assert(ZenoAssentBoundaryTestEntryPolicy.TryCreatePlan(
                123456UL,
                ZenoRoutePayloadClassification.PreFeatureLegacy,
                false,
                Boundary(),
                out ZenoAssentBoundaryTestEntryPlan? plan),
            "A clean Act 3 completed room must allow the opt-in development entry.");
        Assert(plan is not null &&
               plan.InitialState.Route is
               {
                   Stage: ZenoRouteStage.Unresolved,
                   RunId: "DEVELOPMENT_ZENO_123456",
                   ActIndex: 2,
                   Material: null,
               } &&
               plan.Material.SourceKind == ZenoAssentBoundaryTestEntryPolicy.SourceKind &&
               plan.Material.IsNoMaterial &&
               plan.Material.ActionFactId is null &&
               plan.Material.PublicHistoryIds.Count == 0 &&
               plan.Material.CurrentStatementId is null &&
               plan.Material.NarrowStatementId is null &&
               plan.Material.LaterOutcomeId is null &&
               ZenoRouteStateCodec.IsValidFeature(plan.InitialState, plan.Catalog) &&
               plan.TriggerPlan.Context.CompletedRoomReceiptId == "COMPLETED_ACT_2_ROOM_17",
            "The development entry must use one valid NoMaterial record and the real safe-boundary receipt.");
    }

    private static void RejectsUnsafeOrExistingRoutes()
    {
        AssertRejected(ZenoRoutePayloadClassification.Current, false, Boundary());
        AssertRejected(ZenoRoutePayloadClassification.PreFeatureLegacy, true, Boundary());
        AssertRejected(
            ZenoRoutePayloadClassification.PreFeatureLegacy,
            false,
            Boundary() with { ActIndex = 1 });
        AssertRejected(
            ZenoRoutePayloadClassification.PreFeatureLegacy,
            false,
            Boundary() with { IsPreFinished = false });
        AssertRejected(
            ZenoRoutePayloadClassification.PreFeatureLegacy,
            false,
            Boundary() with { RoomCount = 2 });
        AssertRejected(
            ZenoRoutePayloadClassification.PreFeatureLegacy,
            false,
            Boundary() with { EventKind = ZenoRouteObservedEventKind.Diogenes });
    }

    private static ZenoRouteNativeMapBoundary Boundary() =>
        new(
            2,
            1,
            17,
            true,
            false,
            ZenoRouteObservedEventKind.None);

    private static void AssertRejected(
        ZenoRoutePayloadClassification classification,
        bool hasPreservedMarkers,
        ZenoRouteNativeMapBoundary boundary)
    {
        Assert(!ZenoAssentBoundaryTestEntryPolicy.TryCreatePlan(
                123456UL,
                classification,
                hasPreservedMarkers,
                boundary,
                out _),
            "Unsafe, ambiguous or already initialized runs must not be overwritten by the development entry.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
