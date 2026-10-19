namespace STS2Philosophers;

internal sealed record GorgiasSpeechInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct, bool QuestionUnresolved,
    bool HasRequiredRelic, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool ModelAvailable);

internal static class GorgiasSpeechInquiryTriggerPolicy
{
    public static bool ShouldInsert(GorgiasSpeechInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 0 || !context.ModelAvailable
            || context.CurrentNodeId != GorgiasSpeechInquiryState.RequiredNodeId
            || context.LastFixedAct != 1) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return context.CompletedCombatCount >= 1 && context.QuestionUnresolved && context.HasRequiredRelic;
    }
}
