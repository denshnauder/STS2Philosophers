namespace STS2Philosophers;

internal sealed record HegelSubstanceSubjectTriggerContext(
    bool RunInProgress,
    bool AtNativeQuestionEntry,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool RequiredEdgeCompleted,
    bool CompletionEdgeEligible,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable,
    bool TurnedInFirstAct = false,
    int ActIndex = 0,
    string? SourceNode = null);

internal static class HegelSubstanceSubjectTriggerPolicy
{
    public static bool ShouldInsert(HegelSubstanceSubjectTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtNativeQuestionEntry || !context.IsSingleplayer
            || context.CurrentActIndex + 1 != (context.ActIndex > 0
                ? context.ActIndex
                : context.TurnedInFirstAct ? 2 : 3)
            || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == (context.SourceNode ?? (context.TurnedInFirstAct
                ? HegelSubstanceSubjectState.TurnedSourceNodeId
                : HegelSubstanceSubjectState.SourceNodeId))
            && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
