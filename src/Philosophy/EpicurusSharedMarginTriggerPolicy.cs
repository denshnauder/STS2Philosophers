namespace STS2Philosophers;

internal sealed record EpicurusSharedMarginTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    bool RequiredEdgeCompleted,
    bool HasRequiredRelic,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class EpicurusSharedMarginTriggerPolicy
{
    public static bool ShouldInsert(EpicurusSharedMarginTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable)
        {
            return false;
        }

        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        }

        return context.CompletedCombatCount >= 1
            && context.CurrentNodeId == EpicurusSharedMarginState.RequiredNodeId
            && context.RequiredEdgeCompleted
            && context.HasRequiredRelic;
    }
}
