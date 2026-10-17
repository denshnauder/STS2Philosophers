namespace STS2Philosophers;

internal sealed record ParmenidesInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct, bool QuestionAlreadyResolved,
    bool HasRequiredRelic, bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class ParmenidesInquiryTriggerPolicy
{
    public static bool ShouldInsert(ParmenidesInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 0 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed
            && context.LastFixedAct == 1 && context.CurrentNodeId is
                ParmenidesInquiryState.RequiredNodeId or ParmenidesInquiryState.AdoptedNodeId;
        return context.CompletedCombatCount >= 1 && context.LastFixedAct == 1
            && context.CurrentNodeId == ParmenidesInquiryState.RequiredNodeId
            && !context.QuestionAlreadyResolved && context.HasRequiredRelic;
    }
}
