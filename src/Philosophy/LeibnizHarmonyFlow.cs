namespace STS2Philosophers;

internal static class LeibnizHarmonyFlow
{
    public static IReadOnlyList<LeibnizHarmonyAction> Options(LeibnizHarmonyStage stage) => stage switch
    {
        LeibnizHarmonyStage.Invitation => new[] { LeibnizHarmonyAction.Accept, LeibnizHarmonyAction.Decline },
        LeibnizHarmonyStage.ClockChoice => new[] { LeibnizHarmonyAction.DelayOne, LeibnizHarmonyAction.ObserveBoth },
        LeibnizHarmonyStage.Revelation => new[] { LeibnizHarmonyAction.Continue },
        LeibnizHarmonyStage.Decision => new[] { LeibnizHarmonyAction.Independent, LeibnizHarmonyAction.Coordinated, LeibnizHarmonyAction.KeepSpinoza },
        LeibnizHarmonyStage.ResultIndependent => new[] { LeibnizHarmonyAction.Leave },
        LeibnizHarmonyStage.ResultCoordinated => new[] { LeibnizHarmonyAction.Leave },
        LeibnizHarmonyStage.ResultKept => new[] { LeibnizHarmonyAction.Leave },
        LeibnizHarmonyStage.ResultDeclined => new[] { LeibnizHarmonyAction.Leave },
        _ => Array.Empty<LeibnizHarmonyAction>()
    };

