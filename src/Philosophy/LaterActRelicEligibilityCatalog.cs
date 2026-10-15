namespace STS2Philosophers;

internal static class LaterActCandidateIds
{
    public const string Mengzi = "MENGZI";
    public const string Xunzi = "XUNZI";
    public const string QinGuli = "QIN_GULI";
    public const string Huishi = "HUI_SHI";
    public const string Zhuangzi = "ZHUANGZI";
    public const string Yangzhu = "YANG_ZHU";
    public const string Plato = "PLATO";
    public const string Descartes = "DESCARTES";
    public const string Epicurus = "EPICURUS";
    public const string Diogenes = "DIOGENES_OF_SINOPE";
    public const string Zeno = "ZENO_OF_CITIUM";
    public const string HanFei = "HAN_FEI";
}

internal static class RouteRelicIds
{
    public const string KongziMuduo = "KONGZI_MUDUO";
    public const string KongziQingYuPei = "KONGZI_QING_YU_PEI";
    public const string MoziMoSeZhuJian = "MOZI_MO_SE_ZHU_JIAN";
    public const string MoziShouChengTu = "MOZI_SHOU_CHENG_TU";
    public const string LaoziWuWeiShuJian = "LAOZI_WU_WEI_SHU_JIAN";
    public const string LaoziShuiYu = "LAOZI_SHUI_YU";
    public const string SocratesQuestionCup = "SOCRATES_QUESTION_CUP";
    public const string SocratesBalanceWeight = "SOCRATES_BALANCE_WEIGHT";
    public const string XunziShengMo = "XUNZI_SHENG_MO";
}

internal sealed record LaterActRelicEligibilityRegistration(
    string CandidateId,
    int CurrentActIndex,
    IReadOnlyList<string> AcceptedRelicIds);

internal static class LaterActRelicEligibilityCatalog
{
    public const int ActTwoIndex = 1;
    public const int ActThreeIndex = 2;

    private static readonly LaterActRelicEligibilityRegistration[] Entries =
    [
        Register(LaterActCandidateIds.Mengzi, ActTwoIndex, RouteRelicIds.KongziQingYuPei),
        Register(LaterActCandidateIds.Xunzi, ActTwoIndex, RouteRelicIds.KongziMuduo),
        Register(LaterActCandidateIds.QinGuli, ActTwoIndex, RouteRelicIds.MoziShouChengTu),
        Register(LaterActCandidateIds.Huishi, ActTwoIndex, RouteRelicIds.MoziMoSeZhuJian),
        Register(LaterActCandidateIds.Zhuangzi, ActTwoIndex, RouteRelicIds.LaoziWuWeiShuJian),
        Register(LaterActCandidateIds.Yangzhu, ActTwoIndex, RouteRelicIds.LaoziShuiYu),
        Register(LaterActCandidateIds.Plato, ActTwoIndex, RouteRelicIds.SocratesBalanceWeight),
        Register(LaterActCandidateIds.Descartes, ActTwoIndex, RouteRelicIds.SocratesQuestionCup),
        Register(LaterActCandidateIds.Epicurus, ActThreeIndex, RouteRelicIds.SocratesBalanceWeight),
        Register(LaterActCandidateIds.Diogenes, ActThreeIndex, RouteRelicIds.SocratesBalanceWeight),
        Register(LaterActCandidateIds.Zeno, ActThreeIndex, RouteRelicIds.SocratesBalanceWeight),
        Register(LaterActCandidateIds.HanFei, ActThreeIndex, RouteRelicIds.XunziShengMo),
    ];

    public static IReadOnlyList<LaterActRelicEligibilityRegistration> Registrations => Entries;

    public static IReadOnlyList<string> GetEligibleCandidateIds(
        int currentActIndex,
        IEnumerable<string>? currentRelicIds)
    {
        if (currentRelicIds is null)
        {
            return [];
        }

        HashSet<string> heldRelics = currentRelicIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (heldRelics.Count == 0)
        {
            return [];
        }

        return Entries
            .Where(entry => entry.CurrentActIndex == currentActIndex
                && entry.AcceptedRelicIds.Any(heldRelics.Contains))
            .Select(entry => entry.CandidateId)
            .ToArray();
    }

    public static bool IsEligible(
        string candidateId,
        int currentActIndex,
        IEnumerable<string>? currentRelicIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        return GetEligibleCandidateIds(currentActIndex, currentRelicIds)
            .Contains(candidateId, StringComparer.Ordinal);
    }

    private static LaterActRelicEligibilityRegistration Register(
        string candidateId,
        int currentActIndex,
        params string[] acceptedRelicIds) =>
        new(candidateId, currentActIndex, acceptedRelicIds);
}
