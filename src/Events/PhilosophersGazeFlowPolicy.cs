namespace STS2Philosophers;

internal enum PhilosophersGazePage
{
    Initial,
    KongziViewpoints,
    MoziViewpoints,
    LaoziViewpoints,
    ActOneDeclineConfirm,
    Continuation,
    MengziViewpoints,
    XunziViewpoints,
    ActTwoDeclineConfirm,
    KongziMuduo,
    KongziQingYuPei,
    MoziMoSeZhuJian,
    MoziShouChengTu,
    LaoziWuWeiShuJian,
    LaoziShuiYu,
    KongziDecline,
    MoziDecline,
    LaoziDecline,
    Decline,
    MengziXiongZhang,
    XunziShengMo,
    MengziDecline,
    XunziDecline,
    ContinuationDecline,
}

internal enum PhilosophersGazeOption
{
    Kongzi,
    Mozi,
    Laozi,
    Mengzi,
    Xunzi,
    Muduo,
    QingYuPei,
    MoSeZhuJian,
    ShouChengTu,
    WuWeiShuJian,
    ShuiYu,
    XiongZhang,
    ShengMo,
    Decline,
    Confirm,
}

internal enum PhilosophersGazeEffect
{
    None,
    ObtainKongziMuduo,
    ObtainKongziQingYuPei,
    ObtainMoziMoSeZhuJian,
    ObtainMoziShouChengTu,
    ObtainLaoziWuWeiShuJian,
    ObtainLaoziShuiYu,
    ReplaceWithMengziXiongZhang,
    ReplaceWithXunziShengMo,
    RecordContinuationDecline,
}

internal readonly record struct PhilosophersGazeTransition(
    PhilosophersGazePage Destination,
    PhilosophersGazeEffect Effect,
    bool FinishesEvent)
{
    public bool IsNavigation => !FinishesEvent;

    public bool UsesNativeProceed => FinishesEvent;
}

internal static class PhilosophersGazeFlowPolicy
{
    public static bool TryGetTransition(
        PhilosophersGazePage page,
        PhilosophersGazeOption option,
        out PhilosophersGazeTransition transition)
    {
        transition = (page, option) switch
        {
            (PhilosophersGazePage.Initial, PhilosophersGazeOption.Kongzi) => Navigate(PhilosophersGazePage.KongziViewpoints),
            (PhilosophersGazePage.Initial, PhilosophersGazeOption.Mozi) => Navigate(PhilosophersGazePage.MoziViewpoints),
            (PhilosophersGazePage.Initial, PhilosophersGazeOption.Laozi) => Navigate(PhilosophersGazePage.LaoziViewpoints),
            (PhilosophersGazePage.Initial, PhilosophersGazeOption.Decline) => Navigate(PhilosophersGazePage.ActOneDeclineConfirm),
            (PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.Muduo) => Finish(PhilosophersGazePage.KongziMuduo, PhilosophersGazeEffect.ObtainKongziMuduo),
            (PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.QingYuPei) => Finish(PhilosophersGazePage.KongziQingYuPei, PhilosophersGazeEffect.ObtainKongziQingYuPei),
            (PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.Decline) => Finish(PhilosophersGazePage.KongziDecline),
            (PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.MoSeZhuJian) => Finish(PhilosophersGazePage.MoziMoSeZhuJian, PhilosophersGazeEffect.ObtainMoziMoSeZhuJian),
            (PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.ShouChengTu) => Finish(PhilosophersGazePage.MoziShouChengTu, PhilosophersGazeEffect.ObtainMoziShouChengTu),
            (PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.Decline) => Finish(PhilosophersGazePage.MoziDecline),
            (PhilosophersGazePage.LaoziViewpoints, PhilosophersGazeOption.WuWeiShuJian) => Finish(PhilosophersGazePage.LaoziWuWeiShuJian, PhilosophersGazeEffect.ObtainLaoziWuWeiShuJian),
            (PhilosophersGazePage.LaoziViewpoints, PhilosophersGazeOption.ShuiYu) => Finish(PhilosophersGazePage.LaoziShuiYu, PhilosophersGazeEffect.ObtainLaoziShuiYu),
            (PhilosophersGazePage.LaoziViewpoints, PhilosophersGazeOption.Decline) => Finish(PhilosophersGazePage.LaoziDecline),
            (PhilosophersGazePage.ActOneDeclineConfirm, PhilosophersGazeOption.Confirm) => Finish(PhilosophersGazePage.Decline),
            (PhilosophersGazePage.Continuation, PhilosophersGazeOption.Mengzi) => Navigate(PhilosophersGazePage.MengziViewpoints),
            (PhilosophersGazePage.Continuation, PhilosophersGazeOption.Xunzi) => Navigate(PhilosophersGazePage.XunziViewpoints),
            (PhilosophersGazePage.Continuation, PhilosophersGazeOption.Decline) => Navigate(PhilosophersGazePage.ActTwoDeclineConfirm),
            (PhilosophersGazePage.MengziViewpoints, PhilosophersGazeOption.XiongZhang) => Finish(PhilosophersGazePage.MengziXiongZhang, PhilosophersGazeEffect.ReplaceWithMengziXiongZhang),
            (PhilosophersGazePage.MengziViewpoints, PhilosophersGazeOption.Decline) => Finish(PhilosophersGazePage.MengziDecline, PhilosophersGazeEffect.RecordContinuationDecline),
            (PhilosophersGazePage.XunziViewpoints, PhilosophersGazeOption.ShengMo) => Finish(PhilosophersGazePage.XunziShengMo, PhilosophersGazeEffect.ReplaceWithXunziShengMo),
            (PhilosophersGazePage.XunziViewpoints, PhilosophersGazeOption.Decline) => Finish(PhilosophersGazePage.XunziDecline, PhilosophersGazeEffect.RecordContinuationDecline),
            (PhilosophersGazePage.ActTwoDeclineConfirm, PhilosophersGazeOption.Confirm) => Finish(PhilosophersGazePage.ContinuationDecline, PhilosophersGazeEffect.RecordContinuationDecline),
            _ => default,
        };

        return transition != default;
    }

