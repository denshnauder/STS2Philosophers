using System.Text;
using System.Text.Json;
using STS2Philosophers;

internal static class PhilosophyRunStateZenoCodecChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement, NarrowStatement],
        []);

    public static void Run()
    {
        LegacySharedStateRemainsCompatible();
        CurrentFeatureStateUsesLockedRootProperties();
        PendingOperationRoundTripsInsideSharedState();
        UnknownAndInvalidZenoPayloadsAreIsolatedWithoutLosingOtherState();
        DuplicateAndOrphanZenoPropertiesAreRejected();
        Console.WriteLine("Shared run state Zeno checks passed: legacy compatibility, strict embedding and opaque rewrite blocking.");
    }

    private static void LegacySharedStateRemainsCompatible()
    {
        const string legacyJson = "{\"CurrentDoctrine\":{\"ThinkerId\":\"SOCRATES\",\"DoctrineId\":\"VIRTUE\"}}";
        PhilosophyRunState decoded = PhilosophyRunStateCodec.Decode(ToHex(legacyJson), Catalog);

        Assert(decoded.CurrentDoctrine?.ThinkerId == "SOCRATES", "Existing shared state should still load.");
        Assert(
            decoded.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.PreFeatureLegacy,
            "A shared state without Zeno properties should remain a pre-feature run.");
        _ = PhilosophyRunStateCodec.Encode(decoded, Catalog);
    }

    private static void CurrentFeatureStateUsesLockedRootProperties()
    {
        PhilosophyRunState state = new();
        state.SetCurrentZenoRouteState(new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, null));

        string encoded = PhilosophyRunStateCodec.Encode(state, Catalog);
        using JsonDocument document = JsonDocument.Parse(Convert.FromHexString(encoded));
        JsonElement root = document.RootElement;
        Assert(root.GetProperty("zenoRouteFeatureGeneration").GetInt32() == 1, "Shared state should use the locked feature-generation property.");
        Assert(root.GetProperty("zenoRoute").ValueKind == JsonValueKind.Null, "A current run may have no route before the third-act entry.");
        Assert(root.GetProperty("zenoRoutePendingOperation").ValueKind == JsonValueKind.Null, "A current run may have no pending operation.");
        Assert(!root.TryGetProperty("ZenoRoutePayload", out _), "Runtime isolation state must not leak into the shared JSON.");

        PhilosophyRunState decoded = PhilosophyRunStateCodec.Decode(encoded, Catalog);
        Assert(decoded.HasData, "A current feature generation should keep the shared marker even before route creation.");
        Assert(decoded.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current, "Current embedded state should remain current.");
        Assert(decoded.ZenoRoutePayload.State?.Route is null, "The absent route should remain absent.");
        Assert(decoded.ZenoRouteOriginalEncodedState is null, "Valid current data should not be retained as isolated raw state.");
    }

    private static void PendingOperationRoundTripsInsideSharedState()
    {
        ZenoRouteFeatureState feature = CreateUnresolved();
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            null);
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareSwitch(feature, 0, material, Catalog);
        Assert(prepared.Status == ZenoRouteTransitionStatus.Prepared, "Switch should prepare before shared-state embedding.");

        PhilosophyRunState state = new();
        state.SetCurrentZenoRouteState(prepared.State);
        string encoded = PhilosophyRunStateCodec.Encode(state, Catalog);
        PhilosophyRunState decoded = PhilosophyRunStateCodec.Decode(encoded, Catalog);

        Assert(decoded.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current, "A valid prepared operation should remain current.");
        Assert(decoded.ZenoRoutePayload.State?.Route?.Stage == ZenoRouteStage.Unresolved, "The write-ahead record must leave the route at its source stage.");
        Assert(decoded.ZenoRoutePayload.State?.PendingOperation?.Kind == ZenoRouteOperationKind.Switch, "The Switch pending operation should round-trip.");
        Assert(
            decoded.ZenoRoutePayload.State?.PendingOperation?.Candidate.Stage == ZenoRouteStage.WaitingInterval,
            "The pending candidate should retain its only target stage.");
    }

    private static void UnknownAndInvalidZenoPayloadsAreIsolatedWithoutLosingOtherState()
    {
        const string unknownJson =
            "{\"CurrentDoctrine\":{\"ThinkerId\":\"SOCRATES\",\"DoctrineId\":\"VIRTUE\"}," +
            "\"zenoRouteFeatureGeneration\":2,\"zenoRoute\":{\"future\":true}," +
            "\"zenoRoutePendingOperation\":null,\"zenoFuturePolicy\":{\"keep\":true}}";
        string unknownEncoded = ToHex(unknownJson);
        PhilosophyRunState unknown = PhilosophyRunStateCodec.Decode(unknownEncoded, Catalog);

        Assert(unknown.CurrentDoctrine?.ThinkerId == "SOCRATES", "Unknown Zeno data must not discard other valid philosophy state.");
        Assert(unknown.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.UnknownNewer, "A higher feature generation should be isolated as unknown newer data.");
        Assert(unknown.ZenoRouteOriginalEncodedState == unknownEncoded, "The original shared marker should be retained exactly for future preservation.");
        AssertEncodeBlocked(unknown, "Unknown newer Zeno data must block shared marker rewriting.");

        const string invalidJson =
            "{\"GeneratedCandidates\":{\"ACT_ONE\":{\"GenerationKey\":\"ACT_ONE\",\"CandidateIds\":[\"KONGZI\",\"MOZI\"]}}," +
            "\"zenoRouteFeatureGeneration\":1,\"zenoRoute\":{\"version\":1},\"zenoRoutePendingOperation\":null}";
        string invalidEncoded = ToHex(invalidJson);
        PhilosophyRunState invalid = PhilosophyRunStateCodec.Decode(invalidEncoded, Catalog);

        Assert(invalid.GeneratedCandidates["ACT_ONE"].CandidateIds.Count == 2, "Invalid Zeno data must not discard unrelated generated candidates.");
        Assert(invalid.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Invalid, "Malformed current Zeno data should be isolated as invalid.");
        Assert(invalid.ZenoRouteOriginalEncodedState == invalidEncoded, "Invalid current data should retain the exact original shared marker.");
        AssertEncodeBlocked(invalid, "Invalid current Zeno data must block shared marker rewriting.");
    }

    private static void DuplicateAndOrphanZenoPropertiesAreRejected()
    {
        const string duplicateJson =
            "{\"zenoRouteFeatureGeneration\":1,\"zenoRouteFeatureGeneration\":1," +
            "\"zenoRoute\":null,\"zenoRoutePendingOperation\":null}";
        PhilosophyRunState duplicate = PhilosophyRunStateCodec.Decode(ToHex(duplicateJson), Catalog);
        Assert(duplicate.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Invalid, "Duplicate Zeno root properties should remain invalid.");

        const string orphanJson = "{\"zenoRoute\":null,\"zenoRoutePendingOperation\":null}";
        PhilosophyRunState orphan = PhilosophyRunStateCodec.Decode(ToHex(orphanJson), Catalog);
        Assert(orphan.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Invalid, "Zeno route properties without a feature generation should not look like a legacy run.");
    }

    private static ZenoRouteFeatureState CreateUnresolved()
    {
        ZenoRouteState route = new(
            ZenoRouteState.CurrentVersion,
            ZenoRouteStage.Unresolved,
            0,
            0,
            "RUN_20260924_SHARED_001",
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
            null);
        return new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, route);
    }

    private static string ToHex(string json) => Convert.ToHexString(Encoding.UTF8.GetBytes(json));

    private static void AssertEncodeBlocked(PhilosophyRunState state, string message)
    {
        try
        {
            _ = PhilosophyRunStateCodec.Encode(state, Catalog);
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("must not be rewritten", StringComparison.Ordinal))
        {
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
