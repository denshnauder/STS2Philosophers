using STS2Philosophers;

internal static class WesternBeingChecks
{
    public static void Run()
    {
        static WesternPracticePlay[] Plays(string types, string names) => types.Select((t, i) =>
            new WesternPracticePlay(i.ToString(), names[i].ToString(), t switch
            { 'A' => WesternPracticeCardKind.Attack, 'S' => WesternPracticeCardKind.Skill,
                'P' => WesternPracticeCardKind.Power, _ => WesternPracticeCardKind.Other }, false)).ToArray();
        (string Id, string Good, string Names, string Bad, string BadNames, WesternPracticeReward Reward)[] cases =
        [
            ("PARMENIDES", "AAA", "aaa", "AAA", "aab", new(Block: 6)),
            ("ARISTOTLE", "SSA", "abc", "SAS", "abc", new(Energy: 1, Block: 3)),
            ("DEMOCRITUS", "AASS", "aabb", "AASS", "aaab", new(Draw: 1, Block: 3)),
            ("EMPEDOCLES", "AASP", "abcd", "AASS", "abcd", new(Block: 6)),
            ("BARUCH_SPINOZA", "AASS", "abcd", "ASAS", "abcd", new(Energy: 1)),
            ("GEORG_WILHELM_FRIEDRICH_HEGEL", "ASAS", "abcd", "SASA", "abcd", new(Draw: 1, Block: 4)),
            ("EPICURUS", "AS", "ab", "AS", "aa", new(Block: 4)),
            ("GILLES_DELEUZE", "ASPA", "abcd", "ASPA", "abca", new(Energy: 1, Draw: 1)),
        ];
        foreach (var c in cases)
        {
            if (WesternBeingPracticePolicy.Evaluate(c.Id, Plays(c.Good, c.Names)) != c.Reward
                || WesternBeingPracticePolicy.Evaluate(c.Id, Plays(c.Bad, c.BadNames)) != default
                || WesternBeingPracticePolicy.Evaluate(c.Id, []) != default)
                throw new InvalidOperationException($"Being successor practice failed: {c.Id}");
        }
        if (WesternBeingPracticePolicy.Evaluate("UNKNOWN", Plays("ASAS", "abcd")) != default)
            throw new InvalidOperationException("Unknown thinkers must not receive a fallback practice.");
        Console.WriteLine("Western being successor checks passed: eight distinct exercises and violation boundaries.");
    }
}