    public static string ToLocalizationKey(this PhilosophersGazePage page)
    {
        return page switch
        {
            PhilosophersGazePage.Initial => "INITIAL",
            PhilosophersGazePage.KongziViewpoints => "KONGZI_VIEWPOINTS",
            PhilosophersGazePage.MoziViewpoints => "MOZI_VIEWPOINTS",
            PhilosophersGazePage.LaoziViewpoints => "LAOZI_VIEWPOINTS",
            PhilosophersGazePage.ActOneDeclineConfirm => "ACT_ONE_DECLINE_CONFIRM",
            PhilosophersGazePage.Continuation => "CONTINUATION",
            PhilosophersGazePage.MengziViewpoints => "MENGZI_VIEWPOINTS",
            PhilosophersGazePage.XunziViewpoints => "XUNZI_VIEWPOINTS",
            PhilosophersGazePage.ActTwoDeclineConfirm => "ACT_TWO_DECLINE_CONFIRM",
            PhilosophersGazePage.KongziMuduo => "KONGZI_MUDUO",
            PhilosophersGazePage.KongziQingYuPei => "KONGZI_QING_YU_PEI",
            PhilosophersGazePage.MoziMoSeZhuJian => "MOZI_MO_SE_ZHU_JIAN",
            PhilosophersGazePage.MoziShouChengTu => "MOZI_SHOU_CHENG_TU",
            PhilosophersGazePage.LaoziWuWeiShuJian => "LAOZI_WU_WEI_SHU_JIAN",
            PhilosophersGazePage.LaoziShuiYu => "LAOZI_SHUI_YU",
            PhilosophersGazePage.KongziDecline => "KONGZI_DECLINE",
            PhilosophersGazePage.MoziDecline => "MOZI_DECLINE",
            PhilosophersGazePage.LaoziDecline => "LAOZI_DECLINE",
            PhilosophersGazePage.Decline => "DECLINE",
            PhilosophersGazePage.MengziXiongZhang => "MENGZI_XIONG_ZHANG",
            PhilosophersGazePage.XunziShengMo => "XUNZI_SHENG_MO",
            PhilosophersGazePage.MengziDecline => "MENGZI_DECLINE",
            PhilosophersGazePage.XunziDecline => "XUNZI_DECLINE",
            PhilosophersGazePage.ContinuationDecline => "CONTINUATION_DECLINE",
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
        };
    }

    public static string ToLocalizationKey(this PhilosophersGazeOption option)
    {
        return option switch
        {
            PhilosophersGazeOption.Kongzi => "KONGZI",
            PhilosophersGazeOption.Mozi => "MOZI",
            PhilosophersGazeOption.Laozi => "LAOZI",
            PhilosophersGazeOption.Mengzi => "MENGZI",
            PhilosophersGazeOption.Xunzi => "XUNZI",
            PhilosophersGazeOption.Muduo => "MUDUO",
            PhilosophersGazeOption.QingYuPei => "QING_YU_PEI",
            PhilosophersGazeOption.MoSeZhuJian => "MO_SE_ZHU_JIAN",
            PhilosophersGazeOption.ShouChengTu => "SHOU_CHENG_TU",
            PhilosophersGazeOption.WuWeiShuJian => "WU_WEI_SHU_JIAN",
            PhilosophersGazeOption.ShuiYu => "SHUI_YU",
            PhilosophersGazeOption.XiongZhang => "XIONG_ZHANG",
            PhilosophersGazeOption.ShengMo => "SHENG_MO",
            PhilosophersGazeOption.Decline => "DECLINE",
            PhilosophersGazeOption.Confirm => "CONFIRM",
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    private static PhilosophersGazeTransition Navigate(PhilosophersGazePage destination)
    {
        return new PhilosophersGazeTransition(destination, PhilosophersGazeEffect.None, FinishesEvent: false);
    }

    private static PhilosophersGazeTransition Finish(
        PhilosophersGazePage destination,
        PhilosophersGazeEffect effect = PhilosophersGazeEffect.None)
    {
        return new PhilosophersGazeTransition(destination, effect, FinishesEvent: true);
    }
}
