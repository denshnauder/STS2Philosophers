namespace STS2Philosophers;

internal sealed record MachiavelliEmergencyMandateTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    int CompletedCombatCount, string? CurrentNodeId, int LastFixedAct, bool HasRequiredRelic,
    bool HasPrerequisite, bool QuestionAlreadyResolved, bool RetainedEnding,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class MachiavelliEmergencyMandateTriggerPolicy
{
    public static bool ShouldInsert(MachiavelliEmergencyMandateTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || context.LastFixedAct != 2
            || context.CurrentNodeId != MachiavelliEmergencyMandateState.RequiredNodeId
            || !context.HasPrerequisite || context.RetainedEnding || !context.ModelAvailable) return false;
        if (context.HasSavedInstance) return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return context.CompletedCombatCount >= 1 && context.HasRequiredRelic && !context.QuestionAlreadyResolved;
    }
}
