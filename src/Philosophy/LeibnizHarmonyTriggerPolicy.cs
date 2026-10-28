namespace STS2Philosophers;

internal sealed record LeibnizHarmonyTriggerContext(
    ulong RunSeed,
    bool RunInProgress,
    bool AtNativeQuestionEntry,
    bool IsSingleplayer,
    int CurrentActIndex,
    bool OwnsSoleDescartesThinkingLens,
    string CurrentDoctrineId,
    string CurrentDoctrineLensId,
    bool PredecessorUsable,
    bool PredecessorConsistent,
    bool PredecessorClosed,
    bool PredecessorModelAvailable,
    string PredecessorInstanceId,
    string CurrentNodeId,
    int LastFixedAct,
    bool Fixed3Unused,
    bool CompletionEdgeEligible,
    bool InEligibleCombat,
    bool HasOpenRouteQuestion,
    bool HasEdge024,
    bool LeibnizSeen,
    bool HasResolvedLaterQuestions,
    bool RetainedEnding,
    bool HasSavedInstance,
    bool SavedStateUsable,
    bool SavedStateConsistent,
    bool SavedStateClosed,
    bool SavedModelAvailable,
    string SavedInstanceId,
    string SavedSpinozaInstanceId);

internal static class LeibnizHarmonyTriggerPolicy
{
    public const string DescartesDoctrineId = "DESCARTES";
    public const string DescartesLensId = "DESCARTES_THINKING_LENS";

    public static bool ShouldInsert(LeibnizHarmonyTriggerContext context)
    {
        if (context is null) return false;

        // Common guard: applies to both fresh insertion and saved-result recovery.
        if (!context.RunInProgress) return false;
        if (!context.AtNativeQuestionEntry) return false;
        if (!context.IsSingleplayer) return false;
        if (context.CurrentActIndex != 2) return false;
        if (!context.OwnsSoleDescartesThinkingLens) return false;
        if (!string.Equals(context.CurrentDoctrineId, DescartesDoctrineId, StringComparison.Ordinal)) return false;
        if (!string.Equals(context.CurrentDoctrineLensId, DescartesLensId, StringComparison.Ordinal)) return false;

        if (!context.PredecessorUsable || !context.PredecessorConsistent
            || !context.PredecessorClosed || !context.PredecessorModelAvailable) return false;
        if (string.IsNullOrWhiteSpace(context.PredecessorInstanceId)) return false;

        if (context.HasOpenRouteQuestion) return false;

        string expected = LeibnizHarmonyState.BuildInstanceId(context.RunSeed);

        // Saved-result recovery: only requires a usable, still-open, consistent saved instance.
        if (context.HasSavedInstance)
        {
            return context.SavedStateUsable
                && !context.SavedStateClosed
                && context.SavedStateConsistent
                && context.SavedModelAvailable
                && string.Equals(context.SavedInstanceId, expected, StringComparison.Ordinal)
                && string.Equals(context.SavedSpinozaInstanceId, context.PredecessorInstanceId, StringComparison.Ordinal);
        }

        // Fresh insertion: source-node and journey-state guards apply only here.
        if (!string.Equals(context.CurrentNodeId, LeibnizHarmonyState.SourceNodeId, StringComparison.Ordinal)) return false;
        if (context.LastFixedAct != 2) return false;
        if (!context.Fixed3Unused) return false;
        if (!context.CompletionEdgeEligible) return false;
        if (!context.InEligibleCombat) return false;
        if (context.HasEdge024) return false;
        if (context.LeibnizSeen) return false;
        if (context.HasResolvedLaterQuestions) return false;
        if (context.RetainedEnding) return false;

        return true;
    }
}
