namespace STS2Philosophers;

internal record struct HuishiLiWuChouState
{
    public const int SharedBlock = 8;
    public const int RewardDamage = 8;

    public int LastTurnStarted { get; internal set; }

    public int LastTriggeredTurn { get; internal set; }

    public bool BeginPlayerTurn(int turnNumber)
    {
        if (turnNumber <= 0 || turnNumber <= LastTurnStarted)
        {
            return false;
        }

        LastTurnStarted = turnNumber;
        return true;
    }

    public bool TryRewardBlockBreak(
        int turnNumber,
        bool targetIsEnemy,
        bool breakerIsPlayer)
    {
        if (turnNumber <= 0
            || turnNumber != LastTurnStarted
            || LastTriggeredTurn == turnNumber
            || !targetIsEnemy
            || !breakerIsPlayer)
        {
            return false;
        }

        LastTriggeredTurn = turnNumber;
        return true;
    }

    public void Reset()
    {
        this = default;
    }
}
