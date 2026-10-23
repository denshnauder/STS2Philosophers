namespace STS2Philosophers;

internal sealed record CopernicusShiftedCenterTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, bool HasRequiredRelic, bool PredecessorReady, bool NativeForkReady,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool SavedRouteConsistent, bool ModelAvailable);
internal static class CopernicusShiftedCenterTriggerPolicy
{
    public static bool ShouldInsert(CopernicusShiftedCenterTriggerContext c)
    {
        if (!c.RunInProgress || !c.AtSafeMapBoundary || !c.IsSingleplayer || c.CurrentActIndex != 1
            || !c.ModelAvailable || !c.HasRequiredRelic) return false;
        if (c.HasSavedInstance) return c.SavedInstanceUsable && !c.SavedInstanceClosed && c.SavedRouteConsistent && c.NativeForkReady;
        return c.SourceIsCompletedNonBossCombat && c.PredecessorReady && c.NativeForkReady;
    }
}
