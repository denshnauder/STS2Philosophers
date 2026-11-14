namespace STS2Philosophers;

internal sealed record DeleuzeDifferenceTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);

internal static class DeleuzeDifferenceTriggerPolicy
{
    public static bool ShouldInsert(DeleuzeDifferenceTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId
            && context.LastFixedAct == 3 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
