namespace STS2Philosophers;

/// <summary>
/// The options the first act's question offers. The graph says which question
/// edges can name a line of this entry; this decides which of them the event has
/// a page for, so the event and the patch that opens it always agree.
/// </summary>
internal static class RouteTurnQuestionOptions
{
    internal const string LocalizationRoot = "ROUTE_TURN_QUESTION";
    internal const string StayKey = "STAY";
    internal const string AppearanceKey = "TO_HUSSERL_APPEARANCE";
    internal const string EmbodiedKey = "TO_HUSSERL_EMBODIED";

    /// <summary>Figures whose line no first-act branch reaches, so only a question can open it.</summary>
    private static readonly Dictionary<string, string> TurnKeys = new(StringComparer.Ordinal)
    {
        ["IMMANUEL_KANT"] = "TO_KANT",
        ["SIGMUND_FREUD"] = "TO_FREUD",
        ["FRANCIS_BACON"] = "TO_BACON",
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
            if (TurnKeys.TryGetValue(thinker, out string? key)) choices.Add(new Choice(key, edge.EdgeId, null));
        }
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
        ["TO_KANT", "TO_FREUD", "TO_BACON", AppearanceKey, EmbodiedKey, StayKey];

    internal static string OptionKey(Choice choice) => $"{LocalizationRoot}.pages.INVITATION.options.{choice.Key}";
}
