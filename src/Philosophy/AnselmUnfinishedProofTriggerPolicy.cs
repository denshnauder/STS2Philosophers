namespace STS2Philosophers;

internal sealed record AnselmUnfinishedProofTriggerContext(
    bool RunInProgress, bool AtSafeMapBoundary, bool IsSingleplayer, int CurrentActIndex,
    bool SourceIsCompletedNonBossCombat, int CompletedCombatCount,
    string? CurrentNodeId, int LastFixedAct, bool FirstQuestionResolved,
    bool SecondFixedDeclined, bool SecondQuestionResolved,
    bool ThirdFixedResolved, bool ThirdQuestionResolved, bool HasRequiredRelic,
    bool PyrrhoClosed, bool PyrrhoRouteConsistent,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedRouteConsistent, bool ModelAvailable);

internal static class AnselmUnfinishedProofTriggerPolicy
{
    public static bool ShouldInsert(AnselmUnfinishedProofTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedRouteConsistent;
        return context.SourceIsCompletedNonBossCombat && context.CompletedCombatCount >= 1
            && context.CurrentNodeId == AnselmUnfinishedProofState.RequiredNodeId
            && context.LastFixedAct == 2 && context.FirstQuestionResolved
            && context.SecondFixedDeclined && context.SecondQuestionResolved
            && !context.ThirdFixedResolved && !context.ThirdQuestionResolved
            && context.HasRequiredRelic && context.PyrrhoClosed && context.PyrrhoRouteConsistent;
    }
}
