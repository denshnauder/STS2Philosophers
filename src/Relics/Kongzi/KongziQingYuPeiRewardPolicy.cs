namespace STS2Philosophers;

internal enum KongziQingYuPeiBonusCardRarity
{
    Uncommon,
    Rare,
}

internal readonly record struct KongziQingYuPeiRewardRarityDecision(
    KongziQingYuPeiBonusCardRarity? Rarity,
    bool FellBackFromRare);

internal static class KongziQingYuPeiRewardPolicy
{
    public static KongziQingYuPeiRewardRarityDecision RollAndResolveRarity(
        Func<bool> nativeRollIsRare,
        bool hasUncommonCandidate,
        bool hasRareCandidate)
    {
        bool rolledRare = nativeRollIsRare();
        if (rolledRare)
        {
            if (hasRareCandidate)
            {
                return new KongziQingYuPeiRewardRarityDecision(KongziQingYuPeiBonusCardRarity.Rare, false);
            }

            return hasUncommonCandidate
                ? new KongziQingYuPeiRewardRarityDecision(KongziQingYuPeiBonusCardRarity.Uncommon, true)
                : new KongziQingYuPeiRewardRarityDecision(null, true);
        }

        return hasUncommonCandidate
            ? new KongziQingYuPeiRewardRarityDecision(KongziQingYuPeiBonusCardRarity.Uncommon, false)
            : new KongziQingYuPeiRewardRarityDecision(null, false);
    }
}
