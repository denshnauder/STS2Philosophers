namespace STS2Philosophers;

internal struct LaoziWuWeiShuJianState
{
    private HashSet<object>? _processedCallbackTokens;

    public int CardsPlayedThisTurn { get; private set; }

    public int LastEvaluatedTurn { get; private set; }

    public int PendingRewardTurn { get; private set; }

    public int LastRewardedTurn { get; private set; }

    public void BeginCombat()
    {
        CardsPlayedThisTurn = 0;
        LastEvaluatedTurn = 0;
        PendingRewardTurn = 0;
        LastRewardedTurn = 0;
        _processedCallbackTokens = null;
    }

    public void BeginPlayerTurn()
    {
        CardsPlayedThisTurn = 0;
        _processedCallbackTokens = null;
    }

    public bool RecordCardPlayed(object callbackToken)
    {
        ArgumentNullException.ThrowIfNull(callbackToken);

        _processedCallbackTokens ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!_processedCallbackTokens.Add(callbackToken))
        {
            return false;
        }

        CardsPlayedThisTurn++;
        return true;
    }

    public bool TryQualifyTurn(int turnNumber, int maximumCardsPlayed)
    {
        if (turnNumber < 1 || turnNumber <= LastEvaluatedTurn)
        {
            return false;
        }

        LastEvaluatedTurn = turnNumber;
        if (CardsPlayedThisTurn > maximumCardsPlayed)
        {
            return false;
        }

        PendingRewardTurn = turnNumber + 1;
        return true;
    }

    public bool TryTakePendingReward(int turnNumber)
    {
        if (PendingRewardTurn != turnNumber || turnNumber <= LastRewardedTurn)
        {
            if (PendingRewardTurn > 0 && PendingRewardTurn < turnNumber)
            {
                PendingRewardTurn = 0;
            }

            return false;
        }

        PendingRewardTurn = 0;
        LastRewardedTurn = turnNumber;
        return true;
    }

    public void RestoreCardsPlayedThisTurn(int cardsPlayed)
    {
        CardsPlayedThisTurn = Math.Max(0, cardsPlayed);
        _processedCallbackTokens = null;
    }

    public void RestoreLastEvaluatedTurn(int turnNumber)
    {
        LastEvaluatedTurn = Math.Max(0, turnNumber);
    }

    public void RestorePendingRewardTurn(int turnNumber)
    {
        PendingRewardTurn = Math.Max(0, turnNumber);
    }

    public void RestoreLastRewardedTurn(int turnNumber)
    {
        LastRewardedTurn = Math.Max(0, turnNumber);
    }

    public void EndCombat()
    {
        BeginCombat();
    }
}
