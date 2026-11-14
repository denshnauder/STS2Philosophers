namespace STS2Philosophers;

internal sealed record MarxAlienationTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 0);

internal static class MarxAlienationTriggerPolicy
{
    public static bool ShouldInsert(MarxAlienationTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || !context.ModelAvailable) return false;
        // The question opens in the room's own act; a route may place this room in
        // act two or three, and then only that act accepts it.
        int act = context.RouteAct >= 2 ? context.RouteAct - 1 : 2;
        int position = context.RouteAct >= 2 ? context.RouteAct : 3;
        if (context.CurrentActIndex != act) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (context.RouteAct >= 2 || context.CurrentNodeId == MarxAlienationState.SourceNodeId)
            && context.LastFixedAct == position && (context.RouteAct >= 2 || context.RequiredEdgeCompleted)
            && context.CompletionEdgeEligible;
    }
}
