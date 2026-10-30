namespace STS2Philosophers;

internal sealed record HumeInductionTriggerContext(
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
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class HumeInductionTriggerPolicy
{
    public static bool ShouldInsert(HumeInductionTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == HumeInductionState.SourceNodeId
            && context.LastFixedAct <= 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
