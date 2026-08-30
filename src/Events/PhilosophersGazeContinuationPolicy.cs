namespace STS2Philosophers;

[Flags]
internal enum PhilosophersGazeContinuationOption
{
    None = 0,
    MengziXiongZhang = 1,
    XunziShengMo = 2,
    QinGuliShouChengXie = 4,
    ZhuangziDaHu = 8,
    YangzhuQuanShengBi = 16,
}

internal readonly record struct PhilosophersGazeContinuationInsertionContext(
    bool RunInProgress,
    bool CurrentRoomIsMapRoom,
    int CurrentActIndex,
    bool ModelAvailable,
    bool IsSingleplayer,
    PhilosophersGazeRelicOwnership Ownership,
    bool ContinuationRecorded);

internal readonly record struct PhilosophersGazeContinuationEntryPlan(
    bool CloseMapScreen,
    bool FadeToBlack);

internal static class PhilosophersGazeContinuationPolicy
{
    public static PhilosophersGazeContinuationEntryPlan CreateEntryPlan(
        bool mapRoomEntryCompleted)
    {
        return mapRoomEntryCompleted
            ? new PhilosophersGazeContinuationEntryPlan(
                CloseMapScreen: true,
                FadeToBlack: false)
            : default;
    }

    public static PhilosophersGazeContinuationOption GetAvailableOptions(
        PhilosophersGazeRelicOwnership ownership,
        bool continuationRecorded)
    {
        if (continuationRecorded
            || ownership.HasMengziXiongZhang
            || ownership.HasXunziShengMo
            || ownership.HasQinGuliShouChengXie
            || ownership.HasZhuangziDaHu
            || ownership.HasYangzhuQuanShengBi)
        {
            return PhilosophersGazeContinuationOption.None;
        }

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

    public static bool CanGrant(
        PhilosophersGazeContinuationOption choice,
        PhilosophersGazeRelicOwnership ownership,
        bool continuationRecorded)
    {
        PhilosophersGazeContinuationOption availableOptions = GetAvailableOptions(
            ownership,
            continuationRecorded);
        return (choice is PhilosophersGazeContinuationOption.MengziXiongZhang
                or PhilosophersGazeContinuationOption.XunziShengMo
                or PhilosophersGazeContinuationOption.QinGuliShouChengXie
                or PhilosophersGazeContinuationOption.ZhuangziDaHu
                or PhilosophersGazeContinuationOption.YangzhuQuanShengBi)
            && (availableOptions & choice) == choice;
    }

    public static bool ShouldInsert(PhilosophersGazeContinuationInsertionContext context)
    {
        return context.RunInProgress
            && context.CurrentRoomIsMapRoom
            && context.CurrentActIndex == 1
            && context.ModelAvailable
            && context.IsSingleplayer
            && GetAvailableOptions(context.Ownership, context.ContinuationRecorded)
                != PhilosophersGazeContinuationOption.None;
    }
}
