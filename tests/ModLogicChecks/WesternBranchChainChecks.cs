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
        // cannot name one: the design asks for a direction inside the event. The
        // direction option does not exist yet, so this walk stands in for it and
        // the line stays a known gap in the route audit.
        CheckTurn(graph, "APPEARANCE_AND_INTERPRETATION", "DESCARTES__SELF_AND_OTHER__CORE",
            ["WESTERN_EDGE_160", "WESTERN_EDGE_164", "WESTERN_EDGE_161"],
            "HANS_GEORG_GADAMER__SELF_AND_OTHER__CORE", direction: true);

        CheckTurn(graph, "SCIENTIFIC_MODEL", "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE",
            ["WESTERN_EDGE_165", "WESTERN_EDGE_166", "WESTERN_EDGE_167"],
            "THOMAS_KUHN__KNOWLEDGE_AND_DOUBT__CORE");
        CheckTurn(graph, "CONSEQUENCE_AND_FREEDOM", "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            ["WESTERN_EDGE_168", "WESTERN_EDGE_169", "WESTERN_EDGE_170"],
            "RAWLS__FREEDOM_AND_INSTITUTIONS__CORE");
        CheckTurn(graph, "CHECKS_AND_JUSTICE", "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            ["WESTERN_EDGE_171", "WESTERN_EDGE_172", "WESTERN_EDGE_173"],
            "RAWLS__FREEDOM_AND_INSTITUTIONS__CORE");
        CheckTurn(graph, "SYMPATHY_AND_PUBLIC_ACTION", "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            ["WESTERN_EDGE_174", "WESTERN_EDGE_175", "WESTERN_EDGE_176"],
            "HANNAH_ARENDT__FREEDOM_AND_INSTITUTIONS__CORE");

        CheckTurn(graph, "SOCIAL_CRITIQUE", "ROUSSEAU__HISTORY_AND_POWER__CORE",
            ["WESTERN_EDGE_177", "WESTERN_EDGE_178", "WESTERN_EDGE_179"],
            "THEODOR_ADORNO__HISTORY_AND_POWER__CORE");
        CheckTurn(graph, "GENEALOGY_AND_POWER", "ROUSSEAU__HISTORY_AND_POWER__CORE",
            ["WESTERN_EDGE_180", "WESTERN_EDGE_181", "WESTERN_EDGE_182"],
            "HANNAH_ARENDT__FREEDOM_AND_INSTITUTIONS__CORE");
        CheckTurn(graph, "FORMAL_LANGUAGE", "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            ["WESTERN_EDGE_183", "WESTERN_EDGE_184", "WESTERN_EDGE_185"],
            "LUDWIG_WITTGENSTEIN__LANGUAGE_AND_MEANING__CORE");
        CheckTurn(graph, "STRUCTURE_AND_EXCLUSION", "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            ["WESTERN_EDGE_186", "WESTERN_EDGE_187", "WESTERN_EDGE_188"],
            "JACQUES_DERRIDA__LANGUAGE_AND_MEANING__CORE");

        CheckTurn(graph, "UNCONSCIOUS_AND_NORM", "DESCARTES__SELF_AND_OTHER__CORE",
            ["WESTERN_EDGE_189", "WESTERN_EDGE_190", "WESTERN_EDGE_191"],
            "JUDITH_BUTLER__SELF_AND_OTHER__CORE");
        CheckTurn(graph, "EDUCATION_AND_SITUATION", "ROUSSEAU__HISTORY_AND_POWER__CORE",
            ["WESTERN_EDGE_192", "WESTERN_EDGE_193", "WESTERN_EDGE_194"],
            "JUDITH_BUTLER__SELF_AND_OTHER__CORE");

        CheckTurn(graph, "EMBODIED_PERCEPTION", "DESCARTES__SELF_AND_OTHER__CORE",
            ["WESTERN_EDGE_160", "WESTERN_EDGE_195", "WESTERN_EDGE_196"],
            "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE", direction: true);
        CheckTurn(graph, "SOCIAL_KNOWLEDGE", "ROUSSEAU__HISTORY_AND_POWER__CORE",
            ["WESTERN_EDGE_197", "WESTERN_EDGE_198", "WESTERN_EDGE_199"],
            "THOMAS_KUHN__KNOWLEDGE_AND_DOUBT__CORE");

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
            if (direction && edgeId == edgeIds[0]) state.RouteId = routeId;
        }
        Check(state.RouteId == routeId, $"{routeId}: the first act's question must name the line, not '{state.RouteId}'.");
        Check(state.CurrentNodeId == closingNode, $"{routeId}: the turn must end on {closingNode}.");
        Check(graph.IsComplete(state), $"{routeId}: the turn must reach a legitimate third-act conclusion.");
        Check(graph.GetEligibleEdges(state, "Fixed").Count == 0, $"{routeId}: a finished turn leaves no fixed step behind.");
    }
}
