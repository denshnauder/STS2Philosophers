namespace STS2Philosophers;

internal sealed record HanFeiPublicStandardTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    bool HasRequiredRelic,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class HanFeiPublicStandardTriggerPolicy
{
    public static bool ShouldInsert(HanFeiPublicStandardTriggerContext context)
    {
        if (!context.RunInProgress
            || !context.AtSafeMapBoundary
            || !context.IsSingleplayer
            || context.CurrentActIndex != LaterActRelicEligibilityCatalog.ActThreeIndex
            || !context.ModelAvailable)
        {
            return false;
        }

        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        }

        return context.CompletedCombatCount >= 1 && context.HasRequiredRelic;
    }
}
