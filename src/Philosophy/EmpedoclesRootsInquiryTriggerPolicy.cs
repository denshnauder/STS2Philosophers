namespace STS2Philosophers;

internal sealed record EmpedoclesRootsInquiryTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    string? CurrentNodeId, int LastFixedAct, bool DemocritusAdopted,
    bool PriorEventClosed, bool QuestionUnresolved, bool EdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class EmpedoclesRootsInquiryTriggerPolicy
{
    public static bool ShouldInsert(EmpedoclesRootsInquiryTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable
            || context.CurrentNodeId != EmpedoclesRootsInquiryState.RequiredNodeId
            || context.LastFixedAct != 2 || !context.DemocritusAdopted || !context.PriorEventClosed)
            return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return context.QuestionUnresolved && context.EdgeEligible;
    }
}
