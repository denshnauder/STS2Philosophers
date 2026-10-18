namespace STS2Philosophers;

internal sealed record SextusOpposedAppearancesTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool QuestionAlreadyResolved,
    bool HasRequiredRelic,
    bool HasNaturalFork,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class SextusOpposedAppearancesTriggerPolicy
{
    public static bool ShouldInsert(SextusOpposedAppearancesTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 0 || !context.ModelAvailable)
        {
            return false;
        }

        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        }

        return context.CompletedCombatCount >= 1
            && context.CurrentNodeId == SextusOpposedAppearancesState.RequiredNodeId
            && context.LastFixedAct == 1
            && !context.QuestionAlreadyResolved
            && context.HasRequiredRelic
            && context.HasNaturalFork;
    }
}
