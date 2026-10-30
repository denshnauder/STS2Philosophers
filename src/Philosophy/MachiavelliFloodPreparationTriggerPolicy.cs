namespace STS2Philosophers;

internal sealed record MachiavelliFloodPreparationTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount,
    string? CurrentNodeId, int LastFixedAct, bool CompletionEdgeEligible, bool HasRequiredRelic,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);
internal static class MachiavelliFloodPreparationTriggerPolicy
{
    public static bool ShouldInsert(MachiavelliFloodPreparationTriggerContext c)
    {
        if (!c.RunInProgress || !c.AtSafeMapBoundary || !c.IsSingleplayer || c.CurrentActIndex != 1
            || !c.ModelAvailable || !c.HasRequiredRelic) return false;
        if (c.HasSavedInstance) return c.SavedInstanceUsable && !c.SavedInstanceClosed && c.SavedRouteConsistent;
        return c.SourceIsCompletedNonBossCombat && c.CompletedCombatCount >= 1 && c.LastFixedAct <= 2
            && c.CurrentNodeId == MachiavelliFloodPreparationState.SourceNodeId && c.CompletionEdgeEligible;
    }
}
