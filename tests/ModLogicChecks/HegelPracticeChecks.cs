using STS2Philosophers;

internal static class HegelPracticeChecks
{
    public static void Run()
    {
        HegelCombatPracticeState state = HegelCombatPracticeState.CreateDefault();
        Check(state.IsCleanDefault(), "A fresh Hegel practice is the clean default.");
        Check(state.BeginCombat("2:4:98"), "A combat key opens the Hegel practice.");
        Check(!state.BeginCombat("2:4:98"), "Re-entering the same combat does not reset the window.");

        Check(state.ObserveManual("2:4:98", HegelPracticeCardKind.Attack) == HegelPracticeReward.None
            && state.ObserveManual("2:4:98", HegelPracticeCardKind.Skill) == HegelPracticeReward.None
            && state.ObserveManual("2:4:98", HegelPracticeCardKind.Attack) == HegelPracticeReward.None,
            "The first three cards of the sequence pay nothing.");
        Check(state.ObserveManual("2:4:98", HegelPracticeCardKind.Skill) == HegelPracticeReward.DrawAndBlock,
            "Attack, Skill, Attack, Skill pays draw 1 and block 4 once.");
        Check(state.ObserveManual("2:4:98", HegelPracticeCardKind.Attack) == HegelPracticeReward.None
            && state.ObserveManual("2:4:98", HegelPracticeCardKind.Skill) == HegelPracticeReward.None
            && state.ObserveManual("2:4:98", HegelPracticeCardKind.Attack) == HegelPracticeReward.None
            && state.ObserveManual("2:4:98", HegelPracticeCardKind.Skill) == HegelPracticeReward.None,
            "The reward is paid once per combat even if the sequence repeats.");

        HegelCombatPracticeState broken = HegelCombatPracticeState.CreateDefault();
        broken.BeginCombat("2:4:99");
        foreach (HegelPracticeCardKind kind in new[]
                 {
                     HegelPracticeCardKind.Attack, HegelPracticeCardKind.Skill,
                     HegelPracticeCardKind.Attack, HegelPracticeCardKind.Other,
                     HegelPracticeCardKind.Skill, HegelPracticeCardKind.Attack,
                     HegelPracticeCardKind.Skill,
                 })
            broken.ObserveManual("2:4:99", kind);
        Check(!broken.Claimed && broken.ObserveManual("2:4:99", HegelPracticeCardKind.Attack)
            == HegelPracticeReward.None, "A non-Attack/Skill manual card breaks the four-card run.");

        HegelCombatPracticeState automatic = HegelCombatPracticeState.CreateDefault();
        automatic.BeginCombat("2:4:100");
        foreach (HegelPracticeCardKind kind in new[]
                 {
                     HegelPracticeCardKind.Attack, HegelPracticeCardKind.Skill,
                     HegelPracticeCardKind.Attack, HegelPracticeCardKind.Skill,
                 })
            Check(automatic.ObserveManual("2:4:100", kind) is HegelPracticeReward.None or HegelPracticeReward.DrawAndBlock,
                "Only manual kinds reach the state; automatic plays are filtered by the relic.");
        Check(automatic.Claimed, "The manual sequence claims the reward.");

        HegelCombatPracticeState wrongCombat = HegelCombatPracticeState.CreateDefault();
        wrongCombat.BeginCombat("2:4:101");
        Check(wrongCombat.ObserveManual("2:4:102", HegelPracticeCardKind.Attack) == HegelPracticeReward.None,
            "A play from another combat never advances the window.");

        HegelCombatPracticeState next = HegelCombatPracticeState.CreateDefault();
        next.BeginCombat("2:4:103");
        next.ObserveManual("2:4:103", HegelPracticeCardKind.Attack);
        Check(next.BeginCombat("2:4:104") && next.Window.Count == 0 && !next.Claimed,
            "A genuinely new combat clears the window and the claim.");

        string encoded = state.Encode();
        Check(HegelCombatPracticeState.TryRestore(encoded, out HegelCombatPracticeState restored)
            && restored.Claimed && restored.CombatKey == "2:4:98" && restored.Window.Count == 4
            && restored.Encode() == encoded, "The claimed state round-trips through its saved payload.");

        foreach (string bad in new[]
                 {
                     "", "null", "[]", "{\"Version\":2,\"CombatKey\":\"a\",\"Window\":[],\"Claimed\":false}",
                     "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[],\"Claimed\":true}",
                     "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[\"Attack\",\"Skill\",\"Attack\",\"Skill\",\"Attack\"],\"Claimed\":false}",
                     "{\"Version\":1,\"CombatKey\":\"\",\"Window\":[\"Attack\"],\"Claimed\":false}",
                     "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[\"Attack\",\"Skill\",\"Attack\",\"Skill\"]}",
                     "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[\"Attack\",\"Skill\",\"Attack\",\"Skill\"],\"Claimed\":false,\"Extra\":1}",
                     "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[\"Attack\",\"Skill\",\"Attack\",\"Bogus\"],\"Claimed\":false}",
                 })
            Check(!HegelCombatPracticeState.TryRestore(bad, out _), $"Malformed Hegel payload is rejected: {bad}");

        Check(HegelCombatPracticeState.TryRestore(
                "{\"Version\":1,\"CombatKey\":\"a\",\"Window\":[\"Attack\",\"Skill\"],\"Claimed\":false}",
                out HegelCombatPracticeState partial) && partial.Window.Count == 2 && !partial.Claimed,
            "A mid-sequence payload restores without claiming.");
        Console.WriteLine("Hegel practice checks passed: Attack/Skill/Attack/Skill pays draw 1 and block 4 once per combat, a broken run restarts, new combats reset, and strict payloads reject malformed or cross-combat receipts.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
