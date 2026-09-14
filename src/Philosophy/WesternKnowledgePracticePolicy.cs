namespace STS2Philosophers;

// Game exercises for specific problem faces, not historical claims or entry overrides.
internal static class WesternKnowledgePracticePolicy
{
    public static WesternPracticeReward Evaluate(string thinkerId, IReadOnlyList<WesternPracticePlay> plays,
        IReadOnlyList<WesternPracticeCardKind> previousKinds)
    {
        if (plays.Count == 0 || plays.Any(p => !Enum.IsDefined(p.Kind))) return default;
        var kinds = plays.Select(p => p.Kind).ToArray();
        var a = WesternPracticeCardKind.Attack;
        var s = WesternPracticeCardKind.Skill;
        var p = WesternPracticeCardKind.Power;
        return thinkerId switch
        {
            "SEXTUS_EMPIRICUS" when plays.Count == 2 && kinds[0] != kinds[1] => new(Draw: 1),
            "DESCARTES" when kinds.SequenceEqual([s, a, a])
                && plays[1].CardModelId == plays[2].CardModelId => new(Draw: 1, Block: 3),
            "DAVID_HUME" when plays.Count == 3 && plays[0].CardModelId == plays[1].CardModelId
                && plays[1].CardModelId != plays[2].CardModelId => new(Energy: 1),
            "IMMANUEL_KANT" when plays.Count == 3 && kinds.Contains(a) && kinds.Contains(s) && kinds.Contains(p)
                && kinds.SequenceEqual(previousKinds) => new(Draw: 2),
            _ => default,
        };
    }
}
