namespace STS2Philosophers;

internal struct LaoziShuiYuState
{
    public bool DamageReductionActive { get; private set; }

    public int LastEvaluatedTurn { get; private set; }

    public void BeginCombat()
    {
        DamageReductionActive = false;
        LastEvaluatedTurn = 0;
    }

    public void BeginPlayerTurn()
    {
        DamageReductionActive = false;
    }

    public bool EvaluateTurnEnd(int turnNumber, int remainingEnergy)
    {
        if (turnNumber < 1 || turnNumber <= LastEvaluatedTurn)
        {
            return false;
        }

        LastEvaluatedTurn = turnNumber;
        DamageReductionActive = remainingEnergy > 0;
        return DamageReductionActive;
    }

    public void RestoreDamageReductionActive(bool active)
    {
        DamageReductionActive = active;
    }

    public void RestoreLastEvaluatedTurn(int turnNumber)
    {
        LastEvaluatedTurn = Math.Max(0, turnNumber);
    }

    public void EndCombat()
    {
        BeginCombat();
    }
}
