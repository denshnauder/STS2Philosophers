namespace STS2Philosophers;

internal sealed record FichteSelfPositingTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);

internal static class FichteSelfPositingTriggerPolicy
{
    public static bool ShouldInsert(FichteSelfPositingTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex is not (1 or 2) || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedRouteConsistent;
        if (context.CurrentActIndex == 1)
            // Default autonomy line: Kant's subject/autonomy room already took
            // the act-two fixed slot, and this act's question is still open.
            return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
                && context.CurrentNodeId == FichteSelfPositingState.ActTwoSourceNodeId
                && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
                && context.CompletionEdgeEligible;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == FichteSelfPositingState.SourceNodeId
            && context.LastFixedAct == 3 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
