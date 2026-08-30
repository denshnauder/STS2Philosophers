namespace STS2Philosophers;

internal struct YangzhuQuanShengBiState
{
    public int PreservedEnergy { get; private set; }

    public bool DamageReductionActive { get; private set; }

    public int LastEvaluatedTurn { get; private set; }

    public int PendingReturnTurn { get; private set; }

    public int LastReturnedTurn { get; private set; }

    public void BeginCombat()
    {
        PreservedEnergy = 0;
        DamageReductionActive = false;
        LastEvaluatedTurn = 0;
        PendingReturnTurn = 0;
        LastReturnedTurn = 0;
    }

    public bool EvaluateTurnEnd(
        int turnNumber,
        int remainingEnergy,
        int maximumPreservedEnergy)
    {
        if (turnNumber < 1 || turnNumber <= LastEvaluatedTurn)
        {
            return false;
        }

        LastEvaluatedTurn = turnNumber;
        PreservedEnergy = Math.Min(
            Math.Max(0, maximumPreservedEnergy),
            Math.Max(0, remainingEnergy));
        DamageReductionActive = PreservedEnergy > 0;
        PendingReturnTurn = DamageReductionActive ? turnNumber + 1 : 0;
        return DamageReductionActive;
    }

    public bool TryTakePreservedEnergy(int turnNumber, out int energy)
    {
        energy = 0;
        if (turnNumber <= 0
            || PendingReturnTurn != turnNumber
            || turnNumber <= LastReturnedTurn)
        {
            if (PendingReturnTurn > 0 && PendingReturnTurn < turnNumber)
            {
                PreservedEnergy = 0;
                DamageReductionActive = false;
                PendingReturnTurn = 0;
            }

            return false;
        }

        energy = PreservedEnergy;
        PreservedEnergy = 0;
        DamageReductionActive = false;
        PendingReturnTurn = 0;
        LastReturnedTurn = turnNumber;
        return energy > 0;
    }

    public void RestorePreservedEnergy(int energy)
    {
        PreservedEnergy = Math.Max(0, energy);
    }

    public void RestoreDamageReductionActive(bool active)
    {
        DamageReductionActive = active;
    }

    public void RestoreLastEvaluatedTurn(int turnNumber)
    {
        LastEvaluatedTurn = Math.Max(0, turnNumber);
    }

    public void RestorePendingReturnTurn(int turnNumber)
    {
        PendingReturnTurn = Math.Max(0, turnNumber);
    }

    public void RestoreLastReturnedTurn(int turnNumber)
    {
        LastReturnedTurn = Math.Max(0, turnNumber);
    }

    public void EndCombat()
    {
        BeginCombat();
    }
}
