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

    // Routes whose default line the graph can already walk end to end with no
    // question mark. Moving a route onto this list is the deliverable of each
    // migration step; the complement is the remaining gap.
    private static readonly string[] Walkable =
    [
        "EXPERIENCE_AND_SELF",
        "EDUCATION_AND_SITUATION",
    ];

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
        var walkable = new List<string>();
        foreach (WesternRouteDefinition route in table.Routes)
        {
            WesternJourneyState state = graph.Start(route.EntryNodeId);
            state.RouteId = route.RouteId;
            state.LastFixedAct = 1;
            bool ok = true;
            foreach ((int act, string figure) in new[] { (2, route.ActTwoFigure), (3, route.ActThreeFigure) })
            {
                string? edgeId = graph.GetEligibleEdges(state, "Fixed")
                    .Where(edge => edge.Act == act && edge.ToNodeId.StartsWith(figure + "__", StringComparison.Ordinal))
                    .Select(edge => edge.EdgeId)
                    .FirstOrDefault();
                if (edgeId is null || !graph.TryApply(state, edgeId)) { ok = false; break; }
            }
            if (ok) walkable.Add(route.RouteId);
        }
        Check(walkable.SequenceEqual(Walkable),
            $"The walkable default lines are exactly the migrated ones, but the graph walks: {string.Join(", ", walkable)}.");

        Check(missing.SetEquals(MissingFigures),
            "The gap list is exactly the set of named figures that have no node.");

        // Route scoping: an edge that names routes is closed to a run following a
        // different one, and stays open to saves that predate route identity.
        WesternJourneyState journey = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        Check(graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_023"),
            "A legacy run with no route identity keeps every edge open.");
        journey.RouteId = "NATURE_AND_BECOMING";
        Check(graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_023"),
            "The route that owns the edge keeps it open.");
        journey.RouteId = "EXPERIENCE_AND_SELF";
        Check(!graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_023"),
            "A run following another route does not see the edge.");

        // The experience route's default line, with no question mark at all:
        // Descartes' second act hands over to Locke, and Locke's third to Hume.
        var line = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        line.RouteId = "EXPERIENCE_AND_SELF";
        line.LastFixedAct = 2;
        Check(graph.GetEligibleEdges(line, "Fixed").Select(edge => edge.EdgeId).SequenceEqual(["WESTERN_EDGE_072", "WESTERN_EDGE_087", "WESTERN_EDGE_091"]),
            "The second act offers exactly the route's own fixed handover.");
        // Husserl and Freud are act-one arrivals that a first-act question can
        // still reach; the route's own second-act handover is the only act-two one.
        Check(graph.GetEligibleEdges(line, "Fixed").Where(edge => edge.Act == 2)
                .Select(edge => edge.EdgeId).SequenceEqual(["WESTERN_EDGE_091"]),
            "Only the route's own handover belongs to the second act.");
        Check(graph.TryApply(line, "WESTERN_EDGE_091") && line.CurrentNodeId == "JOHN_LOCKE__SELF_AND_OTHER__CORE"
            && line.LastFixedAct == 3,
            "Taking it lands on Locke and spends the second act's position.");
        Check(graph.GetEligibleEdges(line, "Fixed").Where(edge => edge.Act == 3)
                .Select(edge => edge.EdgeId).SequenceEqual(["WESTERN_EDGE_027"]),
            "The third act offers the route's own closing handover.");
        Check(graph.TryApply(line, "WESTERN_EDGE_027")
            && line.CurrentNodeId == "DAVID_HUME__SELF_AND_OTHER__CORE" && line.LastFixedAct == 3,
            "The default line fills all three acts without a question mark.");

        // The education route is the other line the audit already called correct:
        // Rousseau hands the second act to Wollstonecraft and the third to Beauvoir.
        var education = graph.Start("ROUSSEAU__HISTORY_AND_POWER__CORE");
        education.RouteId = "EDUCATION_AND_SITUATION";
        education.LastFixedAct = 2;
        // Machiavelli is the shared second-act alternative for the Rousseau entry;
        // the route's own handover has to be among the offers.
        Check(graph.GetEligibleEdges(education, "Fixed").Where(edge => edge.Act == 2)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_040"),
            "The education route's second act offers Wollstonecraft.");
        Check(graph.TryApply(education, "WESTERN_EDGE_040")
            && education.CurrentNodeId == "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE",
            "Taking it lands on Wollstonecraft.");
        Check(graph.GetEligibleEdges(education, "Fixed").Where(edge => edge.Act == 3)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_042")
            && graph.TryApply(education, "WESTERN_EDGE_042")
            && education.CurrentNodeId == "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE",
            "The third act offers Beauvoir and lands there.");
    }
}
