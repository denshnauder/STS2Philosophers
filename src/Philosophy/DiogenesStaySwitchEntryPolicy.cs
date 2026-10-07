using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed record DiogenesStaySwitchEntryContext(
    int ActIndex,
    int PlayerCount,
    bool IsMapRoom,
    bool IsRestoringRoomStackBase,
    int? SourceRoomId);

[JsonConverter(typeof(DiogenesStaySwitchEntryRecordConverter))]
internal sealed record DiogenesStaySwitchEntryRecord(
    int Version,
    string RunId,
    int? SourceRoomId,
    SocratesVirtueMaterial? Facts,
    ZenoAssentMaterialSnapshot Material,
    bool Closed = false);

internal sealed class DiogenesStaySwitchEntryRecordConverter : JsonConverter<DiogenesStaySwitchEntryRecord>
{
    public DiogenesStaySwitchEntryRecordConverter() { }

    private sealed record FactsPayload(
        [property: JsonRequired] int SourceVersion,
        [property: JsonRequired] string SourceId,
        [property: JsonRequired] string ThinkerId,
        [property: JsonRequired] string ProblemId,
        [property: JsonRequired] string RouteNodeId,
        [property: JsonRequired] string OpportunityId,
        [property: JsonRequired] string CombatId,
        [property: JsonRequired] int ActIndex,
        [property: JsonRequired] bool OnSocratesVirtueBranch,
        [property: JsonRequired] bool HadRealRetreatOpportunity,
        [property: JsonRequired] SocratesVirtueAction Action,
        [property: JsonRequired] SocratesVirtueActionResult Result,
        [property: JsonRequired] int RetainedHitPointLoss,
        [property: JsonRequired] int ConsumedPotionCount,
        [property: JsonRequired] bool RetreatOpportunityConsumed,
        [property: JsonRequired] bool VictoryRewardsForfeited)
    {
        public SocratesVirtueMaterial ToMaterial() => new(
            SourceVersion, SourceId, ThinkerId, ProblemId, RouteNodeId, OpportunityId, CombatId, ActIndex,
            OnSocratesVirtueBranch, HadRealRetreatOpportunity, Action, Result, RetainedHitPointLoss,
            ConsumedPotionCount, RetreatOpportunityConsumed, VictoryRewardsForfeited);

        public static FactsPayload? FromMaterial(SocratesVirtueMaterial? facts) => facts is null ? null : new(
            facts.SourceVersion, facts.SourceId, facts.ThinkerId, facts.ProblemId, facts.RouteNodeId,
            facts.OpportunityId, facts.CombatId, facts.ActIndex, facts.OnSocratesVirtueBranch,
            facts.HadRealRetreatOpportunity, facts.Action, facts.Result, facts.RetainedHitPointLoss,
            facts.ConsumedPotionCount, facts.RetreatOpportunityConsumed, facts.VictoryRewardsForfeited);
    }

    private sealed record MaterialPayload(
        [property: JsonRequired] int SourceVersion,
        [property: JsonRequired] string SourceKind,
        [property: JsonRequired] string? ActionFactId,
        [property: JsonRequired] bool IsNoMaterial,
        [property: JsonRequired] string[] PublicHistoryIds,
        [property: JsonRequired] string? CurrentStatementId,
        [property: JsonRequired] string? NarrowStatementId,
        [property: JsonRequired] string? LaterOutcomeId,
        [property: JsonRequired] string Digest);

    private sealed record EntryPayload(
        [property: JsonRequired] int Version,
        [property: JsonRequired] string RunId,
        [property: JsonRequired] int? SourceRoomId,
        [property: JsonRequired] FactsPayload? Facts,
        [property: JsonRequired] MaterialPayload Material,
        [property: JsonRequired] bool Closed);

    public override DiogenesStaySwitchEntryRecord Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        EntryPayload payload = JsonSerializer.Deserialize<EntryPayload>(ref reader, options)
            ?? throw new JsonException("Missing Diogenes entry payload.");
        if (payload.Material is not { PublicHistoryIds: not null } material)
        {
            throw new JsonException("Missing Diogenes material snapshot.");
        }

        DiogenesStaySwitchEntryRecord entry = new(
            payload.Version,
            payload.RunId,
            payload.SourceRoomId,
            payload.Facts?.ToMaterial(),
            new ZenoAssentMaterialSnapshot(
                material.SourceVersion, material.SourceKind, material.ActionFactId, material.IsNoMaterial,
                material.PublicHistoryIds, material.CurrentStatementId, material.NarrowStatementId,
                material.LaterOutcomeId, material.Digest),
            payload.Closed);
        return DiogenesStaySwitchEntryPolicy.IsValid(entry)
            ? entry
            : throw new JsonException("Invalid Diogenes entry payload.");
    }

    public override void Write(
        Utf8JsonWriter writer, DiogenesStaySwitchEntryRecord value, JsonSerializerOptions options)
    {
        if (!DiogenesStaySwitchEntryPolicy.IsValid(value))
        {
            throw new JsonException("Invalid Diogenes entry payload.");
        }

        ZenoAssentMaterialSnapshot material = value.Material;
        JsonSerializer.Serialize(writer, new EntryPayload(
            value.Version, value.RunId, value.SourceRoomId, FactsPayload.FromMaterial(value.Facts),
            new MaterialPayload(
                material.SourceVersion, material.SourceKind, material.ActionFactId, material.IsNoMaterial,
                material.PublicHistoryIds.ToArray(), material.CurrentStatementId, material.NarrowStatementId,
                material.LaterOutcomeId, material.Digest),
            value.Closed), options);
    }
}

