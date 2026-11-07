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
    bool ModelAvailable,
    int RouteAct = 0);

internal static class WollstonecraftAdmissionTriggerPolicy
{
    public static bool ShouldInsert(WollstonecraftAdmissionTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        // The room may open before its handover (at Rousseau) or after it (at
        // Wollstonecraft's own node, where the route has already placed her).
        bool atSource = context.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId
            && context.LastFixedAct <= 2;
        bool atAdopted = context.RouteAct >= 2
            && context.CurrentNodeId == WollstonecraftAdmissionState.AdoptedNodeId
            && context.LastFixedAct == context.RouteAct;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (atSource || atAdopted) && !context.RivalEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
