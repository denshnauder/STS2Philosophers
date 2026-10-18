namespace STS2Philosophers;

internal sealed record AnaximanderOriginInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct,
    bool PriorQuestionResolved, bool PriorInquiryClosed, bool HasRequiredRelic,
    bool FixedOpportunityUnresolved, bool NoFixedCandidate, bool SecondQuestionUnresolved,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class AnaximanderOriginInquiryTriggerPolicy
{
    public static bool ShouldInsert(AnaximanderOriginInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable || !context.PriorQuestionResolved
            || !context.PriorInquiryClosed || context.CurrentNodeId != AnaximanderOriginInquiryState.RequiredNodeId)
            return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.LastFixedAct == 2;
        return context.CompletedCombatCount >= 1 && context.LastFixedAct == 1
            && context.HasRequiredRelic && context.FixedOpportunityUnresolved
            && context.NoFixedCandidate && context.SecondQuestionUnresolved;
    }
}
