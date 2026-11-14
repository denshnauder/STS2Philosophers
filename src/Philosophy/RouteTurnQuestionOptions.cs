namespace STS2Philosophers;

/// <summary>
/// The options the first act's question offers. The graph says which question
/// edges can name a line of this entry; this decides which of them the event has
/// a page for, so the event and the patch that opens it always agree.
/// </summary>
internal static class RouteTurnQuestionOptions
{
    internal const string LocalizationRoot = "ROUTE_TURN_QUESTION";
    internal const string InstanceIdPrefix = "ROUTE_TURN_QUESTION";
    internal const string StayKey = "STAY";
    internal const string AppearanceKey = "TO_HUSSERL_APPEARANCE";
    internal const string EmbodiedKey = "TO_HUSSERL_EMBODIED";

    /// <summary>
    /// Figures a first-act question may name. A fixed branch is chosen from the
    /// run seed, so it cannot stand in for the player's own turn.
    /// </summary>
    private static readonly Dictionary<string, string> TurnKeys = new(StringComparer.Ordinal)
    {
        ["IMMANUEL_KANT"] = "TO_KANT",
        ["SIGMUND_FREUD"] = "TO_FREUD",
        ["FRANCIS_BACON"] = "TO_BACON",
        ["KARL_MARX"] = "TO_MARX",
        ["FRIEDRICH_NIETZSCHE"] = "TO_NIETZSCHE",
        ["MARY_WOLLSTONECRAFT"] = "TO_WOLLSTONECRAFT",
        ["FERDINAND_DE_SAUSSURE"] = "TO_SAUSSURE",
        ["GOTTLOB_FREGE"] = "TO_FREGE",
        ["ADAM_SMITH"] = "TO_SMITH",
        ["SOREN_KIERKEGAARD"] = "TO_KIERKEGAARD",
        ["JEREMY_BENTHAM"] = "TO_BENTHAM",
        ["BARUCH_SPINOZA"] = "TO_SPINOZA",
        ["MONTESQUIEU"] = "TO_MONTESQUIEU",
    };

    internal sealed record Choice(string Key, string? EdgeId, string? RouteId);

    /// <summary>
    /// One entry per option, in a stable order: the turn options the graph offers,
    /// then the two directions the shared Husserl figure asks about, then staying.
    /// </summary>
    internal static IReadOnlyList<Choice> For(WesternJourneyState journey, WesternRouteGraph graph)
    {
        var choices = new List<Choice>();
        foreach (WesternGraphEdge edge in graph.FirstActTurnOptions(journey))
        {
            string thinker = graph.GetNode(edge.ToNodeId).ThinkerId;
            if (TurnKeys.TryGetValue(thinker, out string? key))
                choices.Add(new Choice(key, edge.EdgeId,
                    thinker switch
                    {
                        "ADAM_SMITH" => "SYMPATHY_AND_PUBLIC_ACTION",
                        "MONTESQUIEU" => "CHECKS_AND_JUSTICE",
                        _ => null,
                    }));
        }
        // The fallible-inquiry line begins with Hume's fixed handover rather than
        // a question edge, so the graph cannot derive this turn from slot alone.
        if (journey.EntryNodeId == "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_069"))
            choices.Add(new Choice("TO_PEIRCE", "WESTERN_EDGE_069", "FALLIBLE_INQUIRY"));
        // Ordinary language and nature are real first-act directions even though
        // their first fixed handover belongs to the next act. Naming the line
        // spends the first-act question without adding a fixed slot or moving the
        // player onto a figure early.
        if (journey.EntryNodeId == "ARISTOTLE__LANGUAGE_AND_MEANING__CORE"
            && WesternRouteTable.Instance.Routes.Any(route => route.RouteId == "ORDINARY_SPEECH"))
            choices.Add(new Choice("TO_WITTGENSTEIN", null, "ORDINARY_SPEECH"));
        // Social critique also has a real fixed first-act branch (083). Offering
        // it here is what lets the player choose Adorno's handover instead of
        // only inheriting it from an old save.
        if (journey.EntryNodeId == "ROUSSEAU__HISTORY_AND_POWER__CORE"
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_083"))
            choices.Add(new Choice("TO_ADORNO", "WESTERN_EDGE_083", "SOCIAL_CRITIQUE"));
        // Husserl stands on two lines, so his question names neither by itself: it
        // asks which one, and the answer is what names the line.
        if (graph.GetEligibleEdges(journey, "Question")
                .Any(edge => edge.EdgeId == "WESTERN_EDGE_160"))
        {
            choices.Add(new Choice(AppearanceKey, "WESTERN_EDGE_160", "APPEARANCE_AND_INTERPRETATION"));
            choices.Add(new Choice(EmbodiedKey, "WESTERN_EDGE_160", "EMBODIED_PERCEPTION"));
        }
        choices.Add(new Choice(StayKey, null, null));
        return choices;
    }

    /// <summary>True when the question has something to ask beyond staying.</summary>
    internal static bool HasTurn(IReadOnlyList<Choice> choices) => choices.Count > 1;

    internal static readonly string[] AllKeys =
        ["TO_KANT", "TO_FREUD", "TO_BACON", "TO_MARX", "TO_ADORNO", "TO_NIETZSCHE", "TO_WOLLSTONECRAFT",
            "TO_SAUSSURE", "TO_FREGE", "TO_WITTGENSTEIN", "TO_SPINOZA", "TO_SMITH", "TO_PEIRCE",
            "TO_KIERKEGAARD", "TO_BENTHAM", "TO_MONTESQUIEU", AppearanceKey, EmbodiedKey, StayKey];

    internal static string OptionKey(Choice choice) => $"{LocalizationRoot}.pages.INVITATION.options.{choice.Key}";
}
