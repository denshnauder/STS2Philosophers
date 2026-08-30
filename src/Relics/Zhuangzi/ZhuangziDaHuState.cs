namespace STS2Philosophers;

internal struct ZhuangziDaHuState
{
    private HashSet<object>? _processedCallbackTokens;

    public int CardsPlayedThisTurn { get; private set; }

    public int LastStartedTurn { get; private set; }

    public int LastEvaluatedTurn { get; private set; }

    public int PendingRewardTurn { get; private set; }

    public int DiscountedPlaysRemaining { get; private set; }

    public void BeginCombat()
    {
        CardsPlayedThisTurn = 0;
        LastStartedTurn = 0;
        LastEvaluatedTurn = 0;
        PendingRewardTurn = 0;
        DiscountedPlaysRemaining = 0;
        _processedCallbackTokens = null;
    }

    public bool BeginPlayerTurn(int turnNumber, int discountedPlayCount)
    {
        if (turnNumber <= 0 || turnNumber <= LastStartedTurn)
        {
            return false;
        }

        LastStartedTurn = turnNumber;
        CardsPlayedThisTurn = 0;
        DiscountedPlaysRemaining = PendingRewardTurn == turnNumber
            ? Math.Max(0, discountedPlayCount)
            : 0;
        if (PendingRewardTurn <= turnNumber)
        {
            PendingRewardTurn = 0;
        }

        _processedCallbackTokens = null;
        return true;
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
        if (DiscountedPlaysRemaining <= 0)
        {
            return false;
        }

        DiscountedPlaysRemaining--;
        return true;
    }

    public bool CanDiscountNextPlay => DiscountedPlaysRemaining > 0;

    public bool TryQualifyTurn(int turnNumber, int maximumCardsPlayed)
    {
        if (turnNumber < 1 || turnNumber <= LastEvaluatedTurn)
        {
            return false;
        }

        LastEvaluatedTurn = turnNumber;
        DiscountedPlaysRemaining = 0;
        if (CardsPlayedThisTurn > maximumCardsPlayed)
        {
            PendingRewardTurn = 0;
            return false;
        }

        PendingRewardTurn = turnNumber + 1;
        return true;
    }

    public void RestoreCardsPlayedThisTurn(int cardsPlayed)
    {
        CardsPlayedThisTurn = Math.Max(0, cardsPlayed);
        _processedCallbackTokens = null;
    }

    public void RestoreLastStartedTurn(int turnNumber)
    {
        LastStartedTurn = Math.Max(0, turnNumber);
    }

    public void RestoreLastEvaluatedTurn(int turnNumber)
    {
        LastEvaluatedTurn = Math.Max(0, turnNumber);
    }

    public void RestorePendingRewardTurn(int turnNumber)
    {
        PendingRewardTurn = Math.Max(0, turnNumber);
    }

    public void RestoreDiscountedPlaysRemaining(int count)
    {
        DiscountedPlaysRemaining = Math.Max(0, count);
        _processedCallbackTokens = null;
    }

    public void EndCombat()
    {
        BeginCombat();
    }
}
