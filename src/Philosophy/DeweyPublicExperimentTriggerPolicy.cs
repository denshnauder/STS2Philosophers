namespace STS2Philosophers;

internal sealed record DeweyPublicExperimentTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 0);

internal static class DeweyPublicExperimentTriggerPolicy
{
    public static bool ShouldInsert(DeweyPublicExperimentTriggerContext context)
    {
        // A route may hand this room the second or the third act; only that act
        // accepts it, and only at that act's own slot.
        int act = context.RouteAct >= 2 ? context.RouteAct - 1 : 2;
        int position = context.RouteAct >= 2 ? context.RouteAct : 3;
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != act || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (context.RouteAct >= 2 || context.CurrentNodeId == DeweyPublicExperimentState.SourceNodeId)
            && context.LastFixedAct == position
            && (context.RouteAct >= 2 || context.RequiredEdgeCompleted)
            && context.CompletionEdgeEligible;
    }
}
