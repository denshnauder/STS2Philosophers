namespace STS2Philosophers;

internal sealed record OckhamNamesInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount,
    string? CurrentNodeId, int LastFixedAct, bool FirstQuestionResolved,
    bool SecondQuestionResolved, bool HasRequiredRelic, bool GorgiasClosed, bool GorgiasRouteConsistent,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);

internal static class OckhamNamesInquiryTriggerPolicy
{
    public static bool ShouldInsert(OckhamNamesInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable || !context.HasRequiredRelic) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == OckhamNamesInquiryState.RequiredNodeId
            && context.LastFixedAct == 1 && context.FirstQuestionResolved
            && !context.SecondQuestionResolved && context.GorgiasClosed && context.GorgiasRouteConsistent;
    }
}
