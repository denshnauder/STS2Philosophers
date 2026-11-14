namespace STS2Philosophers;

internal sealed record HegelRecognitionTriggerContext(
    bool RunInProgress,
    bool AtNativeQuestionEntry,
    bool IsSingleplayer,
    int CurrentActIndex,
    int ActIndex,
    string? CurrentNodeId,
    int LastFixedAct,
    bool SourceIsCompletedNonBossCombat,
    int CompletedCombatCount,
    bool CompletionEdgeEligible,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedRouteConsistent,
    bool ModelAvailable);

internal static class HegelRecognitionTriggerPolicy
{
    public static bool ShouldInsert(HegelRecognitionTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtNativeQuestionEntry || !context.IsSingleplayer
            || !context.ModelAvailable || context.CurrentActIndex + 1 != context.ActIndex) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == HegelRecognitionState.SourceNodeId
            && context.LastFixedAct >= context.ActIndex && context.CompletionEdgeEligible;
    }
}
