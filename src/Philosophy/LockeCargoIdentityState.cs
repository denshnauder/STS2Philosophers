using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LockeCargoStage { Invitation, Evidence, ResultMemory, ResultSealed, ResultDuty, ResultKept }
internal enum LockeCargoAction { None, CheckMemory, CheckSignature, CheckMark, ReturnByMemory, SealAndWitness, ConfirmDutyOnly, KeepDescartes, Leave }
internal enum LockeEvidence { None, Memory, Signature, Mark }
internal enum LockeJudgment { None, Identity, Custody }

internal sealed class LockeCargoIdentityState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "JOHN_LOCKE__SELF_AND_OTHER__CORE";
    public const string EdgeId = "WESTERN_EDGE_026";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public LockeCargoStage Stage { get; set; }
    public LockeCargoAction PendingAction { get; set; }
    public List<LockeCargoAction> ConfirmedActions { get; set; } = [];
    public LockeEvidence Evidence { get; set; }
    public LockeJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static LockeCargoIdentityState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Evidence) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 5
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != LockeCargoAction.None
            || PendingAction != LockeCargoAction.None
                && !LockeCargoIdentityFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (LockeCargoAction action in ConfirmedActions)
        {
            if (replay.Closed || !LockeCargoIdentityFlow.Options(replay.Stage).Contains(action)
                || action == LockeCargoAction.ReturnByMemory && replay.Evidence != LockeEvidence.Memory) return false;
            LockeCargoIdentityFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Evidence == replay.Evidence && Judgment == replay.Judgment
            && EdgeCommitted == replay.EdgeCommitted && QuestionDeclined == replay.QuestionDeclined
            && Closed == replay.Closed;
    }
}

internal static class LockeCargoIdentityFlow
{
    public static IReadOnlyList<LockeCargoAction> Options(LockeCargoStage stage) => stage switch
    {
        LockeCargoStage.Invitation =>
            [LockeCargoAction.CheckMemory, LockeCargoAction.CheckSignature, LockeCargoAction.CheckMark, LockeCargoAction.KeepDescartes],
        LockeCargoStage.Evidence =>
            [LockeCargoAction.ReturnByMemory, LockeCargoAction.SealAndWitness, LockeCargoAction.ConfirmDutyOnly],
        LockeCargoStage.ResultMemory or LockeCargoStage.ResultSealed or LockeCargoStage.ResultDuty or LockeCargoStage.ResultKept =>
            [LockeCargoAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(LockeCargoIdentityState state, LockeCargoAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != LockeCargoAction.None
            || !Options(state.Stage).Contains(action)
            || action == LockeCargoAction.ReturnByMemory && state.Evidence != LockeEvidence.Memory) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(LockeCargoIdentityState state, LockeCargoAction action)
    {
        switch (action)
        {
            case LockeCargoAction.CheckMemory: state.Evidence = LockeEvidence.Memory; state.Stage = LockeCargoStage.Evidence; break;
            case LockeCargoAction.CheckSignature: state.Evidence = LockeEvidence.Signature; state.Stage = LockeCargoStage.Evidence; break;
            case LockeCargoAction.CheckMark: state.Evidence = LockeEvidence.Mark; state.Stage = LockeCargoStage.Evidence; break;
            case LockeCargoAction.ReturnByMemory:
                state.Judgment = LockeJudgment.Identity; state.Stage = LockeCargoStage.ResultMemory; state.EdgeCommitted = true; break;
            case LockeCargoAction.SealAndWitness:
                state.Judgment = LockeJudgment.Identity; state.Stage = LockeCargoStage.ResultSealed; state.EdgeCommitted = true; break;
            case LockeCargoAction.ConfirmDutyOnly:
                state.Judgment = LockeJudgment.Custody; state.Stage = LockeCargoStage.ResultDuty; state.EdgeCommitted = true; break;
            case LockeCargoAction.KeepDescartes:
                state.QuestionDeclined = true; state.Stage = LockeCargoStage.ResultKept; break;
            case LockeCargoAction.Leave:
                state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LockeCargoAction.None;
    }

    public static bool TryCommitPending(LockeCargoIdentityState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == LockeCargoAction.None) return false;
        LockeCargoAction action = state.PendingAction;
        bool adopts = action is LockeCargoAction.ReturnByMemory or LockeCargoAction.SealAndWitness or LockeCargoAction.ConfirmDutyOnly;
        bool declines = action == LockeCargoAction.KeepDescartes;
        if (adopts && !journey.CompletedEdgeIds.Contains(LockeCargoIdentityState.EdgeId)
            && !graph.TryApply(journey, LockeCargoIdentityState.EdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(1)
            && !graph.TryDecline(journey, "Question", 1)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(LockeCargoIdentityState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding || journey.LastFixedAct != 1) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(LockeCargoIdentityState.EdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return !adopted && !journey.ResolvedQuestionActs.Contains(1)
                && journey.CurrentNodeId == LockeCargoIdentityState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted
                && journey.ResolvedQuestionActs.Contains(1)
                && journey.CurrentNodeId == LockeCargoIdentityState.AdoptedNodeId;
        return !adopted && journey.ResolvedQuestionActs.Contains(1)
            && journey.CurrentNodeId == LockeCargoIdentityState.SourceNodeId;
    }

    public static string DescriptionKey(LockeCargoIdentityState state) => state.Stage switch
    {
        LockeCargoStage.Invitation => "INVITATION",
        LockeCargoStage.Evidence => state.Evidence switch
        {
            LockeEvidence.Memory => "EVIDENCE_MEMORY",
            LockeEvidence.Signature => "EVIDENCE_SIGNATURE",
            _ => "EVIDENCE_MARK",
        },
        LockeCargoStage.ResultMemory => "RESULT_MEMORY",
        LockeCargoStage.ResultSealed => "RESULT_SEALED",
        LockeCargoStage.ResultDuty => "RESULT_DUTY",
        LockeCargoStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(LockeCargoStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(LockeCargoAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
