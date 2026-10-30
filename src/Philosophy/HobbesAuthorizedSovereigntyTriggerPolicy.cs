namespace STS2Philosophers;

internal sealed record HobbesAuthorizedSovereigntyTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool RequiredEdgeCompleted,
    bool CompletionEdgeEligible,
    bool AristotleClosed,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class HobbesAuthorizedSovereigntyTriggerPolicy
{
    public static bool ShouldInsert(HobbesAuthorizedSovereigntyTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        }
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == HobbesAuthorizedSovereigntyState.SourceNodeId
            && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible && context.AristotleClosed;
    }
}
