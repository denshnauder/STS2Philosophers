namespace STS2Philosophers;

internal sealed record FregeSenseTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount, string? CurrentNodeId,
    int LastFixedAct, bool CompletionEdgeEligible, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool SavedRouteConsistent, bool ModelAvailable, int RouteAct = 0);

internal static class FregeSenseTriggerPolicy
{
    public static bool ShouldInsert(FregeSenseTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || !context.ModelAvailable) return false;
        int act = context.RouteAct >= 2 ? context.RouteAct - 1 : 0;
        int position = context.RouteAct >= 2 ? context.RouteAct : 1;
        if (context.CurrentActIndex != act) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && (context.RouteAct >= 2 || context.CurrentNodeId == FregeSenseState.SourceNodeId)
            && context.LastFixedAct == position && context.CompletionEdgeEligible;
    }
}
