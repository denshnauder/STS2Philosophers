namespace STS2Philosophers;

internal sealed record ProtagorasSharedDeliberationTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool QuestionResolved,
    bool EdgeCompleted,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class ProtagorasSharedDeliberationTriggerPolicy
{
    public static bool ShouldInsert(ProtagorasSharedDeliberationTriggerContext context)
    {
        if (!context.RunInProgress
            || !context.AtSafeMapBoundary
            || !context.IsSingleplayer
            || context.CurrentActIndex != 0
            || !context.ModelAvailable)
        {
            return false;
        }

        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        }

        return context.CompletedCombatCount >= 1
            && context.CurrentNodeId == ProtagorasSharedDeliberationState.SourceNodeId
            && context.LastFixedAct == 1
            && !context.QuestionResolved
            && !context.EdgeCompleted;
    }
}
