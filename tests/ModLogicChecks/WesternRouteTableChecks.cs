using STS2Philosophers;

internal static class WesternRouteTableChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Figures the design's table names but the graph has no node for yet. The
    // list is asserted so that adding one of these figures fails this check and
    // forces the gap list to be updated with real content.
    private static readonly string[] MissingFigures =
        ["ALBERT_CAMUS", "ARTHUR_SCHOPENHAUER", "AUGUSTE_COMTE", "JUDITH_BUTLER"];

    public static void Run()
    {
        WesternRouteTable table = WesternRouteTable.Instance;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        Check(table.Routes.Count == 20, "The design's table has twenty default mainlines.");
        Check(table.Routes.Select(route => route.RouteId).Distinct().Count() == 20,
            "Every route has its own identity.");
        Check(table.Routes.Select(route => route.EntryNodeId).Distinct().Count() == 7,
            "Twenty routes share seven entries, which is why the entry alone cannot pick a route.");

        var nodes = new HashSet<string>(graph.Nodes.Select(node => (string)node.NodeId), StringComparer.Ordinal);
        foreach (WesternRouteDefinition route in table.Routes)
        {
            Check(nodes.Contains(route.EntryNodeId), $"{route.RouteId} names an entry node that exists.");
            foreach (string figure in new[] { route.ActTwoFigure, route.ActThreeFigure, route.ClosingFigure })
                Check(nodes.Any(node => node.StartsWith(figure + "__", StringComparison.Ordinal))
                    || MissingFigures.Contains(figure),
                    $"{route.RouteId} names {figure}, which needs either a node or a place on the gap list.");
        }

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (WesternRouteDefinition route in table.Routes)
            foreach (string figure in new[] { route.ActTwoFigure, route.ActThreeFigure, route.ClosingFigure })
                if (!nodes.Any(node => node.StartsWith(figure + "__", StringComparison.Ordinal))) missing.Add(figure);
        Check(missing.SetEquals(MissingFigures),
            "The gap list is exactly the set of named figures that have no node.");
    }
}