    public static bool TryPrepare(LeibnizHarmonyState state, LeibnizHarmonyAction action)
    {
        if (state is null || !state.IsUsable() || state.Closed) return false;
        if (state.PendingAction != LeibnizHarmonyAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    internal static void Apply(LeibnizHarmonyState state, LeibnizHarmonyAction action)
    {
        if (state.Closed || !Options(state.Stage).Contains(action)) return;
        switch (action)
        {
            case LeibnizHarmonyAction.Accept:
                state.Stage = LeibnizHarmonyStage.ClockChoice;
                break;
            case LeibnizHarmonyAction.Decline:
                state.Stage = LeibnizHarmonyStage.ResultDeclined;
                state.Disposition = LeibnizDisposition.None;
                state.FixedSlotResolved = true;
                break;
            case LeibnizHarmonyAction.DelayOne:
                state.Probe = LeibnizClockProbe.DelayOne;
                state.Stage = LeibnizHarmonyStage.Revelation;
                break;
            case LeibnizHarmonyAction.ObserveBoth:
                state.Probe = LeibnizClockProbe.ObserveBoth;
                state.Stage = LeibnizHarmonyStage.Revelation;
                break;
            case LeibnizHarmonyAction.Continue:
                state.Stage = LeibnizHarmonyStage.Decision;
                break;
            case LeibnizHarmonyAction.Independent:
                state.Stage = LeibnizHarmonyStage.ResultIndependent;
                state.Disposition = LeibnizDisposition.Independent;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case LeibnizHarmonyAction.Coordinated:
                state.Stage = LeibnizHarmonyStage.ResultCoordinated;
                state.Disposition = LeibnizDisposition.Coordinated;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case LeibnizHarmonyAction.KeepSpinoza:
                state.Stage = LeibnizHarmonyStage.ResultKept;
                state.Disposition = LeibnizDisposition.Kept;
                state.FixedSlotResolved = true;
                break;
            case LeibnizHarmonyAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LeibnizHarmonyAction.None;
    }

    public static bool IsRouteConsistent(LeibnizHarmonyState state, WesternJourneyState journey, SpinozaCauseNetworkState spinoza)
    {
        if (state is null || journey is null || spinoza is null || !state.IsUsable()) return false;
        if (!spinoza.IsUsable() || !spinoza.Closed || !spinoza.EdgeCommitted) return false;
        if (spinoza.Disposition is not (SpinozaDisposition.Intervened or SpinozaDisposition.Reordered)) return false;
        if (!string.Equals(state.SpinozaInstanceId, spinoza.InstanceId, StringComparison.Ordinal)) return false;
        if (journey.SeenThinkerIds is null || journey.ResolvedQuestionActs is null
            || journey.DeclinedFixedActs is null || journey.CompletedEdgeIds is null) return false;
        if (!journey.SeenThinkerIds.Contains("DESCARTES")) return false;
        if (!journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")) return false;
        if (!journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.PredecessorEdgeId)) return false;
        if (journey.CompletedEdgeIds.Contains("WESTERN_EDGE_024")) return false;
        if (journey.ResolvedQuestionActs.Contains(2) || journey.ResolvedQuestionActs.Contains(3)) return false;

        if (!state.FixedSlotResolved)
        {
            return journey.CurrentNodeId == LeibnizHarmonyState.SourceNodeId
                && journey.LastFixedAct >= 2
                && !journey.DeclinedFixedActs.Contains(3)
                && !journey.RetainedEnding
                && !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
                && !journey.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ");
        }

        if (journey.LastFixedAct != 3) return false;
        if (state.EdgeCommitted)
        {
            return journey.CurrentNodeId == LeibnizHarmonyState.AdoptedNodeId
                && journey.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ")
                && journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
                && !journey.DeclinedFixedActs.Contains(3)
                && !journey.RetainedEnding;
        }
        return journey.CurrentNodeId == LeibnizHarmonyState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(3)
            && journey.RetainedEnding
            && !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
            && !journey.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ");
    }

    public static bool TryCommitPending(LeibnizHarmonyState state, WesternJourneyState journey, SpinozaCauseNetworkState spinoza, WesternRouteGraph graph)
    {
        if (state is null || journey is null || spinoza is null || graph is null) return false;
        if (!state.IsUsable() || state.Closed) return false;
        var action = state.PendingAction;
        if (action == LeibnizHarmonyAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;
        if (!IsRouteConsistent(state, journey, spinoza)) return false;

        // Clone before mutating so an invalid ending cannot partially change the journey.
        WesternJourneyState draft = CloneJourney(journey);
        if (action is LeibnizHarmonyAction.Independent or LeibnizHarmonyAction.Coordinated)
        {
            if (!graph.TryApply(draft, LeibnizHarmonyState.CompletionEdgeId)) return false;
        }
        else if (action is LeibnizHarmonyAction.Decline or LeibnizHarmonyAction.KeepSpinoza)
        {
            if (!graph.TryDecline(draft, "Fixed", 3)) return false;
            if (!graph.TryRetainEnding(draft)) return false;
        }

        LeibnizHarmonyState probe = CloneState(state);
        Apply(probe, action);
        if (!IsRouteConsistent(probe, draft, spinoza)) return false;

        CopyJourney(draft, journey);
        Apply(state, action);
        return IsRouteConsistent(state, journey, spinoza);
    }

    public static string StageKey(LeibnizHarmonyStage stage) => stage switch
    {
        LeibnizHarmonyStage.Invitation => "INVITATION",
        LeibnizHarmonyStage.ClockChoice => "CLOCK_CHOICE",
        LeibnizHarmonyStage.Revelation => "REVELATION",
        LeibnizHarmonyStage.Decision => "DECISION",
        LeibnizHarmonyStage.ResultIndependent => "RESULT_INDEPENDENT",
        LeibnizHarmonyStage.ResultCoordinated => "RESULT_COORDINATED",
        LeibnizHarmonyStage.ResultKept => "RESULT_KEPT",
        LeibnizHarmonyStage.ResultDeclined => "RESULT_DECLINED",
        _ => ""
    };

    public static string ActionKey(LeibnizHarmonyAction action) => action switch
    {
        LeibnizHarmonyAction.Accept => "ACCEPT",
        LeibnizHarmonyAction.Decline => "DECLINE",
        LeibnizHarmonyAction.DelayOne => "DELAY_ONE",
        LeibnizHarmonyAction.ObserveBoth => "OBSERVE_BOTH",
        LeibnizHarmonyAction.Continue => "CONTINUE",
        LeibnizHarmonyAction.Independent => "INDEPENDENT",
        LeibnizHarmonyAction.Coordinated => "COORDINATED",
        LeibnizHarmonyAction.KeepSpinoza => "KEEP_SPINOZA",
        LeibnizHarmonyAction.Leave => "LEAVE",
        _ => ""
    };

    public static string DescriptionKey(LeibnizHarmonyState state) =>
        state.Stage != LeibnizHarmonyStage.Revelation ? StageKey(state.Stage)
        : state.Probe switch
        {
            LeibnizClockProbe.DelayOne => "REVELATION_DELAY_ONE",
            LeibnizClockProbe.ObserveBoth => "REVELATION_OBSERVE_BOTH",
            _ => ""
        };

    private static LeibnizHarmonyState CloneState(LeibnizHarmonyState state) => new()
    {
        Version = state.Version,
        InstanceId = state.InstanceId,
        SpinozaInstanceId = state.SpinozaInstanceId,
        Stage = state.Stage,
        PendingAction = state.PendingAction,
        ConfirmedActions = new List<LeibnizHarmonyAction>(state.ConfirmedActions),
        Probe = state.Probe,
        Disposition = state.Disposition,
        FixedSlotResolved = state.FixedSlotResolved,
        EdgeCommitted = state.EdgeCommitted,
        Closed = state.Closed
    };

    private static WesternJourneyState CloneJourney(WesternJourneyState state) => new()
    {
        CurrentNodeId = state.CurrentNodeId,
        LastFixedAct = state.LastFixedAct,
        SeenThinkerIds = new(state.SeenThinkerIds, StringComparer.Ordinal),
        CompletedEdgeIds = new(state.CompletedEdgeIds, StringComparer.Ordinal),
        ResolvedQuestionActs = new(state.ResolvedQuestionActs),
        CandidateWindows = state.CandidateWindows.ToDictionary(
            pair => pair.Key, pair => new List<string>(pair.Value), StringComparer.Ordinal),
        DeclinedFixedActs = new(state.DeclinedFixedActs),
        RetainedEnding = state.RetainedEnding
    };

    private static void CopyJourney(WesternJourneyState from, WesternJourneyState to)
    {
        to.CurrentNodeId = from.CurrentNodeId;
        to.LastFixedAct = from.LastFixedAct;
        to.SeenThinkerIds = new(from.SeenThinkerIds, StringComparer.Ordinal);
        to.CompletedEdgeIds = new(from.CompletedEdgeIds, StringComparer.Ordinal);
        to.ResolvedQuestionActs = new(from.ResolvedQuestionActs);
        to.CandidateWindows = from.CandidateWindows.ToDictionary(
            pair => pair.Key, pair => new List<string>(pair.Value), StringComparer.Ordinal);
        to.DeclinedFixedActs = new(from.DeclinedFixedActs);
        to.RetainedEnding = from.RetainedEnding;
    }
}
