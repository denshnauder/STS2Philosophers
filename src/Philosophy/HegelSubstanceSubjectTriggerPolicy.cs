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
    bool TurnedInFirstAct = false);

internal static class HegelSubstanceSubjectTriggerPolicy
{
    public static bool ShouldInsert(HegelSubstanceSubjectTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtNativeQuestionEntry || !context.IsSingleplayer
            || context.CurrentActIndex != (context.TurnedInFirstAct ? 1 : 2)
            || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == (context.TurnedInFirstAct
                ? HegelSubstanceSubjectState.TurnedSourceNodeId
                : HegelSubstanceSubjectState.SourceNodeId)
            && context.LastFixedAct >= 2 && context.RequiredEdgeCompleted
            && context.CompletionEdgeEligible;
    }
}
