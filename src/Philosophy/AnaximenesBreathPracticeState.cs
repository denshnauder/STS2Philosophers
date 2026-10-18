namespace STS2Philosophers;

internal struct AnaximenesBreathPracticeState
{
    public int BankedTurn { get; set; }
    public int RedeemedTurn { get; set; }

    public bool TryBank(int turn, decimal remainingEnergy)
    {
        if (turn < 1 || remainingEnergy < 1 || BankedTurn == turn) return false;
        BankedTurn = turn;
        return true;
    }

    public bool TryRedeem(int turn)
    {
        if (turn < 2 || BankedTurn != turn - 1 || RedeemedTurn == turn) return false;
        RedeemedTurn = turn;
        return true;
    }

    public void Reset()
    {
        BankedTurn = 0;
        RedeemedTurn = 0;
    }
}
