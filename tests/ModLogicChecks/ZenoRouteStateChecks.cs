using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class ZenoRouteStateChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string PriorStatement = "STATEMENT_PRIOR";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string LaterOutcome = "OUTCOME_PUBLIC";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [PriorStatement, CurrentStatement, NarrowStatement],
        [LaterOutcome]);

    public static void Run()
    {
        CurrentGenerationWithoutRouteRoundTrips();
        EveryRouteStageRoundTrips();
        LegacyAndUnknownGenerationsAreClassifiedWithoutMutation();
        CurrentGenerationRejectsMalformedOrUnknownData();
        OutcomeLegalityIsStrict();
        MaterialDigestAndRegistryAreStrict();
        Console.WriteLine("Zeno route state checks passed: stages, material digest, strict current payloads and legacy classification.");
    }

    private static void CurrentGenerationWithoutRouteRoundTrips()
    {
        string encoded = ZenoRouteStateCodec.Encode(
            new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, null),
            Catalog);
        ZenoRouteDecodeResult decoded = ZenoRouteStateCodec.Decode(encoded, Catalog);

        Assert(decoded.Classification == ZenoRoutePayloadClassification.Current, "Current empty route should decode.");
        Assert(decoded.State?.Route is null, "Current generation may legally have no route before third-act entry.");
        Assert(decoded.OpaquePayload is null, "Valid current payload should not be isolated as opaque data.");
    }

    private static void EveryRouteStageRoundTrips()
    {
        ZenoAssentMaterialSnapshot material = CreateMaterial();
        ZenoAssentMaterialSnapshot noMaterial = CreateNoMaterial();
        ZenoRouteState[] states =
        [
            CreateState(ZenoRouteStage.Unresolved),
            CreateState(ZenoRouteStage.DiogenesClosed),
            CreateState(ZenoRouteStage.WaitingInterval, material),
            CreateState(ZenoRouteStage.ReadyToClaim, material, receipt: "ROOM_RECEIPT_42"),
            CreateState(ZenoRouteStage.OpeningClaimed, material, "ROOM_RECEIPT_42", ZenoOpeningTrigger.SafeBoundary, ZenoResumeDestination.Map),
            CreateState(ZenoRouteStage.EventActive, material, trigger: ZenoOpeningTrigger.BeforeBoss, destination: ZenoResumeDestination.Boss),
            CreateState(ZenoRouteStage.OutcomeCommitted, material, trigger: ZenoOpeningTrigger.BeforeActExit, destination: ZenoResumeDestination.ActExit, outcome: ZenoAssentOutcome.Narrow, currentAfter: NarrowStatement),
            CreateState(ZenoRouteStage.ZenoClosed, noMaterial, trigger: ZenoOpeningTrigger.BeforeActExit, destination: ZenoResumeDestination.ActExit, outcome: ZenoAssentOutcome.NoAssent),
            CreateState(ZenoRouteStage.RunTerminated, material),
        ];

        foreach (ZenoRouteState state in states)
        {
            string encoded = ZenoRouteStateCodec.Encode(
                new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, state),
                Catalog);
            ZenoRouteDecodeResult decoded = ZenoRouteStateCodec.Decode(encoded, Catalog);
            Assert(decoded.Classification == ZenoRoutePayloadClassification.Current, $"{state.Stage} should round-trip.");
            Assert(decoded.State?.Route?.Stage == state.Stage, $"{state.Stage} should retain its stage.");
        }
    }

    private static void LegacyAndUnknownGenerationsAreClassifiedWithoutMutation()
    {
        Assert(
            ZenoRouteStateCodec.Decode(null, Catalog).Classification == ZenoRoutePayloadClassification.PreFeatureLegacy,
            "Absent feature payload should be legacy, not a corrupt current route.");

        const string older = "{\"zenoRouteFeatureGeneration\":0,\"zenoRoute\":null}";
        ZenoRouteDecodeResult unsupported = ZenoRouteStateCodec.Decode(older, Catalog);
        Assert(unsupported.Classification == ZenoRoutePayloadClassification.UnsupportedOlder, "Unregistered older generation should not be guessed into current data.");
        Assert(unsupported.OpaquePayload == older, "Unsupported older payload should be preserved verbatim.");

        const string newer = "{\"zenoRouteFeatureGeneration\":2,\"futureField\":{\"opaque\":true}}";
        ZenoRouteDecodeResult unknown = ZenoRouteStateCodec.Decode(newer, Catalog);
        Assert(unknown.Classification == ZenoRoutePayloadClassification.UnknownNewer, "Higher generation should be classified as unknown newer data.");
        Assert(unknown.OpaquePayload == newer, "Unknown newer payload should be preserved verbatim.");
    }

    private static void CurrentGenerationRejectsMalformedOrUnknownData()
    {
        string valid = ZenoRouteStateCodec.Encode(
            new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, CreateState(ZenoRouteStage.Unresolved)),
            Catalog);
        string duplicate = valid.Replace(
            "\"zenoRouteFeatureGeneration\":1",
            "\"zenoRouteFeatureGeneration\":1,\"zenoRouteFeatureGeneration\":1",
            StringComparison.Ordinal);
        AssertInvalid(duplicate, "Duplicate properties should be rejected.");

        JsonObject unknownField = ParseRoot(valid);
        unknownField["unexpected"] = true;
        AssertInvalid(unknownField.ToJsonString(), "Unknown current-generation fields should be rejected.");

        JsonObject missingRoute = ParseRoot(valid);
        missingRoute.Remove("zenoRoute");
        AssertInvalid(missingRoute.ToJsonString(), "Required route property should not be inferred.");

        JsonObject missingPending = ParseRoot(valid);
        missingPending.Remove("zenoRoutePendingOperation");
        AssertInvalid(missingPending.ToJsonString(), "Required pending-operation property should not be inferred.");

        JsonObject integerEnum = ParseRoot(valid);
        integerEnum["zenoRoute"]!["stage"] = 0;
        AssertInvalid(integerEnum.ToJsonString(), "Integer enum values should be rejected.");

        JsonObject wrongAct = ParseRoot(valid);
        wrongAct["zenoRoute"]!["actIndex"] = 1;
        AssertInvalid(wrongAct.ToJsonString(), "Route state should remain owned by the third act.");

        string waiting = ZenoRouteStateCodec.Encode(
            new ZenoRouteFeatureState(
                ZenoRouteFeatureGeneration.Current,
                CreateState(ZenoRouteStage.WaitingInterval, CreateMaterial())),
            Catalog);
        JsonObject nullHistory = ParseRoot(waiting);
        nullHistory["zenoRoute"]!["material"]!["publicHistoryIds"] = null;
        AssertInvalid(nullHistory.ToJsonString(), "A present but null public history should be rejected without throwing.");

        ZenoRouteState incompleteScheduling = CreateState(ZenoRouteStage.EventActive, CreateMaterial()) with
        {
            OpeningTrigger = ZenoOpeningTrigger.BeforeBoss,
        };
        AssertEncodeFails(incompleteScheduling, "Partially claimed opening should not serialize as a valid state.");
    }

    private static void OutcomeLegalityIsStrict()
    {
        ZenoAssentMaterialSnapshot material = CreateMaterial();
        AssertEncodeFails(
            CreateState(ZenoRouteStage.OutcomeCommitted, material, trigger: ZenoOpeningTrigger.BeforeBoss, destination: ZenoResumeDestination.Boss, outcome: ZenoAssentOutcome.Keep, currentAfter: NarrowStatement),
            "Keep must preserve the current statement.");
        AssertEncodeFails(
            CreateState(ZenoRouteStage.OutcomeCommitted, material, trigger: ZenoOpeningTrigger.BeforeBoss, destination: ZenoResumeDestination.Boss, outcome: ZenoAssentOutcome.Withdraw, currentAfter: CurrentStatement),
            "Withdraw must leave no current statement.");
        AssertEncodeFails(
            CreateState(ZenoRouteStage.OutcomeCommitted, material, trigger: ZenoOpeningTrigger.BeforeBoss, destination: ZenoResumeDestination.Boss, outcome: ZenoAssentOutcome.NoAssent),
            "No-assent must only be available when Switch supplied no action fact or statement.");
    }

    private static void MaterialDigestAndRegistryAreStrict()
    {
        ZenoAssentMaterialSnapshot material = CreateMaterial();
        ZenoAssentMaterialSnapshot tampered = new(
            material.SourceVersion,
            material.SourceKind,
            material.ActionFactId,
            material.IsNoMaterial,
            material.PublicHistoryIds,
            NarrowStatement,
            material.NarrowStatementId,
            material.LaterOutcomeId,
            material.Digest);
        AssertEncodeFails(CreateState(ZenoRouteStage.WaitingInterval, tampered), "Material content must match its digest.");

        ZenoAssentMaterialSnapshot unknownStatement = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1, SourceKind, ActionFact, false, [PriorStatement], "STATEMENT_UNREGISTERED", null, LaterOutcome);
        AssertEncodeFails(CreateState(ZenoRouteStage.WaitingInterval, unknownStatement), "Unregistered ids should be rejected even with a matching digest.");
    }

    private static ZenoAssentMaterialSnapshot CreateMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1, SourceKind, ActionFact, false, [PriorStatement, CurrentStatement], CurrentStatement, NarrowStatement, LaterOutcome);

    private static ZenoAssentMaterialSnapshot CreateNoMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(1, SourceKind, null, true, [], null, null, null);

    private static ZenoRouteState CreateState(
        ZenoRouteStage stage,
        ZenoAssentMaterialSnapshot? material = null,
        string? receipt = null,
        ZenoOpeningTrigger? trigger = null,
        ZenoResumeDestination? destination = null,
        ZenoAssentOutcome? outcome = null,
        string? currentAfter = null)
    {
        bool hasScheduling = trigger is not null;
        return new ZenoRouteState(
            ZenoRouteState.CurrentVersion,
            stage,
            3,
            2,
            "RUN_20260924_001",
            2,
            ZenoRouteIds.RouteEdge,
            ZenoRouteIds.TerminalNode,
            material,
            receipt,
            trigger,
            destination,
            hasScheduling ? $"DESTINATION_{destination}" : null,
            hasScheduling ? "ZENO_EVENT_INSTANCE_001" : null,
            outcome,
            currentAfter);
    }

    private static JsonObject ParseRoot(string json) =>
        JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Expected JSON object.");

    private static void AssertInvalid(string payload, string message)
    {
        ZenoRouteDecodeResult decoded = ZenoRouteStateCodec.Decode(payload, Catalog);
        Assert(decoded.Classification == ZenoRoutePayloadClassification.Invalid, message);
        Assert(decoded.OpaquePayload == payload, "Invalid current payload should be retained for isolation diagnostics.");
    }

    private static void AssertEncodeFails(ZenoRouteState state, string message)
    {
        try
        {
            _ = ZenoRouteStateCodec.Encode(new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, state), Catalog);
            throw new InvalidOperationException(message);
        }
        catch (InvalidDataException)
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
