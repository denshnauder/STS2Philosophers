using STS2Philosophers;

internal static class WesternNodePracticeChecks
{
    public static void Run()
    {
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        static WesternPracticePlay[] Plays(string types, string names, bool automatic = false) => types.Select((t, i) =>
            new WesternPracticePlay(i.ToString(), names[i].ToString(), t switch
            { 'A' => WesternPracticeCardKind.Attack, 'S' => WesternPracticeCardKind.Skill,
                'P' => WesternPracticeCardKind.Power, _ => WesternPracticeCardKind.Other }, automatic)).ToArray();
        const string being = "BEING_AND_CHANGE", knowledge = "KNOWLEDGE_AND_DOUBT", virtue = "VIRTUE_AND_HAPPINESS",
            freedom = "FREEDOM_AND_INSTITUTIONS", self = "SELF_AND_OTHER", language = "LANGUAGE_AND_MEANING", history = "HISTORY_AND_POWER";
        (string Problem, string Thinker, string Good, string Names, string Bad, string BadNames, WesternPracticeReward Reward)[] cases =
        [
            (being, "HERACLITUS", "ASA", "abc", "AAS", "abc", new(Energy: 1)),
            (knowledge, "SOCRATES", "ASP", "abc", "ASP", "aba", new(Draw: 1)),
            (virtue, "SOCRATES", "AASS", "abcd", "AAAS", "abcd", new(Block: 6)),
            (freedom, "PLATO", "ASP", "abc", "ASS", "abc", new(Energy: 1, Block: 3)),
            (self, "DESCARTES", "SA", "ab", "AS", "ab", new(Draw: 1, Block: 3)),
            (language, "ARISTOTLE", "AASS", "abcd", "AAAS", "abcd", new(Draw: 1, Block: 2)),
            (history, "ROUSSEAU", "AS", "ab", "ASP", "abc", new(Energy: 1)),
            (being, "PARMENIDES", "AAA", "aaa", "AAA", "aab", new(Block: 6)),
            (being, "ARISTOTLE", "SSA", "abc", "SAS", "abc", new(Energy: 1, Block: 3)),
            (being, "DEMOCRITUS", "AASS", "aabb", "AASS", "aaab", new(Draw: 1, Block: 3)),
            (being, "EMPEDOCLES", "AASP", "abcd", "AASS", "abcd", new(Block: 6)),
            (being, "BARUCH_SPINOZA", "AASS", "abcd", "ASAS", "abcd", new(Energy: 1)),
            (being, "GEORG_WILHELM_FRIEDRICH_HEGEL", "ASAS", "abcd", "SASA", "abcd", new(Draw: 1, Block: 4)),
            (being, "EPICURUS", "AS", "ab", "AS", "aa", new(Block: 4)),
            (being, "GILLES_DELEUZE", "ASPA", "abcd", "ASPA", "abca", new(Energy: 1, Draw: 1)),
            (being, "GOTTFRIED_WILHELM_LEIBNIZ", "ASP", "abc", "ASS", "abc", new(Block: 6)),
            (knowledge, "SEXTUS_EMPIRICUS", "AS", "ab", "AA", "ab", new(Draw: 1)),
            (knowledge, "DESCARTES", "SAA", "abb", "SAA", "abc", new(Draw: 1, Block: 3)),
            (knowledge, "DAVID_HUME", "AAA", "aab", "AAA", "aba", new(Energy: 1)),
            (knowledge, "IMMANUEL_KANT", "ASP", "abc", "APS", "abc", new(Draw: 2)),
            (virtue, "PROTAGORAS", "ASSA", "abcd", "ASAS", "abcd", new(Draw: 1)),
            (virtue, "PLATO", "ASPS", "abcd", "APSS", "abcd", new(Draw: 1, Block: 4)),
            (virtue, "DIOGENES_OF_SINOPE", "S", "a", "SS", "ab", new(Block: 5)),
            (virtue, "ZENO_OF_CITIUM", "SSA", "abc", "SAA", "abc", new(Block: 6)),
            (freedom, "ARISTOTLE", "SSAP", "abcd", "SAAP", "abcd", new(Block: 7)),
            (freedom, "NICCOLO_MACHIAVELLI", "ASS", "abc", "ASA", "abc", new(Energy: 1)),
            (freedom, "THOMAS_HOBBES", "PAA", "abc", "PAS", "abc", new(Draw: 1, Block: 4)),
            (freedom, "ZENO_OF_CITIUM", "AS", "ab", "SS", "ab", new(Block: 4)),
            (freedom, "AUGUSTINE_OF_HIPPO", "APS", "abc", "SPA", "abc", new(Block: 5)),
            (freedom, "ROUSSEAU", "AAA", "abc", "AAA", "aba", new(Draw: 1, Block: 3)),
            (freedom, "IMMANUEL_KANT", "SAS", "abc", "ASS", "abc", new(Energy: 1, Block: 2)),
            (freedom, "JOHANN_GOTTLIEB_FICHTE", "ASA", "abc", "AAS", "abc", new(Draw: 1, Block: 3)),
            (freedom, "MARY_WOLLSTONECRAFT", "AASS", "abcd", "AAAS", "abcd", new(Draw: 1, Block: 4)),
            (freedom, "JOHN_STUART_MILL", "ASPA", "abcd", "ASPA", "abca", new(Energy: 1)),
            (self, "JOHN_LOCKE", "ASA", "aba", "ASA", "abc", new(Draw: 1, Block: 3)),
            (self, "DAVID_HUME", "ASAS", "abab", "ASAS", "abba", new(Draw: 2)),
            (self, "IMMANUEL_KANT", "SAP", "abc", "ASP", "abc", new(Draw: 2)),
            (self, "SIMONE_DE_BEAUVOIR", "SASA", "abcd", "ASAS", "abcd", new(Energy: 1, Draw: 1)),
            (language, "THOMAS_AQUINAS", "SASP", "abcd", "ASSP", "abcd", new(Draw: 1, Block: 4)),
            (language, "WILLIAM_OF_OCKHAM", "AS", "ab", "AS", "aa", new(Draw: 1)),
            (language, "GOTTLOB_FREGE", "ASAS", "abac", "ASAS", "abab", new(Draw: 2, Block: 2)),
            (language, "FERDINAND_DE_SAUSSURE", "ASA", "abc", "AAS", "abc", new(Draw: 1, Block: 3)),
            (language, "CLAUDE_LEVI_STRAUSS", "ASSA", "abcd", "ASAS", "abcd", new(Draw: 1, Block: 4)),
            (language, "PROTAGORAS", "AAS", "abc", "ASP", "abc", new(Draw: 1, Block: 2)),
            (language, "JACQUES_DERRIDA", "AAAS", "abcd", "AAAS", "abca", new(Energy: 1, Draw: 1)),
            (history, "GEORG_WILHELM_FRIEDRICH_HEGEL", "AASP", "abcd", "ASAP", "abcd", new(Draw: 2, Block: 3)),
            (history, "KARL_MARX", "AASS", "abcd", "ASSA", "abcd", new(Energy: 1, Block: 4)),
        ];
        WesternPracticeCardKind[] previous = [WesternPracticeCardKind.Attack, WesternPracticeCardKind.Skill, WesternPracticeCardKind.Power];
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in cases)
        {
            string id = $"{c.Thinker}__{c.Problem}__{(c.Thinker == "KARL_MARX" ? "EARLY" : "CORE")}";
            covered.Add(id);
            Check(WesternNodePracticePolicy.Evaluate(id, Plays(c.Good, c.Names), previous, 3) == c.Reward, $"Node positive: {id}");
            Check(WesternNodePracticePolicy.Evaluate(id, Plays(c.Bad, c.BadNames), previous, 3) == default, $"Node violation: {id}");
            Check(WesternNodePracticePolicy.Evaluate(id, [], previous, 3) == default, $"Empty turn: {id}");
            Check(WesternNodePracticePolicy.Evaluate(id + "_FUTURE", Plays(c.Good, c.Names), previous, 3) == default, "Unimplemented stage must not inherit another stage.");
            // Exercise the real shared turn lifecycle, including a save on either side of the reward window.
            WesternPracticeState state = new();
            Check(state.TryBindNode(id), "Known node binds outside combat.");
            state.BeginTurn(1);
            foreach (var play in Plays("ASP", "xyz")) state.RecordPlay(1, play.PlayId, play.CardModelId, play.Kind);
            state.CloseTurn(1); state.BeginTurn(2); state.TakeReward(2);
            Check(!state.TryBindNode(id), "Rebinding in combat must not reset or farm a practice.");
            foreach (var play in Plays(c.Good, c.Names)) state.RecordPlay(2, play.PlayId, play.CardModelId, play.Kind);
            state.CloseTurn(2);
            state = WesternPracticeStateCodec.RestoreNode(WesternPracticeStateCodec.Encode(state));
            Check(state.NodeId == id && state.PendingReward == c.Reward, $"Closed-turn payload: {id}");
            state.BeginTurn(3);
            state = WesternPracticeStateCodec.RestoreNode(WesternPracticeStateCodec.Encode(state));
            Check(state.TakeReward(3) == c.Reward && state.TakeReward(3) == default, $"Saved node reward delivered once: {id}");
            state = WesternPracticeStateCodec.RestoreNode(WesternPracticeStateCodec.Encode(state));
            Check(state.TakeReward(3) == default, "Saving after claim must not restore a claimed reward.");
            state.EndCombat();
            Check(state.TryBindNode("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE") && state.PreviousKinds.Count == 0
                && state.PendingReward == default && state.SuccessfulTurns == 0, "Changing doctrine clears earlier practice state.");
        }
        Check(covered.Count == 47 && covered.SetEquals(WesternRouteGraph.LoadEmbedded().Nodes.Select(node => node.NodeId)),
            "Every current graph node needs an explicit positive and violation case, with no fabricated node.");
        foreach (var c in cases.Where(c => (c.Problem, c.Thinker) is
            (self, "DESCARTES") or (virtue, "DIOGENES_OF_SINOPE") or (virtue, "ZENO_OF_CITIUM")
            or (freedom, "ZENO_OF_CITIUM") or (freedom, "IMMANUEL_KANT") or (freedom, "MARY_WOLLSTONECRAFT")
            or (self, "IMMANUEL_KANT") or (self, "SIMONE_DE_BEAUVOIR") or (history, "KARL_MARX")))
        {
            string id = $"{c.Thinker}__{c.Problem}__{(c.Thinker == "KARL_MARX" ? "EARLY" : "CORE")}";
            var plays = Plays(c.Good, c.Names); plays[^1] = plays[^1] with { Automatic = true };
            Check(WesternNodePracticePolicy.Evaluate(id, plays, previous, 3) == default, "One automatic play breaks manual-only practice.");
        }
        const string entry = "HERACLITUS__BEING_AND_CHANGE__CORE";
        var invalid = Plays("ASA", "abc"); invalid[1] = invalid[1] with { Kind = (WesternPracticeCardKind)99 };
        Check(WesternNodePracticePolicy.Evaluate(entry, invalid, [], 0) == default, "Unknown type rejected.");
        invalid = Plays("ASA", "abc"); invalid[1] = invalid[1] with { PlayId = "0" };
        Check(WesternNodePracticePolicy.Evaluate(entry, invalid, [], 0) == default, "Duplicate callbacks are not distinct actions.");
        Check(WesternNodePracticePolicy.Evaluate("DESCARTES__VIRTUE_AND_HAPPINESS__CORE", Plays("SAA", "abb"), [], 0) == default,
            "Thinker existence must not grant undeclared problem faces.");
        WesternPracticeState payloadState = new(); payloadState.TryBindNode(entry); payloadState.BeginTurn(1);
        foreach (var play in Plays("ASA", "abc")) payloadState.RecordPlay(1, play.PlayId, play.CardModelId, play.Kind);
        payloadState.CloseTurn(1);
        string payload = WesternPracticeStateCodec.Encode(payloadState);
        foreach (string damaged in new[]
        {
            payload.Replace("\"Energy\":1", "\"Energy\":99", StringComparison.Ordinal),
            payload.Replace("\"Kind\":2", "\"Kind\":1", StringComparison.Ordinal),
            payload.Replace("\"PendingEvidence\":", "\"LostEvidence\":", StringComparison.Ordinal),
        }) Check(WesternPracticeStateCodec.RestoreNode(damaged).PendingReward == default, "Invalid reward or evidence cannot grant a benefit.");
        Check(WesternPracticeStateCodec.Restore(payload, being, "PARMENIDES__BEING_AND_CHANGE__CORE").PendingReward == default,
            "Same-problem node payloads cannot transfer rewards.");
        foreach (string damaged in new[] { "[]", "{\"NodeId\":null}", "{\"NodeId\":\"UNKNOWN\"}", "broken" })
            Check(WesternPracticeStateCodec.RestoreNode(damaged).NodeId == string.Empty, "Bad self-contained carrier payload is inert.");
        Console.WriteLine("Western node practice checks passed: all 47 nodes, wrong stages/problems, violations and manual-play boundaries.");
    }
}
