using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2Philosophers;

internal static class PhilosophyRunStateCodec
{
    private static readonly string[] ZenoPropertyNames =
    [
        "zenoRouteFeatureGeneration",
        "zenoRoute",
        "zenoRoutePendingOperation",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public static string Encode(PhilosophyRunState state)
    {
        return Encode(state, state.ZenoRouteValidationCatalog);
    }

    internal static string Encode(PhilosophyRunState state, ZenoRouteValidationCatalog zenoCatalog)
    {
        ValidateDiogenesEntry(state);
        if (state.ZenoRoutePayload.Classification is not (
                ZenoRoutePayloadClassification.PreFeatureLegacy or
                ZenoRoutePayloadClassification.Current))
        {
            throw new InvalidOperationException(
                "The shared run state contains an isolated Zeno payload and must not be rewritten.");
        }

        JsonObject root = JsonSerializer.SerializeToNode(state, JsonOptions)?.AsObject()
            ?? throw new InvalidDataException("The philosophy run state could not be serialized as an object.");
        if (state.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current)
        {
            ZenoRouteFeatureState zenoState = state.ZenoRoutePayload.State
                ?? throw new InvalidDataException("The current Zeno payload did not contain a feature state.");
            string zenoJson = ZenoRouteStateCodec.Encode(zenoState, zenoCatalog);
            using JsonDocument zenoDocument = JsonDocument.Parse(zenoJson);
            foreach (JsonProperty property in zenoDocument.RootElement.EnumerateObject())
            {
                root[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        string json = root.ToJsonString(JsonOptions);
        return Convert.ToHexString(Encoding.UTF8.GetBytes(json));
    }

    public static PhilosophyRunState Decode(string encoded)
    {
        return Decode(encoded, ZenoRouteValidationCatalogs.SaveRestore);
    }

    internal static PhilosophyRunState Decode(string encoded, ZenoRouteValidationCatalog zenoCatalog)
    {
        byte[] bytes = Convert.FromHexString(encoded);
        using JsonDocument document = JsonDocument.Parse(bytes);
        PhilosophyRunState state = JsonSerializer.Deserialize<PhilosophyRunState>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The philosophy run state payload was empty.");
        state.NormalizeAfterLoad();
        state.SetZenoRouteValidationCatalog(zenoCatalog);

        string? zenoPayload = ExtractZenoPayload(document.RootElement);
        ZenoRouteDecodeResult zenoResult = ZenoRouteStateCodec.Decode(zenoPayload, zenoCatalog);
        state.RestoreZenoRoutePayload(zenoResult, encoded);
        ValidateDiogenesEntry(state);
        return state;
    }

    private static void ValidateDiogenesEntry(PhilosophyRunState state)
    {
        ZenoRouteState? route = state.ZenoRoutePayload.State?.Route;
        ZenoRouteState? candidate = state.ZenoRoutePayload.State?.PendingOperation?.Candidate;
        if (state.DiogenesStaySwitchEntry is not { } entry)
        {
            if (route?.RunId.StartsWith(DiogenesStaySwitchEntryPolicy.RunIdPrefix, StringComparison.Ordinal) == true ||
                route?.Material?.SourceKind == DiogenesStaySwitchEntryPolicy.SourceKind ||
                candidate?.Material?.SourceKind == DiogenesStaySwitchEntryPolicy.SourceKind)
            {
                throw new InvalidDataException("The production Diogenes route lost its frozen entry.");
            }
            return;
        }

        if (!DiogenesStaySwitchEntryPolicy.IsValid(entry) ||
            state.ZenoRoutePayload.Classification != ZenoRoutePayloadClassification.Current ||
            route is null || route.RunId != entry.RunId ||
            route.Material is { } material && material.Digest != entry.Material.Digest ||
            candidate?.Material is { } pendingMaterial && pendingMaterial.Digest != entry.Material.Digest ||
            entry.Closed && route.Stage == ZenoRouteStage.Unresolved)
        {
            throw new InvalidDataException("The Diogenes entry does not match its frozen route.");
        }
    }

    private static string? ExtractZenoPayload(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The philosophy run state payload was not an object.");
        }

        bool found = false;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!ZenoPropertyNames.Contains(property.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                found = true;
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return found ? Encoding.UTF8.GetString(stream.ToArray()) : null;
    }
}
