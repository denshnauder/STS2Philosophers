namespace STS2Philosophers;

internal sealed record AnaximenesBreathInvitationTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer,
    int CurrentActIndex, int CompletedCombatCount, string? CurrentNodeId,
    bool HasLyre, bool HasPracticeRelic, bool HasSavedInstance,
    bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class AnaximenesBreathInvitationTriggerPolicy
{
    public static bool ShouldInsert(AnaximenesBreathInvitationTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || context.CurrentNodeId != AnaximenesBreathInvitationState.RequiredNodeId
            || !context.HasLyre || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return context.CompletedCombatCount >= 1 && !context.HasPracticeRelic;
    }
}
