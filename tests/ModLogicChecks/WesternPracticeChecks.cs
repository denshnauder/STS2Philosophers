using System.Text.Json;
using STS2Philosophers;

internal static class WesternPracticeChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        static WesternPracticeState Save(WesternPracticeState state) =>
            JsonSerializer.Deserialize<WesternPracticeState>(JsonSerializer.Serialize(state))!;
        static void Play(WesternPracticeState state, string sequence, bool automatic = false, bool duplicateModels = false)
        {
            foreach (char letter in sequence)
            {
                string id = $"PLAY_{state.Plays.Count}";
                WesternPracticeCardKind kind = letter switch
                {
                    'A' => WesternPracticeCardKind.Attack,
                    'S' => WesternPracticeCardKind.Skill,
                    'P' => WesternPracticeCardKind.Power,
                    _ => WesternPracticeCardKind.Other,
                };
                Check(state.RecordPlay(state.Turn, id, duplicateModels ? "SAME" : id, kind, automatic), "Each distinct play must count.");
                Check(!state.RecordPlay(state.Turn, id, id, kind), "Repeated callback must not count twice.");
            }
        }

        (string Problem, string Good, string Bad, WesternPracticeReward Reward)[] cases =
        [
            ("BEING_AND_CHANGE", "ASA", "ASS", new(Energy: 1)),
            ("KNOWLEDGE_AND_DOUBT", "ASP", "AS", new(Draw: 1)),
            ("VIRTUE_AND_HAPPINESS", "AASS", "AASSS", new(Block: 6)),
            ("FREEDOM_AND_INSTITUTIONS", "PAS", "PASS", new(Energy: 1, Block: 3)),
            ("SELF_AND_OTHER", "SA", "AS", new(Draw: 1, Block: 3)),
            ("LANGUAGE_AND_MEANING", "PPSS", "PPSA", new(Draw: 1, Block: 2)),
            ("HISTORY_AND_POWER", "AS", "ASPA", new(Energy: 1)),
        ];
        Check(cases.Select(c => c.Problem).ToHashSet().SetEquals(WesternRouteCatalog.Problems.Select(p => p.ProblemId)), "Every approved problem has a tested practice.");
        foreach (var test in cases)
        {
            WesternPracticeState state = new() { ProblemId = test.Problem };
            Check(!state.RecordPlay(0, "P", "A", WesternPracticeCardKind.Attack), "Cannot record before combat turns begin.");
            state.BeginTurn(1);
            Play(state, "ASP");
            state.CloseTurn(1);
            state.BeginTurn(2);
            state.TakeReward(2);
            Play(state, test.Good);
            state = Save(state);
            Check(!state.RecordPlay(1, "OLD", "OLD", WesternPracticeCardKind.Attack), "Stale turns cannot change practice.");
            Check(!state.RecordPlay(2, "PLAY_0", "COPY", WesternPracticeCardKind.Attack), "Save must preserve callback deduplication.");
            Check(state.CloseTurn(2) && !state.CloseTurn(2), "Close is once per turn.");
            Check(!state.RecordPlay(2, "LATE", "LATE", WesternPracticeCardKind.Attack), "Closed turns cannot gain extra practice.");
            Check(state.TakeReward(3) == default, "No early reward before next turn begins.");
            state = Save(state);
            state.BeginTurn(3);
            Check(state.TakeReward(3) == test.Reward, "Qualifying practice must produce its bounded next-turn reward.");
            state = Save(state);
            Check(state.TakeReward(3) == default && !state.BeginTurn(3), "Reload and repeated start cannot pay twice.");
            Play(state, test.Bad);
            int failures = state.BrokenTurns;
            state.CloseTurn(3);
            Check(state.BrokenTurns == failures + 1, "Violation records one unsuccessful practice.");
            state.BeginTurn(4);
            Check(state.TakeReward(4) == default, "Violation earns nothing.");
            state.CloseTurn(4);
            state.BeginTurn(5);
            Check(state.TakeReward(5) == default, "Empty turns cannot farm rewards, including history.");
            int successes = state.SuccessfulTurns;
            state.EndCombat();
            Check(state.PendingReward == default && state.Plays.Count == 0 && state.Turn == 0
                && state.SuccessfulTurns == successes, "Combat cleanup removes buffs but preserves historical practice counts.");
        }

        WesternPracticeState knowledge = new() { ProblemId = "KNOWLEDGE_AND_DOUBT" };
        knowledge.BeginTurn(1);
        Play(knowledge, "ASP", duplicateModels: true);
        knowledge.CloseTurn(1);
        Check(knowledge.PendingReward == default, "Different play identities cannot hide repeated card models.");
        WesternPracticeState self = new() { ProblemId = "SELF_AND_OTHER" };
        self.BeginTurn(1);
        Play(self, "SA", automatic: true);
        self.CloseTurn(1);
        Check(self.PendingReward == default, "Automatic actions cannot satisfy the deliberate-action practice.");
        WesternPracticeState skipped = new() { ProblemId = "BEING_AND_CHANGE" };
        skipped.BeginTurn(1);
        Play(skipped, "ASA");
        skipped.CloseTurn(1);
        skipped.BeginTurn(3);
        Check(skipped.TakeReward(3) == default, "Rewards expire when their next turn is skipped.");
        skipped.EndCombat();
        skipped.BeginTurn(1);
        Check(skipped.TakeReward(1) == default, "A new fight must not inherit a pending reward.");

        // Long combo decks cannot turn these exercises into unbounded resource engines.
        foreach (var test in cases)
        {
            WesternPracticeState state = new() { ProblemId = test.Problem };
            state.BeginTurn(1);
            Play(state, string.Concat(Enumerable.Repeat("AS", 200)));
            state.CloseTurn(1);
            state.BeginTurn(2);
            WesternPracticeReward reward = state.TakeReward(2);
            Check(reward.Energy <= 1 && reward.Draw <= 1 && reward.Block <= 6, "Even 400-card turns must respect reward caps.");
        }
    }
}
