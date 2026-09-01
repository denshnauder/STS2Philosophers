namespace STS2Philosophers;

internal interface IPhilosophersGazeContinuationCandidateSource
{
    PhilosophersGazeContinuationOption GetCandidates(
        PhilosophersGazeRelicOwnership ownership);
}

internal sealed class LegacyRelicContinuationCandidateSource
    : IPhilosophersGazeContinuationCandidateSource
{
    public static LegacyRelicContinuationCandidateSource Instance { get; } = new();

    private LegacyRelicContinuationCandidateSource()
    {
    }

    public PhilosophersGazeContinuationOption GetCandidates(
        PhilosophersGazeRelicOwnership ownership)
    {
        bool hasConfucianRoot = ownership.HasKongziQingYuPei
            || ownership.HasKongziMuduo;
        bool hasMohistRoot = ownership.HasMoziMoSeZhuJian
            || ownership.HasMoziShouChengTu;
        bool hasDaoistRoot = ownership.HasLaoziWuWeiShuJian
            || ownership.HasLaoziShuiYu;
        int rootFamilyCount = Convert.ToInt32(hasConfucianRoot)
            + Convert.ToInt32(hasMohistRoot)
            + Convert.ToInt32(hasDaoistRoot);
        if (rootFamilyCount != 1)
        {
            return PhilosophersGazeContinuationOption.None;
        }

        PhilosophersGazeContinuationOption options = PhilosophersGazeContinuationOption.None;
        if (ownership.HasKongziQingYuPei)
        {
            options |= PhilosophersGazeContinuationOption.MengziXiongZhang;
        }

        if (ownership.HasKongziMuduo)
        {
            options |= PhilosophersGazeContinuationOption.XunziShengMo;
        }

        if (ownership.HasMoziShouChengTu && !ownership.HasMoziMoSeZhuJian)
        {
            options |= PhilosophersGazeContinuationOption.QinGuliShouChengXie;
        }

        if (ownership.HasMoziMoSeZhuJian && !ownership.HasMoziShouChengTu)
        {
            options |= PhilosophersGazeContinuationOption.HuishiLiWuChou;
        }

        if (ownership.HasLaoziWuWeiShuJian && !ownership.HasLaoziShuiYu)
        {
            options |= PhilosophersGazeContinuationOption.ZhuangziDaHu;
        }

        if (ownership.HasLaoziShuiYu && !ownership.HasLaoziWuWeiShuJian)
        {
            options |= PhilosophersGazeContinuationOption.YangzhuQuanShengBi;
        }

        return options;
    }
}
