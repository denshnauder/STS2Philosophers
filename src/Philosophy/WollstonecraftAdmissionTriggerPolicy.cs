namespace STS2Philosophers;

internal sealed record WollstonecraftAdmissionTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool CompletionEdgeEligible,
    bool RivalEdgeCompleted,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class WollstonecraftAdmissionTriggerPolicy
{
    public static bool ShouldInsert(WollstonecraftAdmissionTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId
            && context.LastFixedAct <= 2 && !context.RivalEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
