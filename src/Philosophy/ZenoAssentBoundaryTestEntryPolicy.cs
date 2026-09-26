namespace STS2Philosophers;

internal sealed record ZenoAssentBoundaryTestEntryPlan(
    ZenoRouteFeatureState InitialState,
    ZenoRouteValidationCatalog Catalog,
    ZenoAssentMaterialSnapshot Material,
    ZenoRouteNativeTriggerPlan TriggerPlan);

internal static class ZenoAssentBoundaryTestEntryPolicy
{
    internal const string SourceKind = "DEVELOPMENT_TEST_ENTRY";

    public static bool TryCreatePlan(
        ulong runSeed,
        ZenoRoutePayloadClassification currentClassification,
        bool hasPreservedMarkers,
        ZenoRouteNativeMapBoundary boundary,
        out ZenoAssentBoundaryTestEntryPlan? plan)
    {
        plan = null;
        if (currentClassification != ZenoRoutePayloadClassification.PreFeatureLegacy ||
            hasPreservedMarkers ||
            !ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(boundary, out ZenoRouteNativeTriggerPlan? triggerPlan) ||
            triggerPlan?.Context.CompletedRoomReceiptId is null)
        {
            return false;
        }

        ZenoRouteValidationCatalog catalog = new(
            [new KeyValuePair<string, int>(SourceKind, 1)],
            [],
            [],
            []);
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            null,
            true,
            [],
            null,
            null,
            null);
        ZenoRouteFeatureState initialState = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                $"DEVELOPMENT_ZENO_{runSeed}",
                2,
                ZenoRouteIds.RouteEdge,
                ZenoRouteIds.TerminalNode,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
        if (!ZenoRouteStateCodec.IsValidFeature(initialState, catalog))
        {
            return false;
        }

        plan = new ZenoAssentBoundaryTestEntryPlan(
            initialState,
            catalog,
            material,
            triggerPlan);
        return true;
    }
}
