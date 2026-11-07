namespace STS2Philosophers;

internal sealed record SpinozaCauseNetworkTriggerContext(
    bool RunInProgress,
    bool AtNativeQuestionEntry,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool HasRequiredRelic,
    bool HasSourceDoctrine,
    bool HasOpenRouteQuestion,
    string? CurrentNodeId,
    int LastFixedAct,
    bool FixedActTwoResolved,
    bool RetainedEnding,
    bool HasEligibleFixedEdge,
    bool HasCompletedActCombat,
    bool HasSeenSpinoza,
    bool HasSavedInstance,
    bool SavedInstanceUsable,
    bool SavedInstanceClosed,
    bool SavedInstanceConsistent,
    bool ModelAvailable,
    string? SourceNode = null);

internal static class SpinozaCauseNetworkTriggerPolicy
{
    internal const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";

    internal static bool ShouldInsert(SpinozaCauseNetworkTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtNativeQuestionEntry || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable || !context.HasRequiredRelic
            || !context.HasSourceDoctrine || context.HasOpenRouteQuestion) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedInstanceConsistent;
        return context.CurrentNodeId == (context.SourceNode ?? SourceNodeId)
            && context.LastFixedAct <= 2
            && !context.FixedActTwoResolved
            && !context.RetainedEnding
            && context.HasEligibleFixedEdge
            && context.HasCompletedActCombat
            && !context.HasSeenSpinoza;
    }
}
