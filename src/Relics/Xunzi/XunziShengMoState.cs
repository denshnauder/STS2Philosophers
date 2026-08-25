namespace STS2MinimalMod;

internal enum XunziShengMoCardKind
{
    Skill,
    Attack,
    Other,
}

internal struct XunziShengMoState
{
    private HashSet<object>? _processedCallbackTokens;

    public int Progress { get; private set; }

    public bool HasTriggeredThisTurn { get; private set; }

    public void BeginCombat()
    {
        ResetTurn();
    }

    public void BeginTurn()
    {
        ResetTurn();
    }

    public bool RecordCard(XunziShengMoCardKind cardKind, object callbackToken)
    {
        ArgumentNullException.ThrowIfNull(callbackToken);

        _processedCallbackTokens ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!_processedCallbackTokens.Add(callbackToken) || HasTriggeredThisTurn)
        {
            return false;
        }

        switch (Progress)
        {
            case 0:
                Progress = cardKind == XunziShengMoCardKind.Skill ? 1 : 0;
                break;
            case 1:
                Progress = cardKind switch
                {
                    XunziShengMoCardKind.Skill => 1,
                    XunziShengMoCardKind.Attack => 2,
                    _ => 0,
                };
                break;
            case 2:
                if (cardKind == XunziShengMoCardKind.Skill)
                {
                    HasTriggeredThisTurn = true;
                    Progress = 0;
                    return true;
                }

                Progress = 0;
                break;
            default:
                Progress = 0;
                break;
        }

        return false;
    }

    public void RestoreProgress(int progress)
    {
        Progress = HasTriggeredThisTurn ? 0 : Math.Clamp(progress, 0, 2);
        _processedCallbackTokens = null;
    }

    public void RestoreTriggeredThisTurn(bool hasTriggeredThisTurn)
    {
        HasTriggeredThisTurn = hasTriggeredThisTurn;
        if (hasTriggeredThisTurn)
        {
            Progress = 0;
        }

        _processedCallbackTokens = null;
    }

    public void EndTurn()
    {
        ResetTurn();
    }

    public void EndCombat()
    {
        ResetTurn();
    }

    private void ResetTurn()
    {
        Progress = 0;
        HasTriggeredThisTurn = false;
        _processedCallbackTokens = null;
    }
}
