using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal static class ZenoRouteStateCodec
{
    private const int MaxPayloadBytes = 64 * 1024;
    private const int MaxHistoryEntries = 16;
    private const int MaxRuntimeTokenLength = 128;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static ZenoAssentMaterialSnapshot CreateMaterialSnapshot(
        int sourceVersion,
        string sourceKind,
        string? actionFactId,
        bool isNoMaterial,
        IEnumerable<string> publicHistoryIds,
        string? currentStatementId,
        string? narrowStatementId,
        string? laterOutcomeId)
    {
        string[] history = publicHistoryIds.ToArray();
        MaterialPayload material = new(
            sourceVersion,
            sourceKind,
            actionFactId,
            isNoMaterial,
            history.ToList(),
            currentStatementId,
            narrowStatementId,
            laterOutcomeId,
            string.Empty);
        string digest = ComputeMaterialDigest(material);

        return new ZenoAssentMaterialSnapshot(
            sourceVersion,
            sourceKind,
            actionFactId,
            isNoMaterial,
            history,
            currentStatementId,
            narrowStatementId,
            laterOutcomeId,
            digest);
    }

    public static string Encode(ZenoRouteFeatureState state, ZenoRouteValidationCatalog catalog)
    {
        if (!IsValidFeature(state, catalog))
        {
            throw new InvalidDataException("Zeno route state is not valid for the current feature generation.");
        }

        FeaturePayload payload = new(
            state.FeatureGeneration,
            state.Route is null ? null : ToPayload(state.Route),
            state.PendingOperation is null ? null : ToPayload(state.PendingOperation));
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    internal static bool IsValidFeature(ZenoRouteFeatureState state, ZenoRouteValidationCatalog catalog)
    {
        if (state.FeatureGeneration != ZenoRouteFeatureGeneration.Current || !ValidateRoute(state.Route, catalog))
        {
            return false;
        }

        return state.PendingOperation is null ||
               (state.Route is not null && ValidatePendingOperation(state.Route, state.PendingOperation, catalog));
    }

    internal static string ComputeCandidateDigest(ZenoRouteState candidate)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(ToPayload(candidate), JsonOptions);
        return Convert.ToHexString(SHA256.HashData(json));
    }

    public static ZenoRouteDecodeResult Decode(string? payload, ZenoRouteValidationCatalog catalog)
    {
        if (payload is null)
        {
            return new ZenoRouteDecodeResult(ZenoRoutePayloadClassification.PreFeatureLegacy, null, null);
        }

        if (string.IsNullOrWhiteSpace(payload) || Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            return Invalid(payload);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !HasUniqueProperties(document.RootElement) ||
                !document.RootElement.TryGetProperty("zenoRouteFeatureGeneration", out JsonElement generationElement) ||
                !generationElement.TryGetInt32(out int featureGeneration))
            {
                return Invalid(payload);
            }

            if (featureGeneration > ZenoRouteFeatureGeneration.Current)
            {
                return new ZenoRouteDecodeResult(ZenoRoutePayloadClassification.UnknownNewer, null, payload);
            }

            if (featureGeneration < ZenoRouteFeatureGeneration.Current)
            {
                return new ZenoRouteDecodeResult(ZenoRoutePayloadClassification.UnsupportedOlder, null, payload);
            }

            FeaturePayload? decoded = JsonSerializer.Deserialize<FeaturePayload>(payload, JsonOptions);
            if (decoded is null || decoded.FeatureGeneration != ZenoRouteFeatureGeneration.Current)
            {
                return Invalid(payload);
            }

            if (decoded.Route?.Material is { PublicHistoryIds: null } ||
                decoded.PendingOperation is { Candidate: null } ||
                decoded.PendingOperation?.Candidate?.Material is { PublicHistoryIds: null })
            {
                return Invalid(payload);
            }

            ZenoRouteState? route = decoded.Route is null ? null : FromPayload(decoded.Route);
            ZenoRoutePendingOperation? pending = decoded.PendingOperation is null
                ? null
                : FromPayload(decoded.PendingOperation);
            ZenoRouteFeatureState state = new(decoded.FeatureGeneration, route, pending);
            if (!IsValidFeature(state, catalog))
            {
                return Invalid(payload);
            }

            return new ZenoRouteDecodeResult(
                ZenoRoutePayloadClassification.Current,
                state,
                null);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return Invalid(payload);
        }
    }

    private static bool ValidateRoute(ZenoRouteState? route, ZenoRouteValidationCatalog catalog)
    {
        if (route is null)
        {
            return true;
        }

        if (route.Version != ZenoRouteState.CurrentVersion ||
            !Enum.IsDefined(route.Stage) ||
            route.Revision < 0 ||
            route.LastCommittedOperationId < 0 ||
            route.LastCommittedOperationId > route.Revision ||
            !IsRuntimeToken(route.RunId) ||
            route.ActIndex != 2 ||
            !string.Equals(route.RouteEdgeId, ZenoRouteIds.RouteEdge, StringComparison.Ordinal) ||
            !string.Equals(route.TerminalNodeId, ZenoRouteIds.TerminalNode, StringComparison.Ordinal) ||
            !ValidateOptionalRuntimeToken(route.CompletedRoomReceiptId) ||
            !ValidateOptionalRuntimeToken(route.ResumeDestinationId) ||
            !ValidateOptionalRuntimeToken(route.EventInstanceId))
        {
            return false;
        }

        if (route.Material is not null && !ValidateMaterial(route.Material, catalog))
        {
            return false;
        }

        if (route.CurrentStatementAfterId is not null && !catalog.IsKnownStatement(route.CurrentStatementAfterId))
        {
            return false;
        }

        bool hasAnyScheduling = route.OpeningTrigger is not null ||
                                route.ResumeDestination is not null ||
                                route.ResumeDestinationId is not null ||
                                route.EventInstanceId is not null;
        bool hasCompleteScheduling = route.OpeningTrigger is not null &&
                                     route.ResumeDestination is not null &&
                                     route.ResumeDestinationId is not null &&
                                     route.EventInstanceId is not null;

        if (hasCompleteScheduling && !ValidateScheduling(route))
        {
            return false;
        }

        return route.Stage switch
        {
            ZenoRouteStage.Unresolved or ZenoRouteStage.DiogenesClosed =>
                route.Material is null && route.CompletedRoomReceiptId is null && !hasAnyScheduling &&
                route.Outcome is null && route.CurrentStatementAfterId is null,
            ZenoRouteStage.WaitingInterval =>
                route.Material is not null && route.CompletedRoomReceiptId is null && !hasAnyScheduling &&
                route.Outcome is null && route.CurrentStatementAfterId is null,
            ZenoRouteStage.ReadyToClaim =>
                route.Material is not null && route.CompletedRoomReceiptId is not null && !hasAnyScheduling &&
                route.Outcome is null && route.CurrentStatementAfterId is null,
            ZenoRouteStage.OpeningClaimed or ZenoRouteStage.EventActive =>
                route.Material is not null && hasCompleteScheduling && route.Outcome is null &&
                route.CurrentStatementAfterId is null,
            ZenoRouteStage.OutcomeCommitted or ZenoRouteStage.ZenoClosed =>
                route.Material is not null && hasCompleteScheduling && route.Outcome is not null &&
                ValidateOutcome(route),
            ZenoRouteStage.RunTerminated =>
                route.Material is not null && (hasCompleteScheduling || !hasAnyScheduling) &&
                route.Outcome is null && route.CurrentStatementAfterId is null,
            _ => false,
        };
    }

    private static bool ValidatePendingOperation(
        ZenoRouteState route,
        ZenoRoutePendingOperation pending,
        ZenoRouteValidationCatalog catalog)
    {
        ZenoRouteState candidate = pending.Candidate;
        if (!Enum.IsDefined(pending.Kind) ||
            !Enum.IsDefined(pending.SourceStage) ||
            !Enum.IsDefined(pending.TargetStage) ||
            pending.OperationId <= 0 ||
            route.LastCommittedOperationId == long.MaxValue ||
            route.Revision == long.MaxValue ||
            pending.OperationId != route.LastCommittedOperationId + 1 ||
            pending.SourceRevision != route.Revision ||
            pending.SourceStage != route.Stage ||
            pending.TargetStage != candidate.Stage ||
            candidate.Revision != route.Revision + 1 ||
            candidate.LastCommittedOperationId != pending.OperationId ||
            !HasSameIdentity(route, candidate) ||
            !ValidateRoute(candidate, catalog) ||
            !string.Equals(
                pending.CandidateDigest,
                ComputeCandidateDigest(candidate),
                StringComparison.Ordinal))
        {
            return false;
        }

        return pending.Kind switch
        {
            ZenoRouteOperationKind.Stay =>
                pending.SourceStage == ZenoRouteStage.Unresolved &&
                pending.TargetStage == ZenoRouteStage.DiogenesClosed &&
                HasSameOperationalData(route, candidate),
            ZenoRouteOperationKind.Switch =>
                pending.SourceStage == ZenoRouteStage.Unresolved &&
                pending.TargetStage == ZenoRouteStage.WaitingInterval &&
                route.Material is null &&
                candidate.Material is not null &&
                HasSameDataExceptMaterial(route, candidate),
            ZenoRouteOperationKind.IntervalCompleted =>
                pending.SourceStage == ZenoRouteStage.WaitingInterval &&
                pending.TargetStage == ZenoRouteStage.ReadyToClaim &&
                candidate.CompletedRoomReceiptId is not null &&
                HasSameDataExceptReceipt(route, candidate),
            ZenoRouteOperationKind.OpeningClaimed =>
                pending.SourceStage is ZenoRouteStage.WaitingInterval or ZenoRouteStage.ReadyToClaim &&
                pending.TargetStage == ZenoRouteStage.OpeningClaimed &&
                HasSameDataExceptScheduling(route, candidate),
            ZenoRouteOperationKind.EventEstablished =>
                pending.SourceStage == ZenoRouteStage.OpeningClaimed &&
                pending.TargetStage == ZenoRouteStage.EventActive &&
                HasSameOperationalData(route, candidate),
            ZenoRouteOperationKind.OutcomeCommitted =>
                pending.SourceStage == ZenoRouteStage.EventActive &&
                pending.TargetStage == ZenoRouteStage.OutcomeCommitted &&
                HasSameDataExceptOutcome(route, candidate),
            ZenoRouteOperationKind.EventClosed =>
                pending.SourceStage == ZenoRouteStage.OutcomeCommitted &&
                pending.TargetStage == ZenoRouteStage.ZenoClosed &&
                HasSameOperationalData(route, candidate),
            _ => false,
        };
    }

    private static bool HasSameIdentity(ZenoRouteState left, ZenoRouteState right)
    {
        return left.Version == right.Version &&
               string.Equals(left.RunId, right.RunId, StringComparison.Ordinal) &&
               left.ActIndex == right.ActIndex &&
               string.Equals(left.RouteEdgeId, right.RouteEdgeId, StringComparison.Ordinal) &&
               string.Equals(left.TerminalNodeId, right.TerminalNodeId, StringComparison.Ordinal);
    }

    private static bool HasSameOperationalData(ZenoRouteState left, ZenoRouteState right)
    {
        return HasSameMaterial(left.Material, right.Material) &&
               string.Equals(left.CompletedRoomReceiptId, right.CompletedRoomReceiptId, StringComparison.Ordinal) &&
               left.OpeningTrigger == right.OpeningTrigger &&
               left.ResumeDestination == right.ResumeDestination &&
               string.Equals(left.ResumeDestinationId, right.ResumeDestinationId, StringComparison.Ordinal) &&
               string.Equals(left.EventInstanceId, right.EventInstanceId, StringComparison.Ordinal) &&
               left.Outcome == right.Outcome &&
               string.Equals(left.CurrentStatementAfterId, right.CurrentStatementAfterId, StringComparison.Ordinal);
    }

    private static bool HasSameDataExceptMaterial(ZenoRouteState left, ZenoRouteState right)
    {
        return string.Equals(left.CompletedRoomReceiptId, right.CompletedRoomReceiptId, StringComparison.Ordinal) &&
               left.OpeningTrigger == right.OpeningTrigger &&
               left.ResumeDestination == right.ResumeDestination &&
               string.Equals(left.ResumeDestinationId, right.ResumeDestinationId, StringComparison.Ordinal) &&
               string.Equals(left.EventInstanceId, right.EventInstanceId, StringComparison.Ordinal) &&
               left.Outcome == right.Outcome &&
               string.Equals(left.CurrentStatementAfterId, right.CurrentStatementAfterId, StringComparison.Ordinal);
    }

    private static bool HasSameDataExceptReceipt(ZenoRouteState left, ZenoRouteState right)
    {
        return HasSameMaterial(left.Material, right.Material) &&
               left.OpeningTrigger == right.OpeningTrigger &&
               left.ResumeDestination == right.ResumeDestination &&
               string.Equals(left.ResumeDestinationId, right.ResumeDestinationId, StringComparison.Ordinal) &&
               string.Equals(left.EventInstanceId, right.EventInstanceId, StringComparison.Ordinal) &&
               left.Outcome == right.Outcome &&
               string.Equals(left.CurrentStatementAfterId, right.CurrentStatementAfterId, StringComparison.Ordinal);
    }

    private static bool HasSameDataExceptScheduling(ZenoRouteState left, ZenoRouteState right)
    {
        return HasSameMaterial(left.Material, right.Material) &&
               string.Equals(left.CompletedRoomReceiptId, right.CompletedRoomReceiptId, StringComparison.Ordinal) &&
               left.Outcome == right.Outcome &&
               string.Equals(left.CurrentStatementAfterId, right.CurrentStatementAfterId, StringComparison.Ordinal);
    }

    private static bool HasSameDataExceptOutcome(ZenoRouteState left, ZenoRouteState right)
    {
        return HasSameMaterial(left.Material, right.Material) &&
               string.Equals(left.CompletedRoomReceiptId, right.CompletedRoomReceiptId, StringComparison.Ordinal) &&
               left.OpeningTrigger == right.OpeningTrigger &&
               left.ResumeDestination == right.ResumeDestination &&
               string.Equals(left.ResumeDestinationId, right.ResumeDestinationId, StringComparison.Ordinal) &&
               string.Equals(left.EventInstanceId, right.EventInstanceId, StringComparison.Ordinal);
    }

    private static bool HasSameMaterial(ZenoAssentMaterialSnapshot? left, ZenoAssentMaterialSnapshot? right)
    {
        return left is null
            ? right is null
            : right is not null && string.Equals(left.Digest, right.Digest, StringComparison.Ordinal);
    }

    private static bool ValidateMaterial(ZenoAssentMaterialSnapshot material, ZenoRouteValidationCatalog catalog)
    {
        if (!catalog.IsKnownSource(material.SourceKind, material.SourceVersion) ||
            material.PublicHistoryIds.Count > MaxHistoryEntries ||
            material.PublicHistoryIds.Any(id => !catalog.IsKnownStatement(id)) ||
            material.PublicHistoryIds.Distinct(StringComparer.Ordinal).Count() != material.PublicHistoryIds.Count ||
            (material.ActionFactId is not null && !catalog.IsKnownActionFact(material.ActionFactId)) ||
            (material.CurrentStatementId is not null && !catalog.IsKnownStatement(material.CurrentStatementId)) ||
            (material.NarrowStatementId is not null && !catalog.IsKnownStatement(material.NarrowStatementId)) ||
            (material.LaterOutcomeId is not null && !catalog.IsKnownLaterOutcome(material.LaterOutcomeId)) ||
            material.IsNoMaterial != (material.ActionFactId is null) ||
            (material.IsNoMaterial && material.LaterOutcomeId is not null) ||
            (material.NarrowStatementId is not null && material.CurrentStatementId is null))
        {
            return false;
        }

        MaterialPayload canonical = ToPayload(material) with { Digest = string.Empty };
        return string.Equals(material.Digest, ComputeMaterialDigest(canonical), StringComparison.Ordinal);
    }

    private static bool ValidateScheduling(ZenoRouteState route)
    {
        if (route.OpeningTrigger is null || route.ResumeDestination is null ||
            !Enum.IsDefined(route.OpeningTrigger.Value) || !Enum.IsDefined(route.ResumeDestination.Value))
        {
            return false;
        }

        return route.OpeningTrigger.Value switch
        {
            ZenoOpeningTrigger.SafeBoundary =>
                route.ResumeDestination == ZenoResumeDestination.Map && route.CompletedRoomReceiptId is not null,
            ZenoOpeningTrigger.BeforeBoss => route.ResumeDestination == ZenoResumeDestination.Boss,
            ZenoOpeningTrigger.BeforeActExit => route.ResumeDestination == ZenoResumeDestination.ActExit,
            _ => false,
        };
    }

    private static bool ValidateOutcome(ZenoRouteState route)
    {
        if (route.Material is null || route.Outcome is null || !Enum.IsDefined(route.Outcome.Value))
        {
            return false;
        }

        string? current = route.Material.CurrentStatementId;
        string? narrow = route.Material.NarrowStatementId;
        return route.Outcome.Value switch
        {
            ZenoAssentOutcome.Keep =>
                current is not null && string.Equals(route.CurrentStatementAfterId, current, StringComparison.Ordinal),
            ZenoAssentOutcome.Narrow =>
                current is not null && narrow is not null &&
                string.Equals(route.CurrentStatementAfterId, narrow, StringComparison.Ordinal),
            ZenoAssentOutcome.Withdraw => current is not null && route.CurrentStatementAfterId is null,
            ZenoAssentOutcome.NoReassent =>
                route.Material.ActionFactId is not null && current is null && route.CurrentStatementAfterId is null,
            ZenoAssentOutcome.NoAssent =>
                route.Material.IsNoMaterial && current is null && route.CurrentStatementAfterId is null,
            _ => false,
        };
    }

    private static bool IsRuntimeToken(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxRuntimeTokenLength &&
        value.All(character => !char.IsControl(character));

    private static bool ValidateOptionalRuntimeToken(string? value) => value is null || IsRuntimeToken(value);

    private static string ComputeMaterialDigest(MaterialPayload material)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(material with { Digest = string.Empty }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(json));
    }

    private static bool HasUniqueProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                HashSet<string> names = new(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name) || !HasUniqueProperties(property.Value))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                return element.EnumerateArray().All(HasUniqueProperties);
            default:
                return true;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16,
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }

    private static ZenoRouteDecodeResult Invalid(string payload) =>
        new(ZenoRoutePayloadClassification.Invalid, null, payload);

    private static RoutePayload ToPayload(ZenoRouteState route) =>
        new(
            route.Version,
            route.Stage,
            route.Revision,
            route.LastCommittedOperationId,
            route.RunId,
            route.ActIndex,
            route.RouteEdgeId,
            route.TerminalNodeId,
            route.Material is null ? null : ToPayload(route.Material),
            route.CompletedRoomReceiptId,
            route.OpeningTrigger,
            route.ResumeDestination,
            route.ResumeDestinationId,
            route.EventInstanceId,
            route.Outcome,
            route.CurrentStatementAfterId);

    private static MaterialPayload ToPayload(ZenoAssentMaterialSnapshot material) =>
        new(
            material.SourceVersion,
            material.SourceKind,
            material.ActionFactId,
            material.IsNoMaterial,
            material.PublicHistoryIds.ToList(),
            material.CurrentStatementId,
            material.NarrowStatementId,
            material.LaterOutcomeId,
            material.Digest);

    private static PendingOperationPayload ToPayload(ZenoRoutePendingOperation pending) =>
        new(
            pending.OperationId,
            pending.Kind,
            pending.SourceRevision,
            pending.SourceStage,
            pending.TargetStage,
            ToPayload(pending.Candidate),
            pending.CandidateDigest);

    private static ZenoRouteState FromPayload(RoutePayload route) =>
        new(
            route.Version,
            route.Stage,
            route.Revision,
            route.LastCommittedOperationId,
            route.RunId,
            route.ActIndex,
            route.RouteEdgeId,
            route.TerminalNodeId,
            route.Material is null ? null : FromPayload(route.Material),
            route.CompletedRoomReceiptId,
            route.OpeningTrigger,
            route.ResumeDestination,
            route.ResumeDestinationId,
            route.EventInstanceId,
            route.Outcome,
            route.CurrentStatementAfterId);

    private static ZenoAssentMaterialSnapshot FromPayload(MaterialPayload material) =>
        new(
            material.SourceVersion,
            material.SourceKind,
            material.ActionFactId,
            material.IsNoMaterial,
            material.PublicHistoryIds ?? [],
            material.CurrentStatementId,
            material.NarrowStatementId,
            material.LaterOutcomeId,
            material.Digest);

    private static ZenoRoutePendingOperation FromPayload(PendingOperationPayload pending) =>
        new(
            pending.OperationId,
            pending.Kind,
            pending.SourceRevision,
            pending.SourceStage,
            pending.TargetStage,
            FromPayload(pending.Candidate!),
            pending.CandidateDigest);

    private sealed record FeaturePayload(
        [property: JsonRequired, JsonPropertyName("zenoRouteFeatureGeneration")] int FeatureGeneration,
        [property: JsonRequired, JsonPropertyName("zenoRoute")] RoutePayload? Route,
        [property: JsonRequired, JsonPropertyName("zenoRoutePendingOperation")]
        PendingOperationPayload? PendingOperation);

    private sealed record PendingOperationPayload(
        [property: JsonRequired] long OperationId,
        [property: JsonRequired] ZenoRouteOperationKind Kind,
        [property: JsonRequired] long SourceRevision,
        [property: JsonRequired] ZenoRouteStage SourceStage,
        [property: JsonRequired] ZenoRouteStage TargetStage,
        [property: JsonRequired] RoutePayload? Candidate,
        [property: JsonRequired] string CandidateDigest);

    private sealed record RoutePayload(
        [property: JsonRequired] int Version,
        [property: JsonRequired] ZenoRouteStage Stage,
        [property: JsonRequired] long Revision,
        [property: JsonRequired] long LastCommittedOperationId,
        [property: JsonRequired] string RunId,
        [property: JsonRequired] int ActIndex,
        [property: JsonRequired] string RouteEdgeId,
        [property: JsonRequired] string TerminalNodeId,
        [property: JsonRequired] MaterialPayload? Material,
        [property: JsonRequired] string? CompletedRoomReceiptId,
        [property: JsonRequired] ZenoOpeningTrigger? OpeningTrigger,
        [property: JsonRequired] ZenoResumeDestination? ResumeDestination,
        [property: JsonRequired] string? ResumeDestinationId,
        [property: JsonRequired] string? EventInstanceId,
        [property: JsonRequired] ZenoAssentOutcome? Outcome,
        [property: JsonRequired] string? CurrentStatementAfterId);

    private sealed record MaterialPayload(
        [property: JsonRequired] int SourceVersion,
        [property: JsonRequired] string SourceKind,
        [property: JsonRequired] string? ActionFactId,
        [property: JsonRequired] bool IsNoMaterial,
        [property: JsonRequired] List<string>? PublicHistoryIds,
        [property: JsonRequired] string? CurrentStatementId,
        [property: JsonRequired] string? NarrowStatementId,
        [property: JsonRequired] string? LaterOutcomeId,
        [property: JsonRequired] string Digest);
}
