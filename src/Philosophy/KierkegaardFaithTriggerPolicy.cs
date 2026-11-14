namespace STS2Philosophers;

internal sealed record KierkegaardFaithTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool CompletionEdgeEligible, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 0,
    bool AtFixedEntry = false);

internal static class KierkegaardFaithTriggerPolicy
{
    public static bool ShouldInsert(KierkegaardFaithTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || !context.ModelAvailable) return false;
        // The default line reaches this room at the second-act fixed boundary,
        // after Socrates' first-act fixed event.  It is not a map question.
        if (context.AtFixedEntry)
        {
            if (context.CurrentActIndex != 1) return false;
            if (context.HasSavedInstance)
                return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
            return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
                && context.CurrentNodeId == KierkegaardFaithState.SourceNodeId
                && context.LastFixedAct == 2 && context.CompletionEdgeEligible;
        }
        // The early-turn question opens in the room's own act; a route may place
        // this room in act two or three, and then only that act accepts it.
        // 158 is itself the completed first-act question. The scene belongs
        // immediately after that choice, before the second-act boundary, so it
        // must not require an extra combat that the route question never had.
        if (context.RouteAct == 1 && context.CurrentActIndex == 0
            && !context.HasSavedInstance
            && context.CurrentNodeId == KierkegaardFaithState.AdoptedNodeId
            && context.LastFixedAct == 1 && context.CompletionEdgeEligible)
            return true;
        int act = context.RouteAct >= 2 ? context.RouteAct - 1 : 0;
        int position = context.RouteAct >= 2 ? context.RouteAct : 1;
        if (context.CurrentActIndex != act) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == KierkegaardFaithState.SourceNodeId
            && context.LastFixedAct == position && context.CompletionEdgeEligible;
    }
}
