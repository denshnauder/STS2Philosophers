namespace STS2Philosophers;

internal record struct MoziShouChengTuState
{
    public const int BlockAmount = 6;

    public bool HasActiveDefenseWindow { get; internal set; }

    public bool EnemyDamageTaken { get; internal set; }

    public int LastBoundaryTurn { get; internal set; }

    public int LastIntentSampleTurn { get; internal set; }

    public bool HasPendingReward { get; internal set; }

    public int PendingRewardTurn { get; internal set; }

    public int LastRewardedTurn { get; internal set; }

    public bool BeginPlayerTurn(int turnNumber)
    {
        if (turnNumber <= 0 || turnNumber <= LastBoundaryTurn)
        {
            return false;
        }

        HasPendingReward = HasActiveDefenseWindow && !EnemyDamageTaken;
        PendingRewardTurn = turnNumber;
        LastBoundaryTurn = turnNumber;
        HasActiveDefenseWindow = false;
        EnemyDamageTaken = false;
        return true;
    }

    public bool SampleEnemyIntents(int turnNumber, bool anyLivingEnemyIntendsToAttack)
    {
        if (turnNumber <= 0
            || turnNumber != LastBoundaryTurn
            || LastIntentSampleTurn == turnNumber)
        {
            return false;
        }

        LastIntentSampleTurn = turnNumber;
        HasActiveDefenseWindow = anyLivingEnemyIntendsToAttack;
        EnemyDamageTaken = false;
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

    public bool TryTakePendingReward(int turnNumber, out bool shouldGrantBlock)
    {
        shouldGrantBlock = false;
        if (turnNumber <= 0
            || PendingRewardTurn != turnNumber
            || LastRewardedTurn == turnNumber)
        {
            return false;
        }

        LastRewardedTurn = turnNumber;
        shouldGrantBlock = HasPendingReward;
        HasPendingReward = false;
        return true;
    }

    public void Reset()
    {
        this = default;
    }
}