internal static class DiogenesStaySwitchEntryPolicy
{
    internal const int CurrentVersion = 1;
    internal const string SourceKind = "DIOGENES_SOCRATES_VIRTUE_ENTRY";
    internal const string ContinuedFactId = "SOCRATES_VIRTUE_CONTINUED";
    internal const string RetreatedFactId = "SOCRATES_VIRTUE_RETREATED";
    internal const string RunIdPrefix = "DIOGENES_SOCRATES_";

    public static bool TryCreate(
        PhilosophyRunState sharedState,
        ulong runSeed,
        DiogenesStaySwitchEntryContext context,
        out DiogenesStaySwitchEntryRecord? entry)
    {
        ArgumentNullException.ThrowIfNull(sharedState);
        ArgumentNullException.ThrowIfNull(context);
        entry = null;
        if (context.ActIndex != 2 || context.PlayerCount != 1 || !context.IsMapRoom ||
            context.IsRestoringRoomStackBase || context.SourceRoomId is < 0 ||
            sharedState.PreservedSaveMarkerEntries.Count != 0 ||
            !CanInitialize(sharedState.ZenoRoutePayload) ||
            sharedState.CurrentDoctrine?.ThinkerId != SocratesVirtueUpstreamRecord.SocratesThinkerId ||
            sharedState.CurrentDoctrine.DoctrineId !=
                WesternEntryPolicy.RelicIdFor(SocratesVirtueUpstreamRecord.VirtueProblemId) ||
            sharedState.WesternJourney?.CurrentNodeId != SocratesVirtueUpstreamRecord.VirtueRouteNodeId)
        {
            return false;
        }

        SocratesVirtueMaterial? facts = null;
        if (sharedState.SocratesVirtueUpstream?.TryGetMaterial(out SocratesVirtueMaterial? observed) == true &&
            observed is not null && IsValidFacts(observed))
        {
            facts = observed;
        }

        entry = new DiogenesStaySwitchEntryRecord(
            CurrentVersion,
            RunIdPrefix + runSeed.ToString(CultureInfo.InvariantCulture),
            context.SourceRoomId,
            facts,
            CreateMaterial(facts));
        return IsValid(entry);
    }

    public static bool IsValid(DiogenesStaySwitchEntryRecord? entry)
    {
        if (entry is null || entry.Version != CurrentVersion || entry.SourceRoomId is < 0 ||
            !IsValidRunId(entry.RunId) || entry.Material is null ||
            entry.Facts is not null && !IsValidFacts(entry.Facts))
        {
            return false;
        }

        ZenoAssentMaterialSnapshot expected = CreateMaterial(entry.Facts);
        ZenoAssentMaterialSnapshot actual = entry.Material;
        return actual.SourceVersion == expected.SourceVersion && actual.SourceKind == expected.SourceKind &&
               actual.ActionFactId == expected.ActionFactId && actual.IsNoMaterial == expected.IsNoMaterial &&
               actual.PublicHistoryIds.Count == 0 && actual.CurrentStatementId is null &&
               actual.NarrowStatementId is null && actual.LaterOutcomeId is null &&
               actual.Digest == expected.Digest;
    }

    public static ZenoRouteFeatureState CreateInitialState(DiogenesStaySwitchEntryRecord entry)
    {
        if (!IsValid(entry) || entry.Closed)
        {
            throw new ArgumentException("A valid open Diogenes entry is required.", nameof(entry));
        }

        return new ZenoRouteFeatureState(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                entry.RunId,
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
    }

    private static bool CanInitialize(ZenoRouteDecodeResult payload) =>
        payload.Classification == ZenoRoutePayloadClassification.PreFeatureLegacy ||
        payload is
        {
            Classification: ZenoRoutePayloadClassification.Current,
            State: { Route: null, PendingOperation: null },
        } && ZenoRouteStateCodec.IsValidFeature(payload.State, ZenoRouteValidationCatalogs.SaveRestore);

    private static bool IsValidRunId(string runId) =>
        runId is not null && runId.StartsWith(RunIdPrefix, StringComparison.Ordinal) &&
        ulong.TryParse(runId.AsSpan(RunIdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
            out ulong seed) && runId == RunIdPrefix + seed.ToString(CultureInfo.InvariantCulture);

    private static bool IsValidFacts(SocratesVirtueMaterial facts)
    {
        // Validate the frozen DTO through the same authoritative contract used by its producer.
        SocratesVirtueUpstreamRecord record = new()
        {
            SourceVersion = facts.SourceVersion,
            SourceId = facts.SourceId,
            ThinkerId = facts.ThinkerId,
            ProblemId = facts.ProblemId,
            RouteNodeId = facts.RouteNodeId,
            OpportunityId = facts.OpportunityId,
            CombatId = facts.CombatId,
            ActIndex = facts.ActIndex,
            OnSocratesVirtueBranch = facts.OnSocratesVirtueBranch,
            HadRealRetreatOpportunity = facts.HadRealRetreatOpportunity,
            Action = facts.Action,
            Result = facts.Result,
            RetainedHitPointLoss = facts.RetainedHitPointLoss,
            ConsumedPotionCount = facts.ConsumedPotionCount,
            RetreatOpportunityConsumed = facts.RetreatOpportunityConsumed,
            VictoryRewardsForfeited = facts.VictoryRewardsForfeited,
        };
        return facts.ActIndex < 2 && record.TryGetMaterial(out _);
    }

    private static ZenoAssentMaterialSnapshot CreateMaterial(SocratesVirtueMaterial? facts) =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            CurrentVersion,
            SourceKind,
            facts?.Action switch
            {
                SocratesVirtueAction.Continued => ContinuedFactId,
                SocratesVirtueAction.Retreated => RetreatedFactId,
                _ => null,
            },
            facts is null,
            [],
            null,
            null,
            null);
}
