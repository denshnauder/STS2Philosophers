namespace STS2Philosophers;

internal static class SpinozaCauseNetworkFlow
{
    public static IReadOnlyList<SpinozaCauseNetworkAction> Options(SpinozaCauseNetworkStage stage) => stage switch
    {
        SpinozaCauseNetworkStage.Invitation => new[] { SpinozaCauseNetworkAction.Accept, SpinozaCauseNetworkAction.Decline },
        SpinozaCauseNetworkStage.Attribution => new[] { SpinozaCauseNetworkAction.BlameEnemy, SpinozaCauseNetworkAction.TraceChains },
        SpinozaCauseNetworkStage.WeatherRevealed => new[] { SpinozaCauseNetworkAction.Continue },
        SpinozaCauseNetworkStage.Decision => new[] { SpinozaCauseNetworkAction.Intervene, SpinozaCauseNetworkAction.Reorder, SpinozaCauseNetworkAction.CorrectRecord },
        SpinozaCauseNetworkStage.ResultIntervened => new[] { SpinozaCauseNetworkAction.Leave },
        SpinozaCauseNetworkStage.ResultReordered => new[] { SpinozaCauseNetworkAction.Leave },
        SpinozaCauseNetworkStage.ResultCorrected => new[] { SpinozaCauseNetworkAction.Leave },
        SpinozaCauseNetworkStage.ResultDeclined => new[] { SpinozaCauseNetworkAction.Leave },
        _ => Array.Empty<SpinozaCauseNetworkAction>()
    };

