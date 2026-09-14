namespace STS2Philosophers;

// These are game exercises for the declared problem faces. They assert no historical links.
internal static class WesternSocialPracticePolicy
{
    public static WesternPracticeReward Evaluate(string problemId, string thinkerId, IReadOnlyList<WesternPracticePlay> plays)
    {
        int n = plays.Count;
        if (n == 0 || plays.Any(p => p.Kind == WesternPracticeCardKind.Other || !Enum.IsDefined(p.Kind))) return default;
        var kinds = plays.Select(p => p.Kind).ToArray();
        var a = WesternPracticeCardKind.Attack;
        var s = WesternPracticeCardKind.Skill;
        var p = WesternPracticeCardKind.Power;
        int attacks = kinds.Count(k => k == a), skills = kinds.Count(k => k == s), powers = kinds.Count(k => k == p);
        int names = plays.Select(play => play.CardModelId).Distinct(StringComparer.Ordinal).Count();
        bool manual = plays.All(play => !play.Automatic);
        return (problemId, thinkerId) switch
        {
            ("VIRTUE_AND_HAPPINESS", "PROTAGORAS") when kinds.SequenceEqual([a, s, s, a]) => new(Draw: 1),
            ("VIRTUE_AND_HAPPINESS", "PLATO") when kinds.SequenceEqual([a, s, p, s]) => new(Draw: 1, Block: 4),
            ("VIRTUE_AND_HAPPINESS", "DIOGENES_OF_SINOPE") when n == 1 && manual => new(Block: 5),
            ("VIRTUE_AND_HAPPINESS", "ZENO_OF_CITIUM") when n >= 3 && manual && skills >= 2 && attacks <= 1 => new(Block: 6),
            ("FREEDOM_AND_INSTITUTIONS", "ARISTOTLE") when n == 4 && skills == 2 && attacks == 1 && powers == 1 => new(Block: 7),
            ("FREEDOM_AND_INSTITUTIONS", "NICCOLO_MACHIAVELLI") when n == 3 && attacks > 0 && skills > 0 && kinds[0] != kinds[^1] => new(Energy: 1),
            ("FREEDOM_AND_INSTITUTIONS", "THOMAS_HOBBES") when n == 3 && kinds[0] == p && kinds[1] == kinds[2] && kinds[1] != p => new(Draw: 1, Block: 4),
            ("FREEDOM_AND_INSTITUTIONS", "ZENO_OF_CITIUM") when n == 2 && manual && skills == 1 && attacks == 1 => new(Block: 4),
            ("FREEDOM_AND_INSTITUTIONS", "AUGUSTINE_OF_HIPPO") when n == 3 && kinds[0] == a && kinds[^1] == s => new(Block: 5),
            ("FREEDOM_AND_INSTITUTIONS", "ROUSSEAU") when n == 3 && names == 3 && kinds.Distinct().Count() == 1 => new(Draw: 1, Block: 3),
            ("FREEDOM_AND_INSTITUTIONS", "IMMANUEL_KANT") when manual && kinds.SequenceEqual([s, a, s]) => new(Energy: 1, Block: 2),
            ("FREEDOM_AND_INSTITUTIONS", "JOHANN_GOTTLIEB_FICHTE") when kinds.SequenceEqual([a, s, a]) => new(Draw: 1, Block: 3),
            ("FREEDOM_AND_INSTITUTIONS", "MARY_WOLLSTONECRAFT") when n == 4 && manual && attacks == 2 && skills == 2 => new(Draw: 1, Block: 4),
            ("FREEDOM_AND_INSTITUTIONS", "JOHN_STUART_MILL") when n is >= 3 and <= 5 && names == n && kinds.Distinct().Count() >= 2 => new(Energy: 1),
            ("SELF_AND_OTHER", "JOHN_LOCKE") when n == 3 && plays[0].CardModelId == plays[2].CardModelId && plays[0].CardModelId != plays[1].CardModelId => new(Draw: 1, Block: 3),
            ("SELF_AND_OTHER", "DAVID_HUME") when n == 4 && names == 2 && plays[0].CardModelId == plays[2].CardModelId && plays[1].CardModelId == plays[3].CardModelId => new(Draw: 2),
            ("SELF_AND_OTHER", "IMMANUEL_KANT") when n == 3 && manual && kinds[0] == s && attacks == 1 && skills == 1 && powers == 1 => new(Draw: 2),
            ("SELF_AND_OTHER", "SIMONE_DE_BEAUVOIR") when manual && kinds.SequenceEqual([s, a, s, a]) => new(Energy: 1, Draw: 1),
            ("LANGUAGE_AND_MEANING", "THOMAS_AQUINAS") when n == 4 && kinds[0] == s && attacks == 1 && skills == 2 && powers == 1 => new(Draw: 1, Block: 4),
            ("LANGUAGE_AND_MEANING", "WILLIAM_OF_OCKHAM") when n == 2 && names == 2 && skills > 0 => new(Draw: 1),
            ("LANGUAGE_AND_MEANING", "GOTTLOB_FREGE") when kinds.SequenceEqual([a, s, a, s]) && plays[0].CardModelId == plays[2].CardModelId && plays[1].CardModelId != plays[3].CardModelId => new(Draw: 2, Block: 2),
            ("LANGUAGE_AND_MEANING", "FERDINAND_DE_SAUSSURE") when n == 3 && names == 3 && kinds[0] == kinds[2] && kinds[0] != kinds[1] => new(Draw: 1, Block: 3),
            ("LANGUAGE_AND_MEANING", "CLAUDE_LEVI_STRAUSS") when n == 4 && names == 4 && kinds[0] == kinds[3] && kinds[1] == kinds[2] && kinds[0] != kinds[1] => new(Draw: 1, Block: 4),
            ("LANGUAGE_AND_MEANING", "PROTAGORAS") when n == 3 && attacks + skills == 3 && attacks > 0 && skills > 0 => new(Draw: 1, Block: 2),
            ("LANGUAGE_AND_MEANING", "JACQUES_DERRIDA") when n == 4 && names == 4 && kinds[0] == kinds[1] && kinds[1] == kinds[2] && kinds[2] != kinds[3] => new(Energy: 1, Draw: 1),
            ("HISTORY_AND_POWER", "GEORG_WILHELM_FRIEDRICH_HEGEL") when n == 4 && kinds[0] == kinds[1] && kinds.Distinct().Count() == 3 => new(Draw: 2, Block: 3),
            ("HISTORY_AND_POWER", "KARL_MARX") when n == 4 && manual && attacks == 2 && skills == 2 && kinds[0] == a && kinds[^1] == s => new(Energy: 1, Block: 4),
            _ => default,
        };
    }
}
