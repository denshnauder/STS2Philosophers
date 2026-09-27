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
    string? EncodedState);

internal static class PhilosophyRunStateRitsuSaveReader
{
    internal static PhilosophyRunStateRitsuSaveReadResult Read(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!TryGetObject(root, "_ritsulib", out JsonElement ritsuRoot) ||
                !TryGetObject(ritsuRoot, "run_saved_data", out JsonElement dataRoot) ||
                !TryGetObjectIgnoreCase(dataRoot, PhilosophyRunStateRitsuContract.ModId, out JsonElement modRoot) ||
                !TryGetObjectIgnoreCase(modRoot, PhilosophyRunStateRitsuContract.SlotKey, out JsonElement slotRoot))
            {
                return new(PhilosophyRunStateRitsuSaveClassification.Missing, null);
            }

            if (!slotRoot.TryGetProperty("schema", out JsonElement schema) ||
                !schema.TryGetInt32(out int schemaVersion) ||
                schemaVersion != PhilosophyRunStateRitsuPayload.CurrentSchemaVersion ||
                !slotRoot.TryGetProperty("kind", out JsonElement kind) ||
                kind.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.GetString(), "run", StringComparison.Ordinal) ||
                !TryGetObject(slotRoot, "data", out JsonElement payload) ||
                !payload.TryGetProperty(nameof(PhilosophyRunStateRitsuPayload.EncodedState), out JsonElement encoded) ||
                encoded.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(encoded.GetString()))
            {
                return new(PhilosophyRunStateRitsuSaveClassification.Invalid, null);
            }

            return new(
                PhilosophyRunStateRitsuSaveClassification.Current,
                encoded.GetString());
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

    private static bool TryGetObjectIgnoreCase(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in parent.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object)
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
