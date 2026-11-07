namespace STS2Philosophers;

internal sealed record KuhnParadigmTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool RequiredEdgeCompleted, bool CompletionEdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 3);

internal static class KuhnParadigmTriggerPolicy
{
    public static bool ShouldInsert(KuhnParadigmTriggerContext context)
    {
        // Both fixed edges are third-act edges. The boundary is the act-three
        // boundary, whose position is already 3 when the room is asked.
        if (context.RouteAct != 3 || !context.RunInProgress || !context.AtSafeMapBoundary
            || !context.IsSingleplayer || context.CurrentActIndex != 2
            || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (context.CurrentNodeId == KuhnParadigmState.NewtonSourceNodeId
                || context.CurrentNodeId == KuhnParadigmState.ComteSourceNodeId)
            && context.LastFixedAct == 3 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
