namespace STS2Philosophers;

internal sealed record PlatoWholeArrangementTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool RequiredEdgeCompleted,
    bool FixedActDeclined,
    bool CompletionEdgeCompleted,
    bool HasRequiredRelic,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class PlatoWholeArrangementTriggerPolicy
{
    public static bool ShouldInsert(PlatoWholeArrangementTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable)
        {
            return false;
        }

        if (context.HasSavedInstance)
        {
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        }

        return context.CompletedCombatCount >= 1
            && context.CurrentNodeId == PlatoWholeArrangementState.SourceNodeId
            && context.LastFixedAct <= 2
            && context.RequiredEdgeCompleted
            && !context.FixedActDeclined
            && !context.CompletionEdgeCompleted
            && context.HasRequiredRelic;
    }
}
