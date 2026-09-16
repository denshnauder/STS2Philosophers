using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SocratesKnowledgeRestoreResult { Absent, Valid, Invalid }
internal sealed record SocratesKnowledgeSavedChoice(
    [property: JsonRequired] SocratesKnowledgeScene Scene,
    [property: JsonRequired] SocratesKnowledgeChoice Choice);
internal sealed record SocratesKnowledgeSave(
    [property: JsonRequired] int Version,
    [property: JsonRequired] string ThinkerId,
    [property: JsonRequired] string ProblemId,
    [property: JsonRequired] List<SocratesKnowledgeScene> SeenScenes,
    [property: JsonRequired] List<SocratesKnowledgeSavedChoice> Choices,
    [property: JsonRequired] SocratesKnowledgeStatement Statement,
    [property: JsonRequired] SocratesKnowledgeQuestion Question,
    [property: JsonRequired] bool ActTwoLateEntry);

internal static class SocratesKnowledgeRouteRecordCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Serialize(SocratesKnowledgeRouteRecord record) => JsonSerializer.Serialize(new SocratesKnowledgeSave(
        SocratesKnowledgeRouteRecord.Version, SocratesKnowledgeRouteRecord.ThinkerId,
        SocratesKnowledgeRouteRecord.ProblemId, record.SeenScenes.ToList(),
        record.History.Select(item => new SocratesKnowledgeSavedChoice(item.Scene, item.Choice)).ToList(),
        record.Statement, record.Question, record.ActTwoLateEntry), Options);

    public static SocratesKnowledgeRestoreResult Restore(string? payload, out SocratesKnowledgeRouteRecord? record)
    {
        record = null;
        // Only a missing field denotes a legacy run. Broken new data cannot activate legacy benefits.
        if (payload is null) return SocratesKnowledgeRestoreResult.Absent;
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 16384) return SocratesKnowledgeRestoreResult.Invalid;
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!HasUniqueProperties(document.RootElement)) return SocratesKnowledgeRestoreResult.Invalid;
            var saved = JsonSerializer.Deserialize<SocratesKnowledgeSave>(payload, Options);
            if (saved is null || saved.Version != SocratesKnowledgeRouteRecord.Version
                || saved.ThinkerId != SocratesKnowledgeRouteRecord.ThinkerId
                || saved.ProblemId != SocratesKnowledgeRouteRecord.ProblemId
                || saved.SeenScenes is null || saved.Choices is null
                || saved.SeenScenes.Count > 2 || saved.Choices.Count > saved.SeenScenes.Count
                || !Enum.IsDefined(saved.Statement) || !Enum.IsDefined(saved.Question))
                return SocratesKnowledgeRestoreResult.Invalid;

            var restored = new SocratesKnowledgeRouteRecord();
            int choiceIndex = 0;
            foreach (var scene in saved.SeenScenes)
            {
                if (!restored.TryShow(scene)) return SocratesKnowledgeRestoreResult.Invalid;
                if (choiceIndex < saved.Choices.Count && saved.Choices[choiceIndex] is { } choice && choice.Scene == scene)
                {
                    if (!restored.TryResolve(scene, choice.Choice)) return SocratesKnowledgeRestoreResult.Invalid;
                    choiceIndex++;
                }
            }
            if (choiceIndex != saved.Choices.Count || restored.Statement != saved.Statement
                || restored.Question != saved.Question || restored.ActTwoLateEntry != saved.ActTwoLateEntry)
                return SocratesKnowledgeRestoreResult.Invalid;
            record = restored;
            return SocratesKnowledgeRestoreResult.Valid;
        }
        catch (JsonException) { return SocratesKnowledgeRestoreResult.Invalid; }
        catch (NotSupportedException) { return SocratesKnowledgeRestoreResult.Invalid; }
    }

    private static bool HasUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || !HasUniqueProperties(property.Value)) return false;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) if (!HasUniqueProperties(item)) return false;
        }
        return true;
    }
}
