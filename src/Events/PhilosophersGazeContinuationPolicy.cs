namespace STS2MinimalMod;

[Flags]
internal enum PhilosophersGazeContinuationOption
{
    None = 0,
    MengziXiongZhang = 1,
    XunziShengMo = 2,
}

internal readonly record struct PhilosophersGazeContinuationInsertionContext(
    bool RunInProgress,
    bool CurrentRoomIsMapRoom,
    int CurrentActIndex,
    bool ModelAvailable,
    bool IsSingleplayer,
    PhilosophersGazeRelicOwnership Ownership,
    bool ContinuationRecorded);

internal static class PhilosophersGazeContinuationPolicy
{
    public static PhilosophersGazeContinuationOption GetAvailableOptions(
        PhilosophersGazeRelicOwnership ownership,
        bool continuationRecorded)
    {
        if (continuationRecorded || ownership.HasMengziXiongZhang || ownership.HasXunziShengMo)
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

        return options;
    }

    public static bool IsContinuationStage(PhilosophersGazeRelicOwnership ownership)
    {
        return ownership.HasKongziMuduo
            || ownership.HasKongziQingYuPei
            || ownership.HasMengziXiongZhang
            || ownership.HasXunziShengMo;
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
                or PhilosophersGazeContinuationOption.XunziShengMo)
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
