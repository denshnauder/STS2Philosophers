using System.Text.Json;

namespace STS2Philosophers;

// ENC-128 deterministic page transitions.
internal static class NewtonTwoSitesFlow
{
    internal static IReadOnlyList<NewtonTwoSitesAction> Options(NewtonTwoSitesStage stage) => stage switch
    {
        NewtonTwoSitesStage.Invitation => new[] { NewtonTwoSitesAction.Accept, NewtonTwoSitesAction.Decline },
        NewtonTwoSitesStage.SiteChoice => new[] { NewtonTwoSitesAction.MeasureDrop, NewtonTwoSitesAction.MeasureTide },
        NewtonTwoSitesStage.FirstReading => new[] { NewtonTwoSitesAction.RecordError },
        NewtonTwoSitesStage.OtherSite => new[] { NewtonTwoSitesAction.RevealOther },
        NewtonTwoSitesStage.Decision => new[]
        {
            NewtonTwoSitesAction.ConditionRelation,
            NewtonTwoSitesAction.RecheckOtherSite,
            NewtonTwoSitesAction.KeepLocal
        },
        NewtonTwoSitesStage.ResultConditional
            or NewtonTwoSitesStage.ResultRechecked
            or NewtonTwoSitesStage.ResultLocal
            or NewtonTwoSitesStage.ResultDeclined => new[] { NewtonTwoSitesAction.Leave },
        _ => Array.Empty<NewtonTwoSitesAction>()
    };

