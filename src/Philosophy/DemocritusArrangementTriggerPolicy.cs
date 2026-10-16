namespace STS2Philosophers;

internal sealed record DemocritusArrangementTriggerContext(
    bool RunInProgress, bool CurrentRoomIsMap, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct, bool HasRequiredRelic,
    bool HasPrerequisite, bool RetainedEnding, bool IsSafeRoomStack,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class DemocritusArrangementTriggerPolicy
{
    public static bool ShouldInsert(DemocritusArrangementTriggerContext context)
    {
        if (!context.RunInProgress || !context.CurrentRoomIsMap || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable
            || !context.HasPrerequisite || context.RetainedEnding || !context.IsSafeRoomStack) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed
            && context.LastFixedAct is 1 or 2 && context.CurrentNodeId is
                DemocritusArrangementState.RequiredNodeId or DemocritusArrangementState.AdoptedNodeId;
        return context.CompletedCombatCount >= 1 && context.LastFixedAct == 1
            && context.CurrentNodeId == DemocritusArrangementState.RequiredNodeId && context.HasRequiredRelic;
    }
}
