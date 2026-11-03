namespace STS2Philosophers;

internal sealed record DiderotPublicPracticeContext(
    bool RunInProgress, bool AtQuestionNode, bool IsSingleplayer, int CurrentActIndex,
    string? RouteId, string? CurrentNodeId, int LastFixedAct, bool HostCompleted,
    bool HostRecordUsable, bool AlreadyResolved, bool HasSavedInstance, bool SavedInstanceUsable,
    bool SavedInstanceClosed, bool ModelAvailable);

internal static class DiderotPublicPracticePolicy
{
    // The social-knowledge line reaches Diderot only at a real act-two question
    // node, after Bacon's fixed room has taken the slot. It is an extra event: it
    // never advances the fixed position and never stands in for the act's own
    // question, and a run that never draws it loses nothing.
    public static bool ShouldInsert(DiderotPublicPracticeContext context)
    {
        if (!context.RunInProgress || !context.AtQuestionNode || !context.IsSingleplayer
            || context.CurrentActIndex != 1 || !context.ModelAvailable) return false;
        if (context.HasSavedInstance)
            return context.SavedInstanceUsable && !context.SavedInstanceClosed;
        return !context.AlreadyResolved && context.HostCompleted && context.HostRecordUsable
            && context.LastFixedAct == 2
            && context.CurrentNodeId == BaconSampleLedgerState.AdoptedNodeId
            && context.RouteId == "SOCIAL_KNOWLEDGE";
    }
}
