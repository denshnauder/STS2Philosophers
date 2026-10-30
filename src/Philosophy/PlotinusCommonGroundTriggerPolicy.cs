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
    private const int MinimumCompletedCombats = 2;

    public static bool ShouldInsert(PlotinusCommonGroundTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= MinimumCompletedCombats
            && context.EpicurusClosed
            && context.CurrentNodeId == PlotinusCommonGroundState.SourceNodeId
            && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible && context.HasRequiredRelic;
    }
}
