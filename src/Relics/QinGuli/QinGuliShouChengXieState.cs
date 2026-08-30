namespace STS2Philosophers;

internal record struct QinGuliShouChengXieState
{
    public const int BlockPerAttacker = 4;
    public const int MaximumBlock = 12;

    public bool HasActiveDefenseWindow { get; internal set; }

    public bool EnemyDamageTaken { get; internal set; }

    public int LastBoundaryTurn { get; internal set; }

    public int LastIntentSampleTurn { get; internal set; }

    public int LastRewardedTurn { get; internal set; }

    public bool BeginPlayerTurn(int turnNumber, out bool shouldApplyWeak)
    {
        shouldApplyWeak = false;
        if (turnNumber <= 0 || turnNumber <= LastBoundaryTurn)
        {
            return false;
        }

        shouldApplyWeak = HasActiveDefenseWindow && !EnemyDamageTaken;
        if (shouldApplyWeak)
        {
            LastRewardedTurn = turnNumber;
        }

        LastBoundaryTurn = turnNumber;
        HasActiveDefenseWindow = false;
        EnemyDamageTaken = false;
        return true;
    }

    public bool SampleEnemyIntents(
        int turnNumber,
        int attackingEnemyCount,
        out int blockAmount)
    {
        blockAmount = 0;
        if (turnNumber <= 0
            || turnNumber != LastBoundaryTurn
            || LastIntentSampleTurn == turnNumber)
        {
            return false;
        }

        LastIntentSampleTurn = turnNumber;
        HasActiveDefenseWindow = attackingEnemyCount > 0;
        EnemyDamageTaken = false;
        blockAmount = Math.Min(
            MaximumBlock,
            Math.Max(0, attackingEnemyCount) * BlockPerAttacker);
        return true;
    }

    public bool RecordEnemyHpLoss(int unblockedDamage)
    {
        if (!HasActiveDefenseWindow
            || EnemyDamageTaken
            || unblockedDamage <= 0)
        {
            return false;
        }

        EnemyDamageTaken = true;
        return true;
    }

    public void Reset()
    {
        this = default;
    }
}
