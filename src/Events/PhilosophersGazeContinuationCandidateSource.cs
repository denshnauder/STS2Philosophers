namespace STS2Philosophers;

internal interface IPhilosophersGazeContinuationCandidateSource
{
    PhilosophersGazeContinuationOption GetCandidates(
        PhilosophersGazeRelicOwnership ownership);
}

internal sealed class RegisteredRelicContinuationCandidateSource
    : IPhilosophersGazeContinuationCandidateSource
{
    public static RegisteredRelicContinuationCandidateSource Instance { get; } = new();

    private RegisteredRelicContinuationCandidateSource()
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

        List<string> currentRelicIds = [];
        AddIfHeld(currentRelicIds, ownership.HasKongziQingYuPei, RouteRelicIds.KongziQingYuPei);
        AddIfHeld(currentRelicIds, ownership.HasKongziMuduo, RouteRelicIds.KongziMuduo);
        AddIfHeld(currentRelicIds, ownership.HasMoziShouChengTu, RouteRelicIds.MoziShouChengTu);
        AddIfHeld(currentRelicIds, ownership.HasMoziMoSeZhuJian, RouteRelicIds.MoziMoSeZhuJian);
        AddIfHeld(currentRelicIds, ownership.HasLaoziWuWeiShuJian, RouteRelicIds.LaoziWuWeiShuJian);
        AddIfHeld(currentRelicIds, ownership.HasLaoziShuiYu, RouteRelicIds.LaoziShuiYu);

        PhilosophersGazeContinuationOption options = PhilosophersGazeContinuationOption.None;
        foreach (string candidateId in LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
                     LaterActRelicEligibilityCatalog.ActTwoIndex,
                     currentRelicIds))
        {
            options |= candidateId switch
            {
                LaterActCandidateIds.Mengzi => PhilosophersGazeContinuationOption.MengziXiongZhang,
                LaterActCandidateIds.Xunzi => PhilosophersGazeContinuationOption.XunziShengMo,
                LaterActCandidateIds.QinGuli => PhilosophersGazeContinuationOption.QinGuliShouChengXie,
                LaterActCandidateIds.Huishi => PhilosophersGazeContinuationOption.HuishiLiWuChou,
                LaterActCandidateIds.Zhuangzi => PhilosophersGazeContinuationOption.ZhuangziDaHu,
                LaterActCandidateIds.Yangzhu => PhilosophersGazeContinuationOption.YangzhuQuanShengBi,
                _ => PhilosophersGazeContinuationOption.None,
            };
        }

        return options;
    }

    private static void AddIfHeld(List<string> ids, bool held, string relicId)
    {
        if (held)
        {
            ids.Add(relicId);
        }
    }
}
