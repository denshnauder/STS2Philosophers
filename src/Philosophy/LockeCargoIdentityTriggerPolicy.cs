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
    bool ModelAvailable,
    bool OnDefaultRoute = false);

internal static class LockeCargoIdentityTriggerPolicy
{
    public static bool ShouldInsert(LockeCargoIdentityTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex is not (0 or 1) || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        if (!context.SourceIsCompletedNonBossCombat || context.CompletedCombatCount < 1
            || context.CurrentNodeId != LockeCargoIdentityState.SourceNodeId
            || !context.EdgeEligible) return false;
        // The act-one question opens Locke for any run; the act-two handover only
        // belongs to the route that names it, so a legacy run keeps its own rooms.
        return context.CurrentActIndex == 0 && context.LastFixedAct == 1
            || context.CurrentActIndex == 1 && context.LastFixedAct == 2 && context.OnDefaultRoute;
    }
}
