namespace STS2Philosophers;

internal sealed record SenecaDailyReviewTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool HasClosedZenoRoute, bool HasClosedDiogenesEntry, bool HasPendingZenoOperation,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class SenecaDailyReviewTriggerPolicy
{
    public static bool ShouldInsert(SenecaDailyReviewTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.HasClosedZenoRoute
            || !context.HasClosedDiogenesEntry || context.HasPendingZenoOperation
            || !context.ModelAvailable) return false;
        return context.HasSavedInstance
            ? context.SavedInstanceUsable && !context.SavedInstanceClosed
            : true;
    }
}
