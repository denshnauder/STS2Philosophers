namespace STS2Philosophers;

internal static class WesternNodePracticePolicy
{
    private static readonly IReadOnlyDictionary<string, WesternGraphNode> Nodes = WesternRouteGraph.LoadEmbedded()
        .Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

    public static WesternPracticeReward Evaluate(string nodeId, IReadOnlyList<WesternPracticePlay> plays,
        IReadOnlyList<WesternPracticeCardKind> previousKinds, int previousCards)
    {
        // The full graph node identity includes stage. No thinker-only or future-stage fallback.
        if (!Nodes.TryGetValue(nodeId, out WesternGraphNode? node) || previousCards < 0
            || plays.Any(play => play is null || !Enum.IsDefined(play.Kind)
                || string.IsNullOrWhiteSpace(play.PlayId) || string.IsNullOrWhiteSpace(play.CardModelId))
            || plays.Select(play => play.PlayId).Distinct(StringComparer.Ordinal).Count() != plays.Count
            || previousKinds.Any(kind => !Enum.IsDefined(kind))
            || (previousKinds.Count > 0 && previousKinds.Count != previousCards)) return default;
        if (WesternRouteCatalog.IsEntryProblem(node.ThinkerId, node.ProblemId))
        {
            return new WesternPracticeState { ProblemId = node.ProblemId, Plays = plays.ToList(), PreviousCards = previousCards }.Evaluate();
        }
        return node.ProblemId switch
        {
            "BEING_AND_CHANGE" => WesternBeingPracticePolicy.Evaluate(node.ThinkerId, plays),
            "KNOWLEDGE_AND_DOUBT" => WesternKnowledgePracticePolicy.Evaluate(node.ThinkerId, plays, previousKinds),
            _ => WesternSocialPracticePolicy.Evaluate(node.ProblemId, node.ThinkerId, plays),
        };
    }
}
