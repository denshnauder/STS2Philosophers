namespace STS2Philosophers;

internal sealed record AnaxagorasMixtureInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    string? CurrentNodeId, int LastFixedAct, bool ParmenidesReached,
    bool SecondFixedDeclined, bool PriorEventClosed, bool QuestionUnresolved,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class AnaxagorasMixtureInquiryTriggerPolicy
{
    public static bool ShouldInsert(AnaxagorasMixtureInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable
            || context.CurrentNodeId != AnaxagorasMixtureInquiryState.RequiredNodeId
            || context.LastFixedAct != 2 || !context.ParmenidesReached
            || !context.SecondFixedDeclined || !context.PriorEventClosed) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return context.QuestionUnresolved;
    }
}
