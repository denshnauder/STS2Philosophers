namespace STS2Philosophers;

// Successor exercises are separate from the entry relic. No automatic assignment yet.
internal static class WesternBeingPracticePolicy
{
    public static WesternPracticeReward Evaluate(string thinkerId, IReadOnlyList<WesternPracticePlay> plays)
    {
        int n = plays.Count;
        if (n == 0 || plays.Any(p => p.Kind == WesternPracticeCardKind.Other)) return default;
        var kinds = plays.Select(p => p.Kind).ToArray();
        int names = plays.Select(p => p.CardModelId).Distinct(StringComparer.Ordinal).Count();
        bool alternating = kinds.Zip(kinds.Skip(1), (a, b) => a != b).All(x => x);
        bool pairs = n % 2 == 0 && Enumerable.Range(0, n / 2).All(i => kinds[i * 2] == kinds[i * 2 + 1]);
        var a = WesternPracticeCardKind.Attack;
        var s = WesternPracticeCardKind.Skill;
        return thinkerId switch
        {
            "PARMENIDES" when n == 3 && names == 1 => new(Block: 6),
            "GOTTFRIED_WILHELM_LEIBNIZ" when n == 3 && names == 3 && kinds.Distinct().Count() == 3 => new(Block: 6),
            "ARISTOTLE" when kinds.SequenceEqual([s, s, a]) => new(Energy: 1, Block: 3),
            "DEMOCRITUS" when n == 4 && names == 2 && plays.GroupBy(p => p.CardModelId).All(g => g.Count() == 2) => new(Draw: 1, Block: 3),
            "EMPEDOCLES" when n == 4 && kinds.Distinct().Count() == 3 => new(Block: 6),
            "BARUCH_SPINOZA" when n >= 4 && pairs && kinds.Contains(a) && kinds.Contains(s) => new(Energy: 1),
            "GEORG_WILHELM_FRIEDRICH_HEGEL" when kinds.SequenceEqual([a, s, a, s]) => new(Draw: 1, Block: 4),
            "EPICURUS" when n == 2 && names == 2 => new(Block: 4),
            "GILLES_DELEUZE" when n >= 4 && names == n && alternating => new(Energy: 1, Draw: 1),
            _ => default,
        };
    }
}
