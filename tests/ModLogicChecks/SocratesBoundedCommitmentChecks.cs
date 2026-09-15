using STS2Philosophers;

internal static class SocratesBoundedCommitmentChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Ready(SocratesBoundedCommitmentState s, int turn, int? attack = 11, int block = 0,
        string[]? enemies = null, int expectedDraw = 0)
    {
        Check(s.OpenNormalTurn("battle", turn), "Open a distinct normal turn.");
        Check(s.TakeOpeningDraw("battle", turn) == expectedDraw, "Receive exactly the preceding reward.");
        Check(s.TakeOpeningDraw("battle", turn) == 0, "Opening draw must be consumed once.");
        Check(s.FreezeFacts("battle", turn, attack, block, enemies ?? ["A", "B"]), "Freeze valid public facts.");
    }

    private static void Close(SocratesBoundedCommitmentState s, int turn, int block, string[]? defeated = null)
    {
        Check(s.LockActions("battle", turn), "Lock the declaration before acting.");
        Check(s.CloseNormalTurn("battle", turn, block, defeated ?? []), "Close once at the observation point.");
    }

    public static void Run()
    {
        SocratesBoundedCommitmentState s = new("battle");
        Ready(s, 1);
        Check(s.GuardTarget == 6 && s.InvitationAvailable, "Odd incoming damage rounds up.");
        Check(!s.LockActions("battle", 1), "Resolve the initial invitation before action.");
        Check(s.Select("battle", 1, SocratesCommitmentMode.Guard), "Initial choice is not a revision.");
        Check(!s.Revised && !s.Select("battle", 1, SocratesCommitmentMode.Clear), "Cannot change a declaration twice.");
        Close(s, 1, 6);
        Check(s.Observation == SocratesCommitmentObservation.Met, "Guard can meet a bounded metric.");
        Check(!s.CloseNormalTurn("battle", 1, 6, []), "Duplicate close does not add reward.");
        Check(s.OpenNormalTurn("battle", 3), "A skipped engine turn does not preclude the next normal turn.");
        Check(!s.FreezeFacts("battle", 3, 8, 0, ["A"]), "Claim opening draw before freezing the decision window.");
        Check(s.TakeOpeningDraw("other", 3) == 0 && s.TakeOpeningDraw("battle", 1) == 0, "Stale identities cannot claim.");
        Check(s.TakeOpeningDraw("battle", 3) == 1, "Prior reward is available once.");
        Check(s.FreezeFacts("battle", 3, 8, 0, ["A"]), "Prepare the receiving normal turn.");
        Check(s.Select("battle", 3, SocratesCommitmentMode.Clear) && s.Revised, "First revision changes the continuing criterion.");
        Close(s, 3, 0, ["A"]);
        Check(s.Observation == SocratesCommitmentObservation.Met, "Revision does not rewrite the observed fact.");
        Ready(s, 4);
        Check(s.Select("battle", 4, SocratesCommitmentMode.Guard) && s.Revised, "A later second revision remains available.");
        Close(s, 4, 6);
        Ready(s, 5);
        Close(s, 5, 6);
        Ready(s, 6, expectedDraw: 1);
        Check(s.EndCombat("battle") && !s.EndCombat("battle"), "Combat termination is idempotent.");
        Check(!s.OpenNormalTurn("battle", 7) && s.TakeOpeningDraw("battle", 6) == 0, "Ended combat cannot reopen.");

        s = new("battle");
        Ready(s, 1, attack: 0, enemies: []);
        Check(!s.InvitationAvailable && !s.Select("battle", 1, SocratesCommitmentMode.Guard), "Do not force an invitation without a public attacker.");
        Close(s, 1, 0);
        Ready(s, 2, block: 20);
        Check(!s.GuardOpportunity && s.Select("battle", 2, SocratesCommitmentMode.Guard), "Selection is independent of current reward eligibility.");
        Close(s, 2, 20);
        Check(s.Observation == SocratesCommitmentObservation.NoCondition, "Existing block is no new opportunity.");
        Ready(s, 3, attack: 0, enemies: []);
        Check(s.Select("battle", 3, SocratesCommitmentMode.Clear), "A quiet normal turn permits revision.");
        Close(s, 3, 0);
        Check(s.Observation == SocratesCommitmentObservation.NoCondition, "No opportunity is not failure.");
        Ready(s, 4);
        Close(s, 4, 0, ["late", "replacement"]);
        Check(s.Observation == SocratesCommitmentObservation.Unmet, "Late or replacement identities do not join the frozen scope.");
        Ready(s, 5);
        Close(s, 5, 0, ["B", "B", "A"]);
        Ready(s, 6, expectedDraw: 1);
        Check(s.LockActions("battle", 6), "Retaining a criterion does not need another decision.");
        Check(!s.Select("battle", 6, SocratesCommitmentMode.Guard), "No post-action revision.");
        Check(s.CloseNormalTurn("battle", 6, 0, null, false), "Incomplete target evidence can close safely.");
        Check(s.Observation == SocratesCommitmentObservation.Incomplete, "Unknown is distinct from unmet.");

        s = new("battle");
        Ready(s, 1, attack: null);
        Check(s.Select("battle", 1, SocratesCommitmentMode.Guard), "Unknown damage does not ban a continuing criterion.");
        Close(s, 1, int.MaxValue);
        Check(s.Observation == SocratesCommitmentObservation.Incomplete, "Unknown damage never becomes a guessed threshold.");
        Ready(s, 2, attack: int.MaxValue);
        Check(s.GuardTarget == 1073741824, "Half rounding must not overflow.");
        Close(s, 2, int.MaxValue);
        Check(s.EndCombat("battle"), "Victory/death expires pending reward.");
        Check(s.TakeOpeningDraw("battle", 2) == 0, "No cross-combat claim.");

        s = new("battle");
        Ready(s, 1);
        Check(s.DeclineBattle("battle", 1), "Decline the battle practice explicitly.");
        Close(s, 1, 20, ["A"]);
        Ready(s, 2);
        Check(!s.InvitationAvailable && !s.Select("battle", 2, SocratesCommitmentMode.Guard), "Declining must not reopen the invitation.");

        s = new("battle");
        Check(!s.OpenNormalTurn("other", 1) && !s.OpenNormalTurn("battle", 0), "Reject invalid combat and turn identities.");
        Check(s.OpenNormalTurn("battle", 1), "Open validation case.");
        Check(!s.FreezeFacts("battle", 1, -1, 0, ["A"]) &&
            !s.FreezeFacts("battle", 1, 4, 0, []) &&
            !s.FreezeFacts("battle", 1, 4, 0, ["A", "A"]), "Invalid facts do not advance the phase.");
        string[] input = ["A"];
        Check(s.FreezeFacts("battle", 1, 4, 0, input), "Accept valid facts after rejected input.");
        input[0] = "changed";
        Check(s.FrozenAttackers.SequenceEqual(["A"]), "Copy caller-owned target data.");
        Check(!s.OpenNormalTurn("battle", 1) && !s.FreezeFacts("battle", 1, 2, 0, ["new"]), "Repeated preparation cannot reset a frozen turn.");
        Check(!s.Select("battle", 1, (SocratesCommitmentMode)99), "Reject invalid modes.");
        Check(s.Select("battle", 1, SocratesCommitmentMode.Guard), "Choose a valid mode.");
        Close(s, 1, 2);
        Check(s.OpenNormalTurn("battle", 2) && s.OpenNormalTurn("battle", 3), "Passing an unclaimed normal turn expires its reward.");
        Check(s.TakeOpeningDraw("battle", 3) == 0, "An old reward cannot be collected later.");
        Console.WriteLine("Socrates C3 in-memory protocol checks passed; no save or engine integration claim.");
    }
}
