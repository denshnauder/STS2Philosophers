namespace STS2Philosophers;

internal sealed record AquinasBridgeNoticeTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount,
    string? CurrentNodeId, int LastFixedAct, bool RequiredEdgeCompleted,
    bool RivalEdgeCompleted, bool CompletionEdgeEligible, bool HasRequiredRelic,
    bool AristotleAdoptedAndClosed, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool SavedRouteConsistent, bool ModelAvailable);

internal static class AquinasBridgeNoticeTriggerPolicy
{
    public static bool ShouldInsert(AquinasBridgeNoticeTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == AquinasBridgeNoticeState.SourceNodeId
            && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
            && !context.RivalEdgeCompleted && context.CompletionEdgeEligible
            && context.HasRequiredRelic && context.AristotleAdoptedAndClosed;
    }
}
