namespace STS2Philosophers;

internal sealed record AristotleCivicInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct, bool HasRequiredRelic,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable,
    bool AugustinePrerequisiteCompleted = false);

internal static class AristotleCivicInquiryTriggerPolicy
{
    public static bool ShouldInsert(AristotleCivicInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed
            && context.LastFixedAct is 1 or 2 && context.CurrentNodeId is
                AristotleCivicInquiryState.RequiredNodeId or AristotleCivicInquiryState.AdoptedNodeId;
        return !context.AugustinePrerequisiteCompleted
            && context.CompletedCombatCount >= 1 && context.LastFixedAct == 1
            && context.CurrentNodeId == AristotleCivicInquiryState.RequiredNodeId && context.HasRequiredRelic;
    }
}
