namespace STS2MinimalMod;

internal record struct MoziMoSeZhuJianState
{
    public const int BlockAmount = 6;

    public const int XiangLiCap = 2;

    public int XiangLi { get; internal set; }

    public bool CurrentRoundUnharmed { get; internal set; }

    public int LastBoundaryTurn { get; internal set; }

    public int PendingRewardAmount { get; internal set; }

    public int PendingRewardTurn { get; internal set; }

    public int LastRewardedTurn { get; internal set; }

    public int LastBlockGrantedTurn { get; internal set; }

    public bool BeginPlayerTurn(int turnNumber)
    {
        if (turnNumber <= 0 || turnNumber <= LastBoundaryTurn)
        {
            return false;
        }

        if (LastBoundaryTurn > 0)
        {
            XiangLi = CurrentRoundUnharmed
                ? Math.Min(XiangLi + 1, XiangLiCap)
                : 0;
        }

        LastBoundaryTurn = turnNumber;
        CurrentRoundUnharmed = true;
        PendingRewardAmount = XiangLi;
        PendingRewardTurn = turnNumber;
        return true;
    }

    public bool RecordHpChange(decimal delta)
    {
        if (delta >= 0m || LastBoundaryTurn <= 0)
        {
            return false;
        }

        bool changed = CurrentRoundUnharmed || XiangLi > 0;
        CurrentRoundUnharmed = false;
        XiangLi = 0;
        return changed;
    }

    public bool TryTakePendingReward(int turnNumber, out int amount)
    {
        amount = 0;
        if (turnNumber <= 0
            || PendingRewardTurn != turnNumber
            || LastRewardedTurn == turnNumber)
        {
            return false;
        }

        LastRewardedTurn = turnNumber;
        amount = PendingRewardAmount;
        return true;
    }

    public bool TryMarkBlockGranted(int turnNumber)
    {
        if (turnNumber <= 0 || LastBlockGrantedTurn == turnNumber)
        {
            return false;
        }

        LastBlockGrantedTurn = turnNumber;
        return true;
    }

    public void Reset()
    {
        this = default;
    }
}
