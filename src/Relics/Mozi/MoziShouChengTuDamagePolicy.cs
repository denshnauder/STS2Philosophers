namespace STS2MinimalMod;

internal static class MoziShouChengTuDamagePolicy
{
    public static bool IsEnemyHpLossToOwner(
        bool resultReceiverIsOwner,
        bool hookTargetIsOwner,
        bool dealerIsEnemy,
        int unblockedDamage)
    {
        return resultReceiverIsOwner
            && hookTargetIsOwner
            && dealerIsEnemy
            && unblockedDamage > 0;
    }
}
