namespace STS2Philosophers;

internal sealed record LockeCargoIdentityTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool EdgeEligible,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class LockeCargoIdentityTriggerPolicy
{
    public static bool ShouldInsert(LockeCargoIdentityTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 0 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == LockeCargoIdentityState.SourceNodeId
            && context.LastFixedAct == 1 && context.EdgeEligible;
    }
}
