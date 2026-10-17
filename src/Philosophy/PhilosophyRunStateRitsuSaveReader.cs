using System.Text.Json;

namespace STS2Philosophers;

internal enum PhilosophyRunStateRitsuSaveClassification
{
    Missing,
    Current,
    Invalid,
}

internal sealed record PhilosophyRunStateRitsuSaveReadResult(
    PhilosophyRunStateRitsuSaveClassification Classification,
    string? EncodedState,
    bool CanPreserveRaw = false,
    string? UnsafeSaveReason = null);

internal static class PhilosophyRunStateRitsuSaveReader
{
    internal static PhilosophyRunStateRitsuSaveReadResult Read(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
            if (!root.TryGetProperty("_ritsulib", out JsonElement ritsuRoot))
                return new(PhilosophyRunStateRitsuSaveClassification.Missing, null, true);
            if (ritsuRoot.ValueKind != JsonValueKind.Object)
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
            if (!ritsuRoot.TryGetProperty("run_saved_data", out JsonElement dataRoot))
                return new(PhilosophyRunStateRitsuSaveClassification.Missing, null, true);
            if (dataRoot.ValueKind != JsonValueKind.Object)
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
            if (!TryGetPropertyIgnoreCase(dataRoot, PhilosophyRunStateRitsuContract.ModId, out JsonElement modRoot))
                return new(PhilosophyRunStateRitsuSaveClassification.Missing, null, true);
            if (modRoot.ValueKind != JsonValueKind.Object)
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
            if (!TryGetPropertyIgnoreCase(modRoot, PhilosophyRunStateRitsuContract.SlotKey, out JsonElement slotRoot))
            {
                return new(PhilosophyRunStateRitsuSaveClassification.Missing, null, true);
            }

            if (slotRoot.ValueKind != JsonValueKind.Object ||
                !slotRoot.TryGetProperty("schema", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out int schemaVersion) ||
                schemaVersion != PhilosophyRunStateRitsuPayload.CurrentSchemaVersion ||
                !slotRoot.TryGetProperty("kind", out JsonElement kind) ||
                kind.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.GetString(), "run", StringComparison.Ordinal) ||
                !TryGetObject(slotRoot, "data", out JsonElement payload) ||
                !payload.TryGetProperty(nameof(PhilosophyRunStateRitsuPayload.SchemaVersion), out JsonElement payloadSchema) ||
                payloadSchema.ValueKind != JsonValueKind.Number ||
                !payloadSchema.TryGetInt32(out int payloadVersion) ||
                payloadVersion != PhilosophyRunStateRitsuPayload.CurrentSchemaVersion ||
                !payload.TryGetProperty(nameof(PhilosophyRunStateRitsuPayload.EncodedState), out JsonElement encoded) ||
                encoded.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(encoded.GetString()))
            {
                // Ritsu preserves object entries even when typed import fails,
                // but its document parser discards non-object entries entirely.
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null,
                    slotRoot.ValueKind == JsonValueKind.Object);
            }

            return new(
                PhilosophyRunStateRitsuSaveClassification.Current,
                encoded.GetString(), true);
        }
        catch (JsonException)
        {
            return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
        }
    }

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(name, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in parent.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
