using STS2Philosophers;

internal static class WesternKnowledgeChecks
{
    public static void Run()
    {
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        static WesternPracticeCardKind Kind(char c) => c switch
        { 'A' => WesternPracticeCardKind.Attack, 'S' => WesternPracticeCardKind.Skill,
            'P' => WesternPracticeCardKind.Power, _ => WesternPracticeCardKind.Other };
        static WesternPracticePlay[] Plays(string types, string names) => types.Select((t, i) =>
            new WesternPracticePlay(i.ToString(), names[i].ToString(), Kind(t), false)).ToArray();
        static WesternPracticeReward Eval(string who, string types, string names, string previous = "") =>
            WesternKnowledgePracticePolicy.Evaluate(who, Plays(types, names), previous.Select(Kind).ToArray());
        Check(Eval("SEXTUS_EMPIRICUS", "AS", "ab") == new WesternPracticeReward(Draw: 1), "Sextus different types.");
        Check(Eval("SEXTUS_EMPIRICUS", "AA", "ab") == default && Eval("SEXTUS_EMPIRICUS", "ASP", "abc") == default,
            "Sextus limits both type difference and count.");
        Check(Eval("DESCARTES", "SAA", "abb") == new WesternPracticeReward(Draw: 1, Block: 3), "Descartes method.");
        Check(Eval("DESCARTES", "SAA", "abc") == default && Eval("DESCARTES", "ASA", "bab") == default,
            "Descartes requires exact order and matching attacks.");
        Check(Eval("DAVID_HUME", "AAA", "aab") == new WesternPracticeReward(Energy: 1), "Hume repetition and difference.");
        Check(Eval("DAVID_HUME", "AAA", "aaa") == default && Eval("DAVID_HUME", "AAA", "aba") == default,
            "Hume requires first pair followed by difference.");
        foreach (string sequence in new[] { "ASP", "APS", "SAP", "SPA", "PAS", "PSA" })
        {
            Check(Eval("IMMANUEL_KANT", sequence, "abc", sequence) == new WesternPracticeReward(Draw: 2), "Kant all orders.");
            Check(Eval("IMMANUEL_KANT", sequence, "abc", new string(sequence.Reverse().ToArray())) == default,
                "Same counts cannot stand in for the previous order.");
        }
        Check(Eval("IMMANUEL_KANT", "ASP", "abc") == default && Eval("IMMANUEL_KANT", "AAA", "abc", "AAA") == default,
            "Kant needs real prior facts and all three types.");
        Check(Eval("UNKNOWN", "ASP", "abc", "ASP") == default, "Unknown thinker has no fallback reward.");

        const string problem = "KNOWLEDGE_AND_DOUBT";
        WesternPracticeState state = new() { ProblemId = problem };
        state.BeginTurn(1);
        foreach (var play in Plays("ASP", "abc")) state.RecordPlay(1, play.PlayId, play.CardModelId, play.Kind);
        state.CloseTurn(1);
        state = WesternPracticeStateCodec.Restore(WesternPracticeStateCodec.Encode(state), problem);
        state.BeginTurn(2);
        Check(state.PreviousKinds.SequenceEqual("ASP".Select(Kind)), "Closed-turn save must retain actual order.");
        foreach (var play in Plays("ASP", "xyz")) state.RecordPlay(2, play.PlayId, play.CardModelId, play.Kind);
        state = WesternPracticeStateCodec.Restore(WesternPracticeStateCodec.Encode(state), problem);
        Check(WesternKnowledgePracticePolicy.Evaluate("IMMANUEL_KANT", state.Plays, state.PreviousKinds)
            == new WesternPracticeReward(Draw: 2), "Mid-turn save must preserve both sequences.");
        state.BeginTurn(3);
        Check(state.PreviousKinds.Count == 0, "An unclosed turn is not evidence.");
        state.RecordPlay(3, "a", "a", Kind('A')); state.CloseTurn(3); state.BeginTurn(5);
        Check(state.PreviousKinds.Count == 0 && state.PreviousCards == 0, "Skipped turns must clear prior facts.");
        state.RecordPlay(5, "a", "a", Kind('A')); state.CloseTurn(5); state.BeginTurn(6); state.EndCombat();
        Check(state.PreviousKinds.Count == 0 && state.PreviousCards == 0, "Combat boundaries must clear prior facts.");
        string oldSave = "{\"ProblemId\":\"KNOWLEDGE_AND_DOUBT\",\"Turn\":2,\"PreviousCards\":3}";
        var old = WesternPracticeStateCodec.Restore(oldSave, problem);
        Check(old.Turn == 2 && old.PreviousCards == 3 && old.PreviousKinds.Count == 0,
            "Old saves keep entry history without inventing sequence.");
        foreach (string sequence in new[] { "null", "[99]", "[1,2]" })
        {
            string bad = oldSave[..^1] + ",\"PreviousKinds\":" + sequence + "}";
            Check(WesternPracticeStateCodec.Restore(bad, problem).Turn == 0, "Malformed sequence must be rejected.");
        }
        Console.WriteLine("Western knowledge checks passed: four exercises, sequence persistence, old saves and interrupted turns.");
    }
}
