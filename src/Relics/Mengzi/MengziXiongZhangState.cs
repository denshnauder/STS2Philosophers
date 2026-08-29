namespace STS2Philosophers;

internal struct MengziXiongZhangState
{
    public int LastTriggeredTurn { get; private set; }

    public void BeginCombat()
    {
        LastTriggeredTurn = 0;
    }

    public bool TryTrigger(int turnNumber, int virtue)
    {
        if (turnNumber < 1
            || virtue < turnNumber
            || turnNumber <= LastTriggeredTurn)
        {
            return false;
        }

        LastTriggeredTurn = turnNumber;
        return true;
    }

    public void RestoreLastTriggeredTurn(int turnNumber)
    {
        LastTriggeredTurn = Math.Max(0, turnNumber);
    }

    public void EndCombat()
    {
        LastTriggeredTurn = 0;
    }
}
