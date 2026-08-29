namespace STS2Philosophers;

internal static class LaoziShuiYuDamagePolicy
{
    public const decimal ActiveMultiplier = 0.5m;

    public static decimal GetMultiplier(
        bool damageReductionActive,
        bool targetIsOwner,
        bool dealerIsEnemy,
        bool isAttackDamage)
    {
        return damageReductionActive
            && targetIsOwner
            && dealerIsEnemy
            && isAttackDamage
                ? ActiveMultiplier
                : 1m;
    }
}
