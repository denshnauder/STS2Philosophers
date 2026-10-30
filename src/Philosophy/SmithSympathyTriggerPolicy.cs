namespace STS2Philosophers;

internal sealed record SmithSympathyTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool CompletionEdgeEligible, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool SavedRouteConsistent, bool ModelAvailable);

internal static class SmithSympathyTriggerPolicy
{
    public static bool ShouldInsert(SmithSympathyTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 0 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == SmithSympathyState.SourceNodeId
            && context.LastFixedAct == 1 && context.CompletionEdgeEligible;
    }
}
