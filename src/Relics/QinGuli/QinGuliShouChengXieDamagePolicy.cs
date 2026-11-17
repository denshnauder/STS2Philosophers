namespace STS2Philosophers;

internal static class QinGuliShouChengXieDamagePolicy
{
    public static bool IsOwnerHpLoss(
        bool resultReceiverIsOwner,
        bool hookTargetIsOwner,
        int unblockedDamage)
    {
        // The canvas says only "未受伤害" for the defense window. That is wider
        // than the old enemy-direct-damage rule: any unblocked HP loss breaks it.
        return resultReceiverIsOwner
            && hookTargetIsOwner
            && unblockedDamage > 0;
    }
}
