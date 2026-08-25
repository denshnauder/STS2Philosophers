namespace STS2MinimalMod;

internal enum KongziMuduoOrder
{
    Undecided,
    Honored,
    Dishonored,
}

internal struct KongziMuduoState
{
    public KongziMuduoOrder Order { get; private set; }

    public int GrantedStrength { get; private set; }

    public void BeginTurn()
    {
        Order = KongziMuduoOrder.Undecided;
        GrantedStrength = 0;
    }

    public void HonorRitual()
    {
        if (Order == KongziMuduoOrder.Undecided)
        {
            Order = KongziMuduoOrder.Honored;
        }
    }

    public bool TryDishonor()
    {
        if (Order != KongziMuduoOrder.Undecided)
        {
            return false;
        }

        Order = KongziMuduoOrder.Dishonored;
        return true;
    }

    public int GetNextSkillStrengthToGrant(int strengthCap)
    {
        if (Order != KongziMuduoOrder.Honored || GrantedStrength >= strengthCap)
        {
            return 0;
        }

        int desiredAmount = GrantedStrength == 0 ? 2 : 1;
        return Math.Min(desiredAmount, strengthCap - GrantedStrength);
    }

    public void RecordGrantedStrength(int amount, int strengthCap)
    {
        if (amount > 0)
        {
            GrantedStrength = Math.Min(strengthCap, GrantedStrength + amount);
        }
    }

    public void EndTurn()
    {
        Order = KongziMuduoOrder.Undecided;
        GrantedStrength = 0;
    }
}