    public static bool TryPrepare(SpinozaCauseNetworkState state, SpinozaCauseNetworkAction action)
    {
        if (state is null || !state.IsUsable() || state.Closed) return false;
        if (state.PendingAction != SpinozaCauseNetworkAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    internal static void Apply(SpinozaCauseNetworkState state, SpinozaCauseNetworkAction action)
    {
        if (state.Closed || !Options(state.Stage).Contains(action)) return;
        switch (action)
        {
            case SpinozaCauseNetworkAction.Accept:
                state.Stage = SpinozaCauseNetworkStage.Attribution;
                break;
            case SpinozaCauseNetworkAction.Decline:
                state.Stage = SpinozaCauseNetworkStage.ResultDeclined;
                state.FixedSlotResolved = true;
                state.Disposition = SpinozaDisposition.None;
                break;
            case SpinozaCauseNetworkAction.BlameEnemy:
                state.FirstAttribution = SpinozaAttribution.BlameEnemy;
                state.Stage = SpinozaCauseNetworkStage.WeatherRevealed;
                break;
            case SpinozaCauseNetworkAction.TraceChains:
                state.FirstAttribution = SpinozaAttribution.TraceChains;
                state.Stage = SpinozaCauseNetworkStage.WeatherRevealed;
                break;
            case SpinozaCauseNetworkAction.Continue:
                state.Stage = SpinozaCauseNetworkStage.Decision;
                break;
            case SpinozaCauseNetworkAction.Intervene:
                state.Stage = SpinozaCauseNetworkStage.ResultIntervened;
                state.Disposition = SpinozaDisposition.Intervened;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case SpinozaCauseNetworkAction.Reorder:
                state.Stage = SpinozaCauseNetworkStage.ResultReordered;
                state.Disposition = SpinozaDisposition.Reordered;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case SpinozaCauseNetworkAction.CorrectRecord:
                state.Stage = SpinozaCauseNetworkStage.ResultCorrected;
                state.Disposition = SpinozaDisposition.Corrected;
                state.FixedSlotResolved = true;
                break;
            case SpinozaCauseNetworkAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SpinozaCauseNetworkAction.None;
    }

    public static bool IsRouteConsistent(SpinozaCauseNetworkState state, WesternJourneyState journey)
    {
        if (state is null || journey is null || !state.IsUsable()) return false;
        if (journey.SeenThinkerIds is null || journey.ResolvedQuestionActs is null
            || journey.DeclinedFixedActs is null || journey.CompletedEdgeIds is null) return false;
        if (!journey.SeenThinkerIds.Contains("DESCARTES")) return false;
        if (journey.RetainedEnding) return false;
        if (journey.ResolvedQuestionActs.Contains(2) || journey.ResolvedQuestionActs.Contains(3)) return false;
        if (journey.DeclinedFixedActs.Contains(3)) return false;

        if (!state.FixedSlotResolved)
        {
            return journey.CurrentNodeId == SpinozaCauseNetworkState.SourceNodeId
                && journey.LastFixedAct is 1 or 2
                && !journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
                && !journey.SeenThinkerIds.Contains("BARUCH_SPINOZA");
        }

        if (journey.LastFixedAct != 2) return false;
        if (state.EdgeCommitted)
        {
            return journey.CurrentNodeId == SpinozaCauseNetworkState.AdoptedNodeId
                && journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
                && journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
                && !journey.DeclinedFixedActs.Contains(2);
        }
        return journey.CurrentNodeId == SpinozaCauseNetworkState.SourceNodeId
            && journey.SeenThinkerIds.Contains("DESCARTES")
            && !journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
            && !journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
            && journey.DeclinedFixedActs.Contains(2);
    }

    public static bool TryCommitPending(SpinozaCauseNetworkState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (state is null || journey is null || graph is null || !state.IsUsable() || state.Closed) return false;
        var action = state.PendingAction;
        if (action == SpinozaCauseNetworkAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;
        if (!IsRouteConsistent(state, journey)) return false;

        if (action is SpinozaCauseNetworkAction.Intervene or SpinozaCauseNetworkAction.Reorder)
        {
            if (!graph.TryApply(journey, SpinozaCauseNetworkState.CompletionEdgeId)) return false;
        }
        else if (action is SpinozaCauseNetworkAction.Decline or SpinozaCauseNetworkAction.CorrectRecord)
        {
            if (!graph.TryDecline(journey, "Fixed", 2)) return false;
        }

        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(SpinozaCauseNetworkStage stage) => stage switch
    {
        SpinozaCauseNetworkStage.Invitation => "INVITATION",
        SpinozaCauseNetworkStage.Attribution => "ATTRIBUTION",
        SpinozaCauseNetworkStage.WeatherRevealed => "WEATHER_REVEALED",
        SpinozaCauseNetworkStage.Decision => "DECISION",
        SpinozaCauseNetworkStage.ResultIntervened => "RESULT_INTERVENED",
        SpinozaCauseNetworkStage.ResultReordered => "RESULT_REORDERED",
        SpinozaCauseNetworkStage.ResultCorrected => "RESULT_CORRECTED",
        SpinozaCauseNetworkStage.ResultDeclined => "RESULT_DECLINED",
        _ => ""
    };

    public static string ActionKey(SpinozaCauseNetworkAction action) => action switch
    {
        SpinozaCauseNetworkAction.Accept => "ACCEPT",
        SpinozaCauseNetworkAction.Decline => "DECLINE",
        SpinozaCauseNetworkAction.BlameEnemy => "BLAME_ENEMY",
        SpinozaCauseNetworkAction.TraceChains => "TRACE_CHAINS",
        SpinozaCauseNetworkAction.Continue => "CONTINUE",
        SpinozaCauseNetworkAction.Intervene => "INTERVENE",
        SpinozaCauseNetworkAction.Reorder => "REORDER",
        SpinozaCauseNetworkAction.CorrectRecord => "CORRECT_RECORD",
        SpinozaCauseNetworkAction.Leave => "LEAVE",
        _ => ""
    };

    public static string DescriptionKey(SpinozaCauseNetworkState state) =>
        state.Stage != SpinozaCauseNetworkStage.WeatherRevealed ? StageKey(state.Stage)
        : state.FirstAttribution switch
        {
            SpinozaAttribution.BlameEnemy => "WEATHER_REVEALED_BLAME_ENEMY",
            SpinozaAttribution.TraceChains => "WEATHER_REVEALED_TRACE_CHAINS",
            _ => ""
        };
}
