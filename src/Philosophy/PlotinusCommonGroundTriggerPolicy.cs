namespace STS2Philosophers;

internal sealed record PlotinusCommonGroundTriggerContext(
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
    bool HasRequiredRelic,
    bool EpicurusClosed,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class PlotinusCommonGroundTriggerPolicy
{
    public static bool ShouldInsert(PlotinusCommonGroundTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 2
            && context.EpicurusClosed
            && context.CurrentNodeId == PlotinusCommonGroundState.SourceNodeId
            && context.LastFixedAct == 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible && context.HasRequiredRelic;
    }
}
