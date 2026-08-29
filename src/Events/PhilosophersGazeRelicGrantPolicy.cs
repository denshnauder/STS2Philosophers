namespace STS2Philosophers;

internal readonly record struct PhilosophersGazeRelicOwnership(
    bool HasKongziMuduo,
    bool HasKongziQingYuPei,
    bool HasMengziXiongZhang,
    bool HasXunziShengMo,
    bool HasMoziMoSeZhuJian = false,
    bool HasMoziShouChengTu = false,
    bool HasLaoziWuWeiShuJian = false,
    bool HasLaoziShuiYu = false);

internal static class PhilosophersGazeRelicGrantPolicy
{
    public static bool CanGrant(PhilosophersGazeRelicOwnership ownership)
    {
        return !ownership.HasKongziMuduo
            && !ownership.HasKongziQingYuPei
            && !ownership.HasMengziXiongZhang
            && !ownership.HasXunziShengMo
            && !ownership.HasMoziMoSeZhuJian
            && !ownership.HasMoziShouChengTu
            && !ownership.HasLaoziWuWeiShuJian
            && !ownership.HasLaoziShuiYu;
    }
}
