namespace STS2MinimalMod;

internal readonly record struct PhilosophersGazeRelicOwnership(
    bool HasKongziMuduo,
    bool HasKongziQingYuPei,
    bool HasMengziXiongZhang,
    bool HasXunziShengMo);

internal static class PhilosophersGazeRelicGrantPolicy
{
    public static bool CanGrant(PhilosophersGazeRelicOwnership ownership)
    {
        return !ownership.HasKongziMuduo
            && !ownership.HasKongziQingYuPei
            && !ownership.HasMengziXiongZhang
            && !ownership.HasXunziShengMo;
    }
}
