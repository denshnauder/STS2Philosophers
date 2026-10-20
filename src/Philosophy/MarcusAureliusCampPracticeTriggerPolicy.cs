namespace STS2Philosophers;

internal sealed record MarcusAureliusCampPracticeTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, bool HasClosedZenoRoute, bool HasClosedSenecaReview,
    bool HasClosedEpictetusPractice, bool HasPendingZenoOperation, bool HasSavedInstance,
    bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class MarcusAureliusCampPracticeTriggerPolicy
{
    public static bool ShouldInsert(MarcusAureliusCampPracticeTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.SourceIsCompletedNonBossCombat
            || !context.HasClosedZenoRoute || !context.HasClosedSenecaReview
            || !context.HasClosedEpictetusPractice || context.HasPendingZenoOperation
            || !context.ModelAvailable) return false;
        return context.HasSavedInstance
            ? context.SavedInstanceUsable && !context.SavedInstanceClosed
            : true;
    }
}
