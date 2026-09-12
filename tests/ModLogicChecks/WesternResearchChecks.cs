using System.Text.Json;
using System.Text.RegularExpressions;
using STS2Philosophers;

internal static class WesternResearchChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText("config/western_research.json"));
        JsonElement root = document.RootElement;
        JsonElement[] people = root.GetProperty("people").EnumerateArray().ToArray();
        JsonElement[] paths = root.GetProperty("sample_paths").EnumerateArray().ToArray();
        Check(root.GetProperty("schema_version").GetInt32() == 1 && people.Length == 89 && paths.Length == 12,
            "Western research must retain all 89 people and 12 sample paths.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> names = new(StringComparer.Ordinal);
        string[] allowedUses = ["固定入口", "固定", "问号", "内批", "转线", "实践", "回响", "研究"];
        foreach (JsonElement person in people)
        {
            string id = person.GetProperty("thinker_id").GetString()!;
            string name = person.GetProperty("display_name").GetString()!;
            Check(Regex.IsMatch(id, "^[A-Z][A-Z0-9_]*$") && ids.Add(id) && names.Add(name), "Research people require unique names and stable identifiers.");
            Check(allowedUses.Contains(person.GetProperty("primary_use").GetString()), "Unknown primary use must not silently become a playable node.");
            Check(person.GetProperty("secondary_uses").EnumerateArray().All(use => allowedUses.Contains(use.GetString())), "Unknown secondary use.");
            Check(person.GetProperty("implementation_status").GetString() == "RESEARCH_ONLY", "Importing research must not enable game rewards.");
            Check(person.GetProperty("index_source").GetString()!.StartsWith("14人物与流派索引.md:", StringComparison.Ordinal)
                && person.GetProperty("projection_source").GetString()!.StartsWith("18游戏流程投影.md:", StringComparison.Ordinal), "Every person needs both index and usage provenance.");
        }
        Check(people.Where(person => person.GetProperty("primary_use").GetString() == "固定入口")
            .Select(person => person.GetProperty("thinker_id").GetString()!).ToHashSet(StringComparer.Ordinal)
            .SetEquals(WesternRouteCatalog.EntryThinkerIds), "The source's six anchors must agree with the implemented entry catalog.");
        Check(people.Where(person => person.GetProperty("primary_use").GetString() == "研究")
            .Select(person => person.GetProperty("display_name").GetString()).ToHashSet().SetEquals(["泰勒斯", "留基伯"]),
            "Research-only figures must not disappear from coverage or acquire a fixed-node role.");
        Check(paths.Select(path => path.GetProperty("sample_id").GetString()).Distinct().Count() == 12, "Sample IDs must be unique.");
        foreach (string entry in WesternRouteCatalog.EntryThinkerIds)
            Check(paths.Count(path => path.GetProperty("entry_thinker_id").GetString() == entry) == 2, "Each entry must retain both approved sample paths.");
        foreach (JsonElement path in paths)
        {
            string sequence = path.GetProperty("original_sequence").GetString()!;
            Check(sequence.Contains("第一幕固定") && sequence.Contains("第二幕固定") && sequence.Contains("第三幕固定") && sequence.Contains("第四幕"), "Preserve the complete source, including future echoes without enabling them.");
            Check(path.GetProperty("implementation_status").GetString() == "REQUIRES_EDGE_REVIEW", "Sample prose is not an audited runtime graph.");
        }
        Check(root.GetProperty("source_hashes").GetArrayLength() == 2
            && root.GetProperty("source_hashes").EnumerateArray().All(source => Regex.IsMatch(source.GetProperty("sha256").GetString()!, "^[0-9A-F]{64}$")), "Both source snapshots must be identifiable.");
        Console.WriteLine("Western research coverage checks passed: 89 people, six anchors, twelve source-preserving paths.");
    }
}
