namespace STS2Philosophers;

internal sealed record KantUniversalizationTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 0);

internal static class KantUniversalizationTriggerPolicy
{
    public static bool ShouldInsert(KantUniversalizationTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || !context.ModelAvailable) return false;
        // The room's own act is the third; a route may place it elsewhere, and
        // then only that act accepts it.
        int act = context.RouteAct >= 2 ? context.RouteAct - 1 : 2;
        int position = context.RouteAct >= 2 ? context.RouteAct : 2;
        if (context.CurrentActIndex != act) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (context.RouteAct >= 2 || context.CurrentNodeId == KantUniversalizationState.SourceNodeId)
            && context.LastFixedAct >= position
            && (context.RouteAct >= 2 || context.RequiredEdgeCompleted)
            && context.CompletionEdgeEligible;
    }
}
