using STS2Philosophers;

internal static class CrossActCombatHistoryChecks
{
    internal static void Run()
    {
        SameActCombatAfterPredecessor();
        EarlierActPredecessorWithLaterActCombat();
        EarlierActPredecessorWithSameActCombat();
        CombatBeforePredecessorDoesNotCount();
        MissingPredecessorDoesNotCount();
        FutureActPredecessorDoesNotCount();
        CombatInEarlierActDoesNotCount();
        LastPredecessorOccurrenceWins();
    }

    private static CombatHistoryPoint Predecessor() => new(IsPredecessor: true, IsCombat: false);
    private static CombatHistoryPoint Combat() => new(IsPredecessor: false, IsCombat: true);
    private static CombatHistoryPoint Plain() => new(IsPredecessor: false, IsCombat: false);

    private static void SameActCombatAfterPredecessor()
    {
        Check(CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Predecessor(), Combat() }], 0),
            "A fight after the predecessor in the same act counts.");
    }

    private static void EarlierActPredecessorWithLaterActCombat()
    {
        Check(CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Predecessor(), Plain() }, new[] { Combat() }], 1),
            "A second-act predecessor is found from a third-act node when a later fight exists.");
    }

    private static void EarlierActPredecessorWithSameActCombat()
    {
        Check(CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Predecessor(), Combat() }, new[] { Plain() }], 1),
            "A fight after the predecessor inside its own act still counts across acts.");
    }

    private static void CombatBeforePredecessorDoesNotCount()
    {
        Check(!CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Combat(), Predecessor() }], 0),
            "A fight before the predecessor does not satisfy the prerequisite.");
    }

    private static void MissingPredecessorDoesNotCount()
    {
        Check(!CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Combat() }, new[] { Combat() }], 1),
            "A missing predecessor cannot be proven by fights alone.");
    }

    private static void FutureActPredecessorDoesNotCount()
    {
        Check(!CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Plain() }, new[] { Predecessor(), Combat() }], 0),
            "A predecessor recorded after the current act is ignored.");
    }

    private static void CombatInEarlierActDoesNotCount()
    {
        Check(!CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Combat() }, new[] { Predecessor() }], 1),
            "A fight in an earlier act is not after a later predecessor.");
    }

    private static void LastPredecessorOccurrenceWins()
    {
        Check(!CrossActCombatHistory.HasCombatAfterPredecessor(
                [new[] { Predecessor(), Combat(), Predecessor() }], 0),
            "Only the last predecessor occurrence decides whether a later fight exists.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
