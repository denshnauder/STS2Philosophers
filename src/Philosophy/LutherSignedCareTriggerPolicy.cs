namespace STS2Philosophers;

internal sealed record LutherSignedCareTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount,
    string? CurrentNodeId, int LastFixedAct, bool QuestionResolved, bool HasRequiredRelic,
    bool PredecessorAdoptedAndClosed, bool PredecessorRouteConsistent, bool EdgeEligible,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);

internal static class LutherSignedCareTriggerPolicy
{
    public static bool ShouldInsert(LutherSignedCareTriggerContext c)
    {
        if (!c.RunInProgress || !c.AtSafeMapBoundary || !c.IsSingleplayer || c.CurrentActIndex != 1
            || !c.HasRequiredRelic || !c.ModelAvailable) return false;
        if (c.HasSavedInstance) return c.SavedInstanceUsable && !c.SavedInstanceClosed && c.SavedRouteConsistent;
        return c.SourceIsCompletedNonBossCombat && c.CompletedCombatCount >= 1 && c.LastFixedAct == 2
            && c.CurrentNodeId == LutherSignedCareState.SourceNodeId && !c.QuestionResolved
            && c.PredecessorAdoptedAndClosed && c.PredecessorRouteConsistent && c.EdgeEligible;
    }
}
