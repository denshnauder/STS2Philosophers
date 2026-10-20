namespace STS2Philosophers;

internal sealed record PyrrhoReportedCrossingTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, string? CurrentNodeId, int LastFixedAct,
    bool FirstQuestionResolved, bool SecondQuestionResolved, bool SecondFixedDeclined,
    bool HasRequiredRelic, bool SextusClosed, bool DescartesDeclinedAndClosed,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed, bool ModelAvailable);

internal static class PyrrhoReportedCrossingTriggerPolicy
{
    public static bool ShouldInsert(PyrrhoReportedCrossingTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.SourceIsCompletedNonBossCombat
            || context.CurrentNodeId != SextusOpposedAppearancesState.RequiredNodeId
            || context.LastFixedAct != 2 || !context.FirstQuestionResolved
            || !context.SecondFixedDeclined || !context.HasRequiredRelic
            || !context.SextusClosed || !context.DescartesDeclinedAndClosed
            || !context.ModelAvailable) return false;
        return context.HasSavedInstance
            ? context.SavedInstanceUsable && !context.SavedInstanceClosed
            : !context.SecondQuestionResolved;
    }
}
