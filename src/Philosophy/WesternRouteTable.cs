using System.Text.Json;

namespace STS2Philosophers;

/// <summary>One default mainline from the design's three-act table.</summary>
internal sealed class WesternRouteDefinition
{
    public string RouteId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EntryThinker { get; set; } = string.Empty;
    public string EntryProblem { get; set; } = string.Empty;
    public string ActOneFigure { get; set; } = string.Empty;
    public string ActTwoFigure { get; set; } = string.Empty;
    public string ActThreeFigure { get; set; } = string.Empty;
    public string ClosingFigure { get; set; } = string.Empty;

    public string EntryNodeId => $"{EntryThinker}__{EntryProblem}__CORE";
}

internal sealed class WesternRouteTable
{
    private static readonly Lazy<WesternRouteTable> Cached = new(LoadEmbedded);

    public static WesternRouteTable Instance => Cached.Value;

    public List<WesternRouteDefinition> Routes { get; private set; } = [];

    public static WesternRouteTable LoadEmbedded()
    {
        using Stream stream = typeof(WesternRouteTable).Assembly
            .GetManifestResourceStream("STS2Philosophers.config.western_mainlines.json")
            ?? throw new InvalidDataException("The western route table is missing from the assembly.");
        using JsonDocument document = JsonDocument.Parse(stream);
        var table = new WesternRouteTable();
        foreach (JsonElement element in document.RootElement.GetProperty("routes").EnumerateArray())
            table.Routes.Add(new WesternRouteDefinition
            {
                RouteId = element.GetProperty("route_id").GetString() ?? string.Empty,
                DisplayName = element.GetProperty("display_name").GetString() ?? string.Empty,
                EntryThinker = element.GetProperty("entry_thinker").GetString() ?? string.Empty,
                EntryProblem = element.GetProperty("entry_problem").GetString() ?? string.Empty,
                ActOneFigure = element.GetProperty("act_one_figure").GetString() ?? string.Empty,
                ActTwoFigure = element.GetProperty("act_two_figure").GetString() ?? string.Empty,
                ActThreeFigure = element.GetProperty("act_three_figure").GetString() ?? string.Empty,
                ClosingFigure = element.GetProperty("closing_figure").GetString() ?? string.Empty,
            });
        return table;
    }

    public WesternRouteDefinition? ForEntry(string nodeId) =>
        Routes.FirstOrDefault(route => route.EntryNodeId == nodeId);
}
