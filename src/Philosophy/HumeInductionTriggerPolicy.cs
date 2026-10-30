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
    bool ModelAvailable,
    bool OnDefaultRoute = false);

internal static class HumeInductionTriggerPolicy
{
    public static bool ShouldInsert(HumeInductionTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex is not (1 or 2) || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        if (!context.SourceIsCompletedNonBossCombat || context.CompletedCombatCount < 1
            || context.CurrentNodeId != HumeInductionState.SourceNodeId
            || !context.RequiredEdgeCompleted || !context.CompletionEdgeEligible) return false;
        // Act two is the question branch; act three only belongs to the route that
        // names Hume as its third act.
        return context.CurrentActIndex == 1
            || context.CurrentActIndex == 2 && context.OnDefaultRoute;
    }
}
