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
    HuishiLiWuChou = 32,
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
        return GetAvailableOptions(
            ownership,
            continuationRecorded,
            LegacyRelicContinuationCandidateSource.Instance);
    }

    internal static PhilosophersGazeContinuationOption GetAvailableOptions(
        PhilosophersGazeRelicOwnership ownership,
        bool continuationRecorded,
        IPhilosophersGazeContinuationCandidateSource candidateSource)
    {
        if (continuationRecorded
            || ownership.HasMengziXiongZhang
            || ownership.HasXunziShengMo
            || ownership.HasQinGuliShouChengXie
            || ownership.HasZhuangziDaHu
            || ownership.HasYangzhuQuanShengBi
            || ownership.HasHuishiLiWuChou)
        {
            return PhilosophersGazeContinuationOption.None;
        }

        return candidateSource.GetCandidates(ownership);
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
                or PhilosophersGazeContinuationOption.YangzhuQuanShengBi
                or PhilosophersGazeContinuationOption.HuishiLiWuChou)
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
