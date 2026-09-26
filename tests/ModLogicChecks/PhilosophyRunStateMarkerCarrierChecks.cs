using System.Text;

namespace STS2Philosophers;

internal static class PhilosophyRunStateMarkerCarrierChecks
{
    public static void Run()
    {
        EmptyMarkersDoNotCreateState();
        CurrentMarkerRestoresAndRewritesOnce();
        IdenticalCurrentMarkersDeduplicate();
        ConflictingMarkersRoundTripExactly();
        UnknownVersionMarkersRoundTripExactly();
        MalformedCurrentMarkerRoundTripsExactly();
        InvalidZenoChildRoundTripsExactly();
        DevelopmentZenoMarkerRestoresWithBuiltInCatalog();

        Console.WriteLine("Philosophy run state marker carrier checks passed.");
    }

    private static void EmptyMarkersDoNotCreateState()
    {
        PhilosophyRunStateMarkerRestoreResult result = PhilosophyRunStateMarkerCarrier.Restore([]);

        Assert(result.Classification == PhilosophyRunStateMarkerClassification.None,
            "No markers should classify as none.");
        Assert(result.State is null, "No markers should not create a run state.");
    }

    private static void CurrentMarkerRestoresAndRewritesOnce()
    {
        string entry = CurrentEntry(
            "{\"CurrentDoctrine\":{\"ThinkerId\":\"ZENO_OF_ELEA\",\"DoctrineId\":\"DICHOTOMY\"}}");
        PhilosophyRunStateMarkerRestoreResult result = PhilosophyRunStateMarkerCarrier.Restore([entry]);

        Assert(result.Classification == PhilosophyRunStateMarkerClassification.Current,
            "A valid current marker should restore normally.");
        Assert(result.State?.CurrentDoctrine?.ThinkerId == "ZENO_OF_ELEA",
            "The shared run state should restore from the marker.");
        IReadOnlyList<string> saved = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(result.State!);
        Assert(saved.Count == 1 && saved[0].StartsWith(PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix),
            "A restored current marker should rewrite as one current marker.");
    }

    private static void IdenticalCurrentMarkersDeduplicate()
    {
        string entry = CurrentEntry(
            "{\"CurrentDoctrine\":{\"ThinkerId\":\"ZENO_OF_ELEA\",\"DoctrineId\":\"DICHOTOMY\"}}");
        PhilosophyRunStateMarkerRestoreResult result = PhilosophyRunStateMarkerCarrier.Restore([entry, entry]);

        Assert(result.Classification == PhilosophyRunStateMarkerClassification.IdenticalCurrentDuplicates,
            "Identical valid current markers should be recognized as safe duplicates.");
        IReadOnlyList<string> saved = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(result.State!);
        Assert(saved.Count == 1, "Identical valid current markers should deduplicate on save.");
    }

    private static void ConflictingMarkersRoundTripExactly()
    {
        string first = CurrentEntry(
            "{\"CurrentDoctrine\":{\"ThinkerId\":\"ZENO_OF_ELEA\",\"DoctrineId\":\"DICHOTOMY\"}}");
        string second = CurrentEntry(
            "{\"CurrentDoctrine\":{\"ThinkerId\":\"ZENO_OF_ELEA\",\"DoctrineId\":\"ARROW\"}}");
        AssertExactIsolation(
            [first, second],
            PhilosophyRunStateMarkerClassification.Conflicting,
            "Conflicting markers");
    }

    private static void UnknownVersionMarkersRoundTripExactly()
    {
        AssertExactIsolation(
            ["V2_FUTURE", "V2_FUTURE"],
            PhilosophyRunStateMarkerClassification.UnknownVersion,
            "Unknown-version markers");
    }

    private static void MalformedCurrentMarkerRoundTripsExactly()
    {
        AssertExactIsolation(
            ["V1_NOTHEX"],
            PhilosophyRunStateMarkerClassification.InvalidCurrent,
            "Malformed current markers");
    }

    private static void InvalidZenoChildRoundTripsExactly()
    {
        string entry = CurrentEntry(
            "{\"zenoRouteFeatureGeneration\":2," +
            "\"zenoRoute\":null,\"zenoRoutePendingOperation\":null}");
        AssertExactIsolation(
            [entry],
            PhilosophyRunStateMarkerClassification.InvalidCurrent,
            "A marker with an invalid Zeno child payload");
    }

    private static void DevelopmentZenoMarkerRestoresWithBuiltInCatalog()
    {
        bool created = ZenoAssentBoundaryTestEntryPolicy.TryCreatePlan(
                66UL,
                ZenoRoutePayloadClassification.PreFeatureLegacy,
                false,
                new ZenoRouteNativeMapBoundary(
                    2,
                    1,
                    13,
                    true,
                    false,
                    ZenoRouteObservedEventKind.None),
                out ZenoAssentBoundaryTestEntryPlan? createdPlan);
        Assert(created && createdPlan is not null,
            "The development route fixture should be valid.");
        ZenoAssentBoundaryTestEntryPlan plan = createdPlan!;
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareSwitch(
            plan.InitialState,
            plan.InitialState.Route!.Revision,
            plan.Material,
            plan.Catalog);
        ZenoRoutePendingOperation pending = prepared.State.PendingOperation
            ?? throw new InvalidOperationException("The development Switch should prepare.");
        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            prepared.State,
            pending.OperationId,
            pending.CandidateDigest,
            plan.Catalog);
        PhilosophyRunState state = new();
        state.SetCurrentZenoRouteState(committed.State, plan.Catalog);
        IReadOnlyList<string> entries = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(state);

        PhilosophyRunStateMarkerRestoreResult restored =
            PhilosophyRunStateMarkerCarrier.Restore(entries);

        Assert(restored.Classification == PhilosophyRunStateMarkerClassification.Current &&
               restored.State?.ZenoRoutePayload.State?.Route is
               {
                   Stage: ZenoRouteStage.WaitingInterval,
                   Material.SourceKind: ZenoAssentBoundaryTestEntryPolicy.SourceKind,
                   Material.IsNoMaterial: true,
               },
            "A saved development route must restore before native trigger recovery runs.");
    }

    private static void AssertExactIsolation(
        IReadOnlyList<string> entries,
        PhilosophyRunStateMarkerClassification expectedClassification,
        string subject)
    {
        PhilosophyRunStateMarkerRestoreResult result = PhilosophyRunStateMarkerCarrier.Restore(entries);

        Assert(result.Classification == expectedClassification,
            $"{subject} should receive the expected isolation classification.");
        Assert(result.IsIsolated, $"{subject} should be isolated.");
        Assert(result.State is not null, $"{subject} should create a preservation state.");
        IReadOnlyList<string> saved = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(result.State!);
        Assert(saved.SequenceEqual(entries), $"{subject} should round-trip byte-for-byte and in order.");
    }

    private static string CurrentEntry(string json)
    {
        return $"{PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix}{Convert.ToHexString(Encoding.UTF8.GetBytes(json))}";
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
