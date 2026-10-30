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
        ["ALBERT_CAMUS", "JUDITH_BUTLER"];

    // Routes whose default line the graph can already walk end to end with no
    // question mark. Moving a route onto this list is the deliverable of each
    // migration step; the complement is the remaining gap.
    private static readonly string[] Walkable =
    [
        "EXPERIENCE_AND_SELF",
        "AUTONOMY_AND_HISTORY",
        "NATURE_AND_BECOMING",
        "WILL_AND_VALUE",
        "INDIVIDUAL_COMMITMENT",
        "APPEARANCE_AND_INTERPRETATION",
        "EMBODIED_PERCEPTION",
        "FORMAL_LANGUAGE",
        "ORDINARY_SPEECH",
        "STRUCTURE_AND_EXCLUSION",
        "UNCONSCIOUS_AND_NORM",
        "EDUCATION_AND_SITUATION",
        "CHECKS_AND_JUSTICE",
        "CONSEQUENCE_AND_FREEDOM",
        "SYMPATHY_AND_PUBLIC_ACTION",
        "SOCIAL_CRITIQUE",
        "GENEALOGY_AND_POWER",
        "SCIENTIFIC_MODEL",
        "SOCIAL_KNOWLEDGE",
        "FALLIBLE_INQUIRY",
    ];

    // A run that follows the default line never takes a question node, so its
    // route is never named by the first act's question. This list is every route
    // whose act-two figure therefore stays out of reach: the act-two handover is
    // route-scoped, and a run without route identity cannot see it. The list is
    // asserted so the day act one names the route this check has to be updated.
    private static readonly string[] CannotReachActTwo =
    [
        "EXPERIENCE_AND_SELF", "AUTONOMY_AND_HISTORY", "NATURE_AND_BECOMING",
        "WILL_AND_VALUE", "INDIVIDUAL_COMMITMENT", "APPEARANCE_AND_INTERPRETATION",
        "EMBODIED_PERCEPTION", "FORMAL_LANGUAGE", "ORDINARY_SPEECH",
        "STRUCTURE_AND_EXCLUSION", "UNCONSCIOUS_AND_NORM", "CHECKS_AND_JUSTICE",
        "CONSEQUENCE_AND_FREEDOM", "SYMPATHY_AND_PUBLIC_ACTION", "SOCIAL_CRITIQUE",
        "GENEALOGY_AND_POWER", "SCIENTIFIC_MODEL", "SOCIAL_KNOWLEDGE",
        "FALLIBLE_INQUIRY",
    ];

    // Every route names the figure it meets at the second and third acts. Where no
    // fixed edge reaches that figure at all, the route cannot be walked however it
    // is named; those are content gaps, not code gaps.
    private static readonly string[] HandoverGaps =
    [
        "ORDINARY_SPEECH act three LUDWIG_WITTGENSTEIN",
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
        var thinkers = new HashSet<string>(graph.Nodes.Select(node => (string)node.ThinkerId), StringComparer.Ordinal);
        string ThinkerOf(string nodeId) => graph.Nodes.First(node => node.NodeId == nodeId).ThinkerId;
        foreach (WesternRouteDefinition route in table.Routes)
        {
            Check(nodes.Contains(route.EntryNodeId), $"{route.RouteId} names an entry node that exists.");
            foreach (string figure in new[] { route.ActTwoFigure, route.ActThreeFigure, route.ClosingFigure })
                Check(thinkers.Contains(figure) || MissingFigures.Contains(figure),
                    $"{route.RouteId} names {figure}, which needs either a node or a place on the gap list.");
        }

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (WesternRouteDefinition route in table.Routes)
            foreach (string figure in new[] { route.ActTwoFigure, route.ActThreeFigure, route.ClosingFigure })
                if (!thinkers.Contains(figure)) missing.Add(figure);
        var walkable = new List<string>();
        foreach (WesternRouteDefinition route in table.Routes)
        {
            WesternJourneyState state = graph.Start(route.EntryNodeId);
            state.RouteId = route.RouteId;
            state.LastFixedAct = 1;
            bool ok = true;
            string previousFigure = route.ActOneFigure;
            foreach ((int act, string figure) in new[] { (2, route.ActTwoFigure), (3, route.ActThreeFigure) })
            {
                // A route whose next act stays with the same figure has nothing to
                // hand over; the act passes without an edge.
                if (figure == previousFigure) continue;
                previousFigure = figure;
                string? edgeId = graph.GetEligibleEdges(state, "Fixed")
                    .Where(edge => edge.Act == act && ThinkerOf(edge.ToNodeId) == figure)
                    .Select(edge => edge.EdgeId)
                    .FirstOrDefault();
                if (edgeId is null || !graph.TryApply(state, edgeId)) { ok = false; break; }
            }
            if (ok) walkable.Add(route.RouteId);
        }
        Check(walkable.SequenceEqual(Walkable),
            $"The walkable default lines are exactly the migrated ones, but the graph walks: {string.Join(", ", walkable)}.");

        // The walk above proves each act can be reached from the act before it.
        // It does not prove the two handovers meet: a route can hand over into
        // one node and then look for the next handover somewhere else, which
        // reads as "the room never opens" only when the act is played. Check the
        // seam directly.
        var seams = new List<string>();
        int seamsChecked = 0;
        foreach (WesternRouteDefinition route in table.Routes)
        {
            WesternJourneyState state = graph.Start(route.EntryNodeId);
            state.RouteId = route.RouteId;
            state.LastFixedAct = 1;
            string previousFigure = route.ActOneFigure;
            var placed = new Dictionary<int, WesternGraphEdge>();
            foreach ((int act, string figure) in new[] { (2, route.ActTwoFigure), (3, route.ActThreeFigure) })
            {
                if (figure == previousFigure) continue;
                previousFigure = figure;
                WesternGraphEdge? edge = graph.GetEligibleEdges(state, "Fixed")
                    .FirstOrDefault(candidate => candidate.Act == act && ThinkerOf(candidate.ToNodeId) == figure);
                if (edge is null) break;
                placed[act] = edge;
                if (!graph.TryApply(state, edge.EdgeId)) break;
            }
            if (!placed.TryGetValue(2, out WesternGraphEdge? second)
                || !placed.TryGetValue(3, out WesternGraphEdge? third)) continue;
            seamsChecked++;
            if (third.FromNodeId != second.ToNodeId)
                seams.Add($"{route.RouteId}: act two lands on {second.ToNodeId}, act three starts from {third.FromNodeId}");
        }
        // A seam check that silently skips most routes proves nothing.
        Check(seamsChecked >= 15, $"Only {seamsChecked} routes have both handovers to join.");
        Check(seams.Count == 0, "Every handover must start where the one before it landed: " + string.Join("; ", seams) + ".");

        // The default line is for runs that took no question. A run that answered
        // the first act's question has chosen, even when the answer named no
        // single line, and must not be renamed onto the default.
        WesternJourneyState answered = graph.Start(
            table.Routes.First(route => route.EntryNodeId.EndsWith("DESCARTES__SELF_AND_OTHER__CORE", StringComparison.Ordinal)).EntryNodeId);
        answered.RouteRules = true;
        answered.ResolvedQuestionActs.Add(1);
        WesternRouteGraph.NameDefaultRoute(answered);
        Check(string.IsNullOrEmpty(answered.RouteId),
            "A run that answered the first act's question must not fall back to the default line.");
        WesternJourneyState silent = graph.Start(answered.EntryNodeId);
        silent.RouteRules = true;
        WesternRouteGraph.NameDefaultRoute(silent);
        Check(!string.IsNullOrEmpty(silent.RouteId),
            "A run that took no question must join its entry's default line.");

        var unreachable = new List<string>();
        foreach (WesternRouteDefinition route in table.Routes)
        {
            WesternJourneyState plain = graph.Start(route.EntryNodeId);
            plain.LastFixedAct = 2;
            bool any = graph.GetEligibleEdges(plain, "Fixed")
                .Any(edge => edge.Act == 2 && ThinkerOf(edge.ToNodeId) == route.ActTwoFigure);
            if (!any) unreachable.Add(route.RouteId);
        }
        Check(unreachable.SequenceEqual(CannotReachActTwo),
            "A default line reaches its second act only after act one names the route, but the "
            + $"routes that still cannot are: {string.Join(", ", unreachable)}.");

        // A run started under the route rules never walks an edge written before
        // routes existed: the second act's legacy handovers belong to no mainline.
        WesternJourneyState routed = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        routed.RouteRules = true;
        routed.LastFixedAct = 2;
        Check(!graph.GetEligibleEdges(routed, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_023"),
            "A route-rules run does not walk the legacy Descartes-to-Spinoza handover.");
        WesternJourneyState legacy = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        legacy.LastFixedAct = 2;
        Check(graph.GetEligibleEdges(legacy, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_023"),
            "A save written before routes existed still sees the handover it was saved with.");

        var gaps = new List<string>();
        foreach (WesternRouteDefinition route in table.Routes)
        {
            foreach (var (act, figure) in new[] { (2, route.ActTwoFigure), (3, route.ActThreeFigure) })
            {
                bool any = graph.Edges.Any(edge => edge.Act == act && edge.Slot == "Fixed"
                    && graph.GetNode(edge.ToNodeId).ThinkerId == figure);
                if (!any) gaps.Add($"{route.RouteId} act {(act == 2 ? "two" : "three")} {figure}");
            }
        }
        Check(gaps.SequenceEqual(HandoverGaps),
            "These routes name a figure no fixed edge reaches: "
            + string.Join(", ", gaps) + ".");

        Check(missing.SetEquals(MissingFigures),
            "The gap list is exactly the set of named figures that have no node.");

        // Route scoping: an edge that names routes is closed to a run following a
        // different one, and stays open to saves that predate route identity.
        WesternJourneyState journey = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        Check(!graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_091"),
            "A save written before routes existed does not see route-only content.");
        journey.RouteId = "EXPERIENCE_AND_SELF";
        Check(graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_091"),
            "The route that owns the edge keeps it open.");
        journey.RouteId = "NATURE_AND_BECOMING";
        Check(!graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_091"),
            "A run following another route does not see the edge.");

        // A run that takes the first act's Locke question names its mainline.
        var chooser = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        Check(graph.TryApply(chooser, "WESTERN_EDGE_026")
            && chooser.RouteId == "EXPERIENCE_AND_SELF",
            "The first act's question names the route that continues through it.");
        Check(graph.GetEligibleEdges(chooser, "Fixed").Where(edge => edge.Act == 2)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_093"),
            "Once named, the route's second act handover opens from Locke.");

        // The experience route's default line, with no question mark at all:
        // Descartes' second act hands over to Locke, and Locke's third to Hume.
        var line = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        line.RouteId = "EXPERIENCE_AND_SELF";
        line.LastFixedAct = 2;
        Check(graph.GetEligibleEdges(line, "Fixed").Where(edge => edge.Act == 2)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_091"),
            "The second act offers the route's own fixed handover.");
        // Husserl and Freud are act-one arrivals that a first-act question can
        // still reach; the route's own second-act handover is the only act-two one.
        Check(graph.GetEligibleEdges(line, "Fixed").Where(edge => edge.Act == 2)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_091"),
            "The route's own handover belongs to the second act.");
        Check(graph.TryApply(line, "WESTERN_EDGE_091") && line.CurrentNodeId == "JOHN_LOCKE__SELF_AND_OTHER__CORE"
            && line.LastFixedAct == 3,
            "Taking it lands on Locke and spends the second act's position.");
        Check(graph.GetEligibleEdges(line, "Fixed").Where(edge => edge.Act == 3)
                .Select(edge => edge.EdgeId).Contains("WESTERN_EDGE_027"),
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
