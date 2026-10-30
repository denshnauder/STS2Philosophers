namespace STS2Philosophers;

internal sealed record DescartesMethodicReconstructionTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool FixedActAlreadyDeclined,
    bool HasRequiredRelic,
    bool SextusEncounterClosed,
    bool HasNaturalFork,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool ModelAvailable);

internal static class DescartesMethodicReconstructionTriggerPolicy
{
    public static bool ShouldInsert(DescartesMethodicReconstructionTriggerContext context)
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
            && context.CurrentNodeId == DescartesMethodicReconstructionState.RequiredNodeId
            && context.LastFixedAct <= 2
            && !context.FixedActAlreadyDeclined
            && context.HasRequiredRelic
            && context.SextusEncounterClosed
            && context.HasNaturalFork;
    }
}
