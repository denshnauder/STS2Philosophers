using System.Text.Json;
using STS2Philosophers;

internal static class PlotinusCombatPracticeChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static PlotinusCombatPracticeState NewTurn()
    {
        PlotinusCombatPracticeState state = new();
        state.BeginCombat();
        Check(state.BeginTurn(1), "The first turn begins once.");
        return state;
    }

    public static void Run()
    {
        PlotinusCombatPracticeState valid = NewTurn();
        Check(valid.RecordPlay(1, "SKILL_A", WesternPracticeCardKind.Skill, false)
            && valid.RecordPlay(1, "ATTACK", WesternPracticeCardKind.Attack, false),
            "The two opening manual cards are recorded.");
        Check(PlotinusCombatPracticeState.TryRestore(valid.Encode(), out valid)
            && valid.RecordPlay(1, "SKILL_B", WesternPracticeCardKind.Skill, false)
            && valid.CloseTurn(1) == 5 && valid.CloseTurn(1) == 0,
            "A saved exact three-card sequence grants five Block once.");
        Check(PlotinusCombatPracticeState.TryRestore(valid.Encode(), out PlotinusCombatPracticeState closed)
            && closed.CloseTurn(1) == 0 && !closed.RecordPlay(1, "SKILL_C", WesternPracticeCardKind.Skill, false),
            "Reload after the turn cannot pay again or append a card.");
        Check(closed.BeginTurn(2), "The next turn starts independently.");

        PlotinusCombatPracticeState sameSkill = NewTurn();
        sameSkill.RecordPlay(1, "SKILL_A", WesternPracticeCardKind.Skill, false);
        sameSkill.RecordPlay(1, "ATTACK", WesternPracticeCardKind.Attack, false);
        sameSkill.RecordPlay(1, "SKILL_A", WesternPracticeCardKind.Skill, false);
        Check(sameSkill.CloseTurn(1) == 0, "Repeating one skill model is not two distinct local responses.");

        PlotinusCombatPracticeState automatic = NewTurn();
        automatic.RecordPlay(1, "SKILL_A", WesternPracticeCardKind.Skill, false);
        Check(!automatic.RecordPlay(1, "ATTACK", WesternPracticeCardKind.Attack, true)
            && !automatic.RecordPlay(1, "SKILL_B", WesternPracticeCardKind.Skill, false)
            && automatic.CloseTurn(1) == 0, "An automatic play invalidates this turn's practice.");

        PlotinusCombatPracticeState extra = NewTurn();
        extra.RecordPlay(1, "SKILL_A", WesternPracticeCardKind.Skill, false);
        extra.RecordPlay(1, "ATTACK", WesternPracticeCardKind.Attack, false);
        extra.RecordPlay(1, "SKILL_B", WesternPracticeCardKind.Skill, false);
        Check(!extra.RecordPlay(1, "ATTACK_2", WesternPracticeCardKind.Attack, false)
            && extra.CloseTurn(1) == 0, "A fourth card prevents the exact-sequence reward.");
        Check(PlotinusCombatPracticeState.TryRestore(extra.Encode(), out PlotinusCombatPracticeState invalidTurn)
            && invalidTurn.BeginTurn(2) && !invalidTurn.InvalidTurn,
            "Save/load retains a failed turn but resets it only at the next real turn.");

        foreach (string bad in new[] { "broken", "{\"Version\":99}", "{\"Turn\":-1}",
                     "{\"Plays\":[{\"PlayId\":\"x\",\"CardModelId\":\"\"}]}" })
            Check(!PlotinusCombatPracticeState.TryRestore(bad, out _), "Malformed practice payload is inert.");
        Check(PlotinusCombatPracticeState.TryRestore("", out PlotinusCombatPracticeState legacy)
            && legacy.Turn == 0, "Old relic saves without this field do not invent a past reward.");

        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument text = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/relics.json"));
            Check(text.RootElement.TryGetProperty("SOCRATES_BALANCE_WEIGHT.plotinus.title", out _)
                && text.RootElement.TryGetProperty("SOCRATES_BALANCE_WEIGHT.plotinus.description", out _),
                "The route-specific relic tooltip must be bilingual.");
        }
        Console.WriteLine("Plotinus combat practice logic checks passed: exact manual sequence, Block decision, saved replay, invalid turns and bilingual tooltip.");
    }
}
