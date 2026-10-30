using STS2Philosophers;

/// <summary>
/// The design table gives some lines a turn: the first act's question accepts a
/// figure the default chain reaches later, and the next two acts then follow
/// that figure's own successors. Those steps are edges now, so they have to be
/// walkable from the question to the closing figure, not only present in the
/// table.
/// </summary>
internal static class WesternBranchChainChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();

        CheckTurn(graph, "AUTONOMY_AND_HISTORY", "DESCARTES__SELF_AND_OTHER__CORE",
            ["WESTERN_EDGE_153", "WESTERN_EDGE_154", "WESTERN_EDGE_155"],
            "KARL_MARX__HISTORY_AND_POWER__EARLY");
        CheckTurn(graph, "NATURE_AND_BECOMING", "HERACLITUS__BEING_AND_CHANGE__CORE",
            ["WESTERN_EDGE_156", "WESTERN_EDGE_162", "WESTERN_EDGE_157"],
            "GILLES_DELEUZE__BEING_AND_CHANGE__CORE");
        CheckTurn(graph, "INDIVIDUAL_COMMITMENT", "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            ["WESTERN_EDGE_158", "WESTERN_EDGE_163", "WESTERN_EDGE_159"],
            "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE");
        // Two lines share the first act's Husserl figure, so the question alone
        // cannot name one: the event asks for the direction and names the line.
        CheckTurn(graph, "APPEARANCE_AND_INTERPRETATION", "DESCARTES__SELF_AND_OTHER__CORE",
            ["WESTERN_EDGE_160", "WESTERN_EDGE_164", "WESTERN_EDGE_161"],
            "HANS_GEORG_GADAMER__SELF_AND_OTHER__CORE", direction: true);

        // The default chain of a turned line still ends where the table says it
        // does: the turn's closing step is only open to a run that took the turn.
        WesternJourneyState defaultChain = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        defaultChain.RouteRules = true;
        // The line's default chain is the table's own order: Kant, then Hegel.
        defaultChain.RouteId = "AUTONOMY_AND_HISTORY";
        Check(graph.TryApply(defaultChain, "WESTERN_EDGE_095"), "The default chain takes the second act's fixed event.");
        Check(graph.TryApply(defaultChain, "WESTERN_EDGE_096"), "The default chain takes the third act's fixed event.");
        Check(graph.IsComplete(defaultChain) && !graph.GetEligibleEdges(defaultChain, "Fixed").Any(),
            "A line that ended on its own third-act figure does not keep a closing step open.");
    }

    private static void CheckTurn(WesternRouteGraph graph, string routeId, string entryNode,
        string[] edgeIds, string closingNode, bool direction = false)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        WesternJourneyState state = graph.Start(entryNode);
        state.RouteRules = true;
        foreach (string edgeId in edgeIds)
        {
            WesternGraphEdge edge = graph.GetEdge(edgeId);
            Check(graph.GetEligibleEdges(state, edge.Slot).Any(candidate => candidate.EdgeId == edgeId),
                $"{routeId}: the turn's {edge.Slot} step {edgeId} must be offered before it is taken.");
            Check(graph.TryApply(state, edgeId), $"{routeId}: the turn's step {edgeId} must apply.");
            if (direction && edgeId == edgeIds[0])
                Check(graph.TryNameRoute(state, routeId),
                    $"{routeId}: the shared first-act figure must let the event name its direction.");
        }
        Check(state.RouteId == routeId, $"{routeId}: the first act's question must name the line, not '{state.RouteId}'.");
        Check(state.CurrentNodeId == closingNode, $"{routeId}: the turn must end on {closingNode}.");
        Check(graph.IsComplete(state), $"{routeId}: the turn must reach a legitimate third-act conclusion.");
        Check(graph.GetEligibleEdges(state, "Fixed").Count == 0, $"{routeId}: a finished turn leaves no fixed step behind.");
    }
}
