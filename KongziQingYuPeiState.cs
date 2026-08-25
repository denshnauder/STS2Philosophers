namespace STS2MinimalMod;

internal enum KongziQingYuPeiBattleOutcome
{
    Available,
    VirtuousConduct,
    LostOpportunity,
}

internal enum KongziQingYuPeiTurnResolution
{
    None,
    VirtuousConduct,
}

internal struct KongziQingYuPeiState
{
    public KongziQingYuPeiBattleOutcome Outcome { get; private set; }

    public bool HasLockedReward => Outcome == KongziQingYuPeiBattleOutcome.VirtuousConduct;

    public bool IsResolved => Outcome != KongziQingYuPeiBattleOutcome.Available;

    public bool IsOpportunityTurn { get; private set; }

    public bool HadAttackOpportunity { get; private set; }

    public void BeginCombat()
    {
        Outcome = KongziQingYuPeiBattleOutcome.Available;
        ResetTurn();
    }

    public void BeginTurn(bool allLivingEnemiesAreNonAttacking)
    {
        ResetTurn();
        IsOpportunityTurn = Outcome == KongziQingYuPeiBattleOutcome.Available
            && allLivingEnemiesAreNonAttacking;
    }

    public bool ObserveAttackOpportunity(bool hasPlayableAttack)
    {
        if (IsOpportunityTurn
            && Outcome == KongziQingYuPeiBattleOutcome.Available
            && hasPlayableAttack
            && !HadAttackOpportunity)
        {
            HadAttackOpportunity = true;
            return true;
        }

        return false;
    }

    public bool CancelOpportunityIfEnemiesAttack(bool allLivingEnemiesAreNonAttacking)
    {
        if (allLivingEnemiesAreNonAttacking || Outcome != KongziQingYuPeiBattleOutcome.Available)
        {
            return false;
        }

        bool canceledOpportunity = IsOpportunityTurn || HadAttackOpportunity;
        ResetTurn();
        return canceledOpportunity;
    }

    public bool RecordAttackPlayed()
    {
        if (!IsOpportunityTurn || IsResolved)
        {
            return false;
        }

        // Reaching the native card-play hook proves that this Attack was playable
        // with the target selected by the player, even if an earlier observation
        // did not see it in hand.
        HadAttackOpportunity = true;
        Outcome = KongziQingYuPeiBattleOutcome.LostOpportunity;
        return true;
    }

    public KongziQingYuPeiTurnResolution EndTurn()
    {
        KongziQingYuPeiTurnResolution resolution = KongziQingYuPeiTurnResolution.None;
        if (IsOpportunityTurn
            && Outcome == KongziQingYuPeiBattleOutcome.Available
            && HadAttackOpportunity)
        {
            Outcome = KongziQingYuPeiBattleOutcome.VirtuousConduct;
            resolution = KongziQingYuPeiTurnResolution.VirtuousConduct;
        }

        ResetTurn();
        return resolution;
    }

    public void EndCombat()
    {
        ResetTurn();
    }

    private void ResetTurn()
    {
        IsOpportunityTurn = false;
        HadAttackOpportunity = false;
    }
}
