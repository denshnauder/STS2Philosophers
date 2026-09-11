using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed record WesternProblem(
    [property: JsonPropertyName("problem_id")] string ProblemId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("entry_thinker_id")] string EntryThinkerId);

internal static class WesternRouteCatalog
{
    private const string ResourceName = "STS2Philosophers.config.western_routes.json";
    private static readonly Lazy<IReadOnlyList<WesternProblem>> Catalog = new(LoadEmbedded);

    public static IReadOnlyList<WesternProblem> Problems => Catalog.Value;

    public static IReadOnlyList<string> EntryThinkerIds { get; } = Array.AsReadOnly(new[]
    {
        "HERACLITUS", "SOCRATES", "PLATO", "DESCARTES", "ARISTOTLE", "ROUSSEAU",
    });

    public static bool IsEntryProblem(string thinkerId, string problemId) =>
        Problems.Any(problem => problem.EntryThinkerId == thinkerId && problem.ProblemId == problemId);

    internal static IReadOnlyList<WesternProblem> ParseJson(string json)
    {
        List<WesternProblem?> rows = JsonSerializer.Deserialize<List<WesternProblem?>>(json)
            ?? throw new InvalidDataException("Western route catalog is empty.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (WesternProblem? row in rows)
        {
            if (row is null || !IsIdentifier(row.ProblemId)
                || string.IsNullOrWhiteSpace(row.DisplayName)
                || !EntryThinkerIds.Contains(row.EntryThinkerId, StringComparer.Ordinal)
                || !ids.Add(row.ProblemId))
            {
                throw new InvalidDataException("Invalid or duplicate western problem, name, or entry thinker.");
            }
        }

        if (rows.Count != 7 || rows.Select(row => row!.EntryThinkerId).Distinct(StringComparer.Ordinal).Count() != 6)
        {
            throw new InvalidDataException("The western baseline requires seven problems and six distinct entry thinkers.");
        }

        return rows.Select(row => row!).ToList().AsReadOnly();
    }

    private static bool IsIdentifier(string? id) => !string.IsNullOrEmpty(id)
        && id[0] is >= 'A' and <= 'Z'
        && id.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private static IReadOnlyList<WesternProblem> LoadEmbedded()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing western route catalog: {ResourceName}");
        using StreamReader reader = new(stream);
        return ParseJson(reader.ReadToEnd());
    }
}