    internal static bool TryPrepare(NewtonTwoSitesState state, NewtonTwoSitesAction action)
    {
        if (state is null) return false;
        if (!state.IsUsable()) return false;
        if (state.Closed) return false;
        if (state.PendingAction != NewtonTwoSitesAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;

        state.PendingAction = action;
        return true;
    }

    internal static void Apply(NewtonTwoSitesState state, NewtonTwoSitesAction action)
    {
        switch (state.Stage)
        {
            case NewtonTwoSitesStage.Invitation:
                if (action == NewtonTwoSitesAction.Accept)
                {
                    state.Stage = NewtonTwoSitesStage.SiteChoice;
                }
                else if (action == NewtonTwoSitesAction.Decline)
                {
                    state.FixedSlotResolved = true;
                    state.Stage = NewtonTwoSitesStage.ResultDeclined;
                }
                break;

            case NewtonTwoSitesStage.SiteChoice:
                if (action == NewtonTwoSitesAction.MeasureDrop || action == NewtonTwoSitesAction.MeasureTide)
                {
                    var site = action == NewtonTwoSitesAction.MeasureDrop ? NewtonSite.Drop : NewtonSite.Tide;
                    state.FirstSite = site;
                    state.CalibrationSite = site;
                    state.Stage = NewtonTwoSitesStage.FirstReading;
                }
                break;

            case NewtonTwoSitesStage.FirstReading:
                if (action == NewtonTwoSitesAction.RecordError)
                {
                    state.Stage = NewtonTwoSitesStage.OtherSite;
                }
                break;

            case NewtonTwoSitesStage.OtherSite:
                if (action == NewtonTwoSitesAction.RevealOther)
                {
                    state.Stage = NewtonTwoSitesStage.Decision;
                }
                break;

            case NewtonTwoSitesStage.Decision:
                if (action == NewtonTwoSitesAction.ConditionRelation)
                {
                    state.Disposition = NewtonDisposition.Conditional;
                    state.FixedSlotResolved = true;
                    state.EdgeCommitted = true;
                    state.Stage = NewtonTwoSitesStage.ResultConditional;
                }
                else if (action == NewtonTwoSitesAction.RecheckOtherSite)
                {
                    state.Disposition = NewtonDisposition.Rechecked;
                    state.CalibrationSite = OtherSite(state.FirstSite);
                    state.FixedSlotResolved = true;
                    state.EdgeCommitted = true;
                    state.Stage = NewtonTwoSitesStage.ResultRechecked;
                }
                else if (action == NewtonTwoSitesAction.KeepLocal)
                {
                    state.Disposition = NewtonDisposition.Local;
                    state.FixedSlotResolved = true;
                    state.Stage = NewtonTwoSitesStage.ResultLocal;
                }
                break;

            case NewtonTwoSitesStage.ResultConditional:
            case NewtonTwoSitesStage.ResultRechecked:
            case NewtonTwoSitesStage.ResultLocal:
            case NewtonTwoSitesStage.ResultDeclined:
                if (action == NewtonTwoSitesAction.Leave)
                {
                    state.Closed = true;
                }
                break;
        }

        state.ConfirmedActions.Add(action);
        state.PendingAction = NewtonTwoSitesAction.None;
    }

    private static NewtonSite OtherSite(NewtonSite site) => site switch
    {
        NewtonSite.Drop => NewtonSite.Tide,
        NewtonSite.Tide => NewtonSite.Drop,
        _ => NewtonSite.None
    };

    public static string StageKey(NewtonTwoSitesStage stage) => stage switch
    {
        NewtonTwoSitesStage.Invitation => "INVITATION",
        NewtonTwoSitesStage.SiteChoice => "SITE_CHOICE",
        NewtonTwoSitesStage.FirstReading => "FIRST_READING",
        NewtonTwoSitesStage.OtherSite => "OTHER_SITE",
        NewtonTwoSitesStage.Decision => "DECISION",
        NewtonTwoSitesStage.ResultConditional => "RESULT_CONDITIONAL",
        NewtonTwoSitesStage.ResultRechecked => "RESULT_RECHECKED",
        NewtonTwoSitesStage.ResultLocal => "RESULT_LOCAL",
        NewtonTwoSitesStage.ResultDeclined => "RESULT_DECLINED",
        _ => ""
    };

    public static string ActionKey(NewtonTwoSitesAction action) => action switch
    {
        NewtonTwoSitesAction.Accept => "ACCEPT",
        NewtonTwoSitesAction.Decline => "DECLINE",
        NewtonTwoSitesAction.MeasureDrop => "MEASURE_DROP",
        NewtonTwoSitesAction.MeasureTide => "MEASURE_TIDE",
        NewtonTwoSitesAction.RecordError => "RECORD_ERROR",
        NewtonTwoSitesAction.RevealOther => "REVEAL_OTHER",
        NewtonTwoSitesAction.ConditionRelation => "CONDITION_RELATION",
        NewtonTwoSitesAction.RecheckOtherSite => "RECHECK_OTHER_SITE",
        NewtonTwoSitesAction.KeepLocal => "KEEP_LOCAL",
        NewtonTwoSitesAction.Leave => "LEAVE",
        _ => ""
    };

    public static string DescriptionKey(NewtonTwoSitesState state)
    {
        if (state is null) return "";
        var key = StageKey(state.Stage);
        if (key.Length == 0) return "";
        switch (state.Stage)
        {
            case NewtonTwoSitesStage.FirstReading:
            case NewtonTwoSitesStage.OtherSite:
            case NewtonTwoSitesStage.Decision:
            case NewtonTwoSitesStage.ResultConditional:
            case NewtonTwoSitesStage.ResultRechecked:
            case NewtonTwoSitesStage.ResultLocal:
                if (state.FirstSite == NewtonSite.Drop) return key + "_DROP";
                if (state.FirstSite == NewtonSite.Tide) return key + "_TIDE";
                return "";
            default:
                return key;
        }
    }

    public static bool IsRouteConsistent(
        NewtonTwoSitesState state,
        WesternJourneyState journey,
        SextusOpposedAppearancesState sextus,
        DescartesMethodicReconstructionState descartes)
    {
        if (state is null || journey is null || sextus is null || descartes is null) return false;
        if (!state.IsUsable()) return false;
        if (string.IsNullOrWhiteSpace(state.SextusInstanceId) || string.IsNullOrWhiteSpace(state.DescartesInstanceId)) return false;
        if (state.SextusInstanceId != sextus.InstanceId) return false;
        if (state.DescartesInstanceId != descartes.InstanceId) return false;

        if (sextus.ConfirmedActions is null || sextus.ConfirmedActions.Count > 8) return false;
        if (descartes.ConfirmedActions is null || descartes.ConfirmedActions.Count > 8) return false;
        if (!sextus.Closed || !sextus.IsUsable()) return false;
        if (!descartes.Closed || !descartes.IsUsable()) return false;
        if (!descartes.MethodCompleted || !descartes.LimitedJudgmentRebuilt) return false;

        var sextusReplay = SextusOpposedAppearancesState.Create(sextus.InstanceId);
        foreach (var action in sextus.ConfirmedActions)
        {
            if (!SextusOpposedAppearancesFlow.TryPrepare(sextusReplay, action)) return false;
            if (!SextusOpposedAppearancesFlow.TryCommitPending(sextusReplay)) return false;
        }
        if (JsonSerializer.Serialize(sextusReplay) != JsonSerializer.Serialize(sextus)) return false;

        var descartesReplay = DescartesMethodicReconstructionState.Create(descartes.InstanceId);
        foreach (var action in descartes.ConfirmedActions)
        {
            if (!DescartesMethodicReconstructionFlow.TryPrepare(descartesReplay, action)) return false;
            if (!DescartesMethodicReconstructionFlow.TryCommitPending(descartesReplay)) return false;
        }
        if (JsonSerializer.Serialize(descartesReplay) != JsonSerializer.Serialize(descartes)) return false;

        if (journey.SeenThinkerIds is null || journey.ResolvedQuestionActs is null
            || journey.DeclinedFixedActs is null || journey.CompletedEdgeIds is null
            || !journey.SeenThinkerIds.Contains("SOCRATES")
            || !journey.ResolvedQuestionActs.Contains(1)
            || journey.ResolvedQuestionActs.Contains(2) || journey.ResolvedQuestionActs.Contains(3)
            || !journey.DeclinedFixedActs.Contains(2)) return false;

        bool edge = journey.CompletedEdgeIds.Contains(NewtonTwoSitesState.CompletionEdgeId);
        bool seen = journey.SeenThinkerIds.Contains("ISAAC_NEWTON");
        if (!state.FixedSlotResolved)
            return journey.CurrentNodeId == NewtonTwoSitesState.SourceNodeId && journey.LastFixedAct >= 2
                && !journey.DeclinedFixedActs.Contains(3) && !journey.RetainedEnding && !edge && !seen;
        if (journey.LastFixedAct != 3) return false;
        return state.EdgeCommitted
            ? journey.CurrentNodeId == NewtonTwoSitesState.AdoptedNodeId && edge && seen
                && !journey.DeclinedFixedActs.Contains(3) && !journey.RetainedEnding
            : journey.CurrentNodeId == NewtonTwoSitesState.SourceNodeId && !edge && !seen
                && journey.DeclinedFixedActs.Contains(3) && journey.RetainedEnding;
    }

    public static bool TryCommitPending(
        NewtonTwoSitesState state,
        WesternJourneyState journey,
        SextusOpposedAppearancesState sextus,
        DescartesMethodicReconstructionState descartes,
        WesternRouteGraph graph)
    {
        if (state is null || journey is null || sextus is null || descartes is null || graph is null) return false;
        if (!state.IsUsable()) return false;
        if (state.Closed) return false;
        var action = state.PendingAction;
        if (action == NewtonTwoSitesAction.None) return false;
        if (!Options(state.Stage).Contains(action)) return false;
        if (!IsRouteConsistent(state, journey, sextus, descartes)) return false;

        switch (action)
        {
            case NewtonTwoSitesAction.ConditionRelation:
            case NewtonTwoSitesAction.RecheckOtherSite:
                if (!graph.TryApply(journey, NewtonTwoSitesState.CompletionEdgeId)) return false;
                break;

            case NewtonTwoSitesAction.Decline:
            case NewtonTwoSitesAction.KeepLocal:
                var probe = CloneJourney(journey);
                if (!graph.TryDecline(probe, "Fixed", 3)) return false;
                if (!graph.TryRetainEnding(probe)) return false;
                if (!graph.TryDecline(journey, "Fixed", 3)) return false;
                if (!graph.TryRetainEnding(journey)) return false;
                break;

            default:
                break;
        }

        Apply(state, action);
        return IsRouteConsistent(state, journey, sextus, descartes);
    }

    private static WesternJourneyState CloneJourney(WesternJourneyState journey) =>
        JsonSerializer.Deserialize<WesternJourneyState>(JsonSerializer.Serialize(journey))!;
}
