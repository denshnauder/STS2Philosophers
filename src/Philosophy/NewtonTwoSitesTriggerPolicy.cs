namespace STS2Philosophers;

internal sealed record NewtonTwoSitesTriggerContext(
    bool RunInProgress, bool AtNativeQuestionEntry, bool IsSingleplayer, int CurrentActIndex,
    bool HasRequiredRelic, bool HasValidKnowledgePredecessors, bool HasClosedRebuiltDescartes,
    bool HasPyrrhoInstance, bool HasAnselmInstance, bool HasOpenRouteQuestion,
    string? CurrentNodeId, int LastFixedAct, bool FixedActThreeResolved, bool RetainedEnding,
    bool HasEligibleFixedEdge, bool HasCompletedCombatAfterDescartes,
    bool HasSavedInstance, bool SavedInstanceUsable, bool SavedInstanceClosed,
    bool SavedInstanceConsistent, bool ModelAvailable);

internal static class NewtonTwoSitesTriggerPolicy
{
    public const string SourceNodeId = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";

    public static bool ShouldInsert(NewtonTwoSitesTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtNativeQuestionEntry || !context.IsSingleplayer
            || context.CurrentActIndex != 2 || !context.ModelAvailable || !context.HasRequiredRelic
            || !context.HasValidKnowledgePredecessors || !context.HasClosedRebuiltDescartes
            || context.HasPyrrhoInstance || context.HasAnselmInstance || context.HasOpenRouteQuestion)
            return false;

        // An already committed choice may have consumed Fixed3. Recover that
        // exact instance using its consistency check, never a second fresh claim.
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed
                && context.SavedInstanceConsistent;

        return context.CurrentNodeId == SourceNodeId && context.LastFixedAct >= 2
            && !context.FixedActThreeResolved && !context.RetainedEnding
            && context.HasEligibleFixedEdge && context.HasCompletedCombatAfterDescartes;
    }
}
