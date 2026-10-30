using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum OckhamNamesStage { Invitation, Audit, Discrepancy, ResultDeclined, ResultAnnotated, ResultNamed, ResultKept }
internal enum OckhamNamesAction { None, Accept, Decline, CheckWorkers, CheckConcept, CheckInscription, Annotate, UseNamedList, KeepRule, Close }
internal enum OckhamNamesAudit { None, Workers, Concept, Inscription }
internal enum OckhamNamesDisposition { None, Annotated, Named, Kept }

internal sealed class OckhamNamesInquiryState : IJsonOnDeserialized
{
    public const int QuestionAct = 2;
    public const string RequiredNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string GorgiasInstanceId { get; set; } = string.Empty;
    public OckhamNamesStage Stage { get; set; } = (OckhamNamesStage)(-1);
    public OckhamNamesAction PendingAction { get; set; } = (OckhamNamesAction)(-1);
    public List<OckhamNamesAction> ConfirmedActions { get; set; } = null!;
    public OckhamNamesAudit Audit { get; set; }
    public OckhamNamesDisposition Disposition { get; set; }
    public bool SecondFixedDeclined { get; set; }
    public bool QuestionReserved { get; set; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static OckhamNamesInquiryState Create(string instanceId, string gorgiasInstanceId) => new()
    {
        Version = 1, InstanceId = instanceId, GorgiasInstanceId = gorgiasInstanceId,
        Stage = OckhamNamesStage.Invitation, PendingAction = OckhamNamesAction.None,
        ConfirmedActions = [], Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        GorgiasInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId)
            || string.IsNullOrWhiteSpace(GorgiasInstanceId) || !_closed.HasValue
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4) return false;
        OckhamNamesInquiryState replay = Create(InstanceId, GorgiasInstanceId);
        foreach (OckhamNamesAction action in ConfirmedActions)
        {
            if (replay.Closed || !OckhamNamesInquiryFlow.Options(replay.Stage).Contains(action)) return false;
            OckhamNamesInquiryFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Closed == replay.Closed && Audit == replay.Audit
            && Disposition == replay.Disposition && SecondFixedDeclined == replay.SecondFixedDeclined
            && QuestionReserved == replay.QuestionReserved
            && (PendingAction == OckhamNamesAction.None
                || !Closed && OckhamNamesInquiryFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class OckhamNamesInquiryFlow
{
    public static IReadOnlyList<OckhamNamesAction> Options(OckhamNamesStage stage) => stage switch
    {
        OckhamNamesStage.Invitation => [OckhamNamesAction.Accept, OckhamNamesAction.Decline],
        OckhamNamesStage.Audit => [OckhamNamesAction.CheckWorkers, OckhamNamesAction.CheckConcept, OckhamNamesAction.CheckInscription],
        OckhamNamesStage.Discrepancy => [OckhamNamesAction.Annotate, OckhamNamesAction.UseNamedList, OckhamNamesAction.KeepRule],
        >= OckhamNamesStage.ResultDeclined and <= OckhamNamesStage.ResultKept => [OckhamNamesAction.Close],
        _ => [],
    };

    internal static void Apply(OckhamNamesInquiryState state, OckhamNamesAction action)
    {
        switch (action)
        {
            case OckhamNamesAction.Accept:
                state.SecondFixedDeclined = state.QuestionReserved = true;
                state.Stage = OckhamNamesStage.Audit; break;
            case OckhamNamesAction.Decline: state.Stage = OckhamNamesStage.ResultDeclined; break;
            case OckhamNamesAction.CheckWorkers:
                state.Audit = OckhamNamesAudit.Workers; state.Stage = OckhamNamesStage.Discrepancy; break;
            case OckhamNamesAction.CheckConcept:
                state.Audit = OckhamNamesAudit.Concept; state.Stage = OckhamNamesStage.Discrepancy; break;
            case OckhamNamesAction.CheckInscription:
                state.Audit = OckhamNamesAudit.Inscription; state.Stage = OckhamNamesStage.Discrepancy; break;
            case OckhamNamesAction.Annotate:
                state.Disposition = OckhamNamesDisposition.Annotated; state.Stage = OckhamNamesStage.ResultAnnotated; break;
            case OckhamNamesAction.UseNamedList:
                state.Disposition = OckhamNamesDisposition.Named; state.Stage = OckhamNamesStage.ResultNamed; break;
            case OckhamNamesAction.KeepRule:
                state.Disposition = OckhamNamesDisposition.Kept; state.Stage = OckhamNamesStage.ResultKept; break;
            case OckhamNamesAction.Close: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = OckhamNamesAction.None;
    }

    public static bool IsRouteConsistent(OckhamNamesInquiryState state,
        WesternJourneyState journey, GorgiasSpeechInquiryState gorgias) =>
        state.IsUsable() && gorgias.IsUsable() && gorgias.Closed
        && state.GorgiasInstanceId == gorgias.InstanceId
        && journey.CurrentNodeId == OckhamNamesInquiryState.RequiredNodeId
        && journey.SeenThinkerIds.Contains("ARISTOTLE")
        && journey.ResolvedQuestionActs.Contains(1) && !journey.RetainedEnding
        && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
        && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")
        && (state.SecondFixedDeclined
            ? journey.LastFixedAct is 2 or 3 && journey.DeclinedFixedActs.Contains(2)
                && journey.ResolvedQuestionActs.Contains(2)
            : journey.LastFixedAct is 1 or 2 && !journey.DeclinedFixedActs.Contains(2)
                && !journey.ResolvedQuestionActs.Contains(2));

    public static bool TryPrepare(OckhamNamesInquiryState state, OckhamNamesAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != OckhamNamesAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(OckhamNamesInquiryState state, WesternJourneyState journey,
        GorgiasSpeechInquiryState gorgias, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey, gorgias) || state.Closed
            || state.PendingAction == OckhamNamesAction.None) return false;
        OckhamNamesAction action = state.PendingAction;
        if (action == OckhamNamesAction.Accept
            && (!graph.TryDecline(journey, "Fixed", 2) || !graph.TryReserveQuestion(journey, 2))) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey, gorgias);
    }

    public static string StageKey(OckhamNamesStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(OckhamNamesAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(OckhamNamesInquiryState state) =>
        state.Stage == OckhamNamesStage.Discrepancy
            ? $"DISCREPANCY_{UpperSnake(state.Audit.ToString())}" : StageKey(state.Stage);
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
