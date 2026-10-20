namespace STS2Philosophers;

internal sealed record EpictetusGuidePracticeTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool HasClosedZenoRoute, bool HasClosedSenecaReview, bool HasPendingZenoOperation,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class EpictetusGuidePracticeTriggerPolicy
{
    public static bool ShouldInsert(EpictetusGuidePracticeTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.HasClosedZenoRoute
            || !context.HasClosedSenecaReview || context.HasPendingZenoOperation
            || !context.ModelAvailable) return false;
        return context.HasSavedInstance
            ? context.SavedInstanceUsable && !context.SavedInstanceClosed
            : true;
    }
}
