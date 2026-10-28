namespace STS2Philosophers;

internal static class MachiavelliRealistChallengeRoute
{
    public const int QuestionAct = 2;
    public const string SourceNodeId = "ARISTOTLE__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_016";
    public const string EdgeId = "WESTERN_EDGE_017";
}

internal sealed record MachiavelliRealistChallengeTriggerContext(
    bool RunInProgress,
    bool AtSafeMapBoundary,
    bool IsSingleplayer,
    int CurrentActIndex,
    int CompletedCombatCount,
    string? CurrentNodeId,
    int LastFixedAct,
    bool RequiredEdgeCompleted,
    bool QuestionResolved,
    bool EdgeEligible,
    bool AristotleClosed,
    bool ModelAvailable);

internal static class MachiavelliRealistChallengeTriggerPolicy
{
    public static bool ShouldInsert(MachiavelliRealistChallengeTriggerContext context)
    {
        if (!context.RunInProgress || !context.AtSafeMapBoundary || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable)
        {
            return false;
        }

        return context.CompletedCombatCount >= 1
            && context.CurrentNodeId == MachiavelliRealistChallengeRoute.SourceNodeId
            && context.LastFixedAct == MachiavelliRealistChallengeRoute.QuestionAct
            && context.RequiredEdgeCompleted
            && !context.QuestionResolved
            && context.EdgeEligible
            && context.AristotleClosed;
    }
}
