using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LockeCargoStage { Invitation, Evidence, ResultMemory, ResultSealed, ResultDuty, ResultKept }
internal enum LockeCargoAction { None, CheckMemory, CheckSignature, CheckMark, ReturnByMemory, SealAndWitness, ConfirmDutyOnly, KeepDescartes, InspectScreen, Leave }
internal enum LockeEvidence { None, Memory, Signature, Mark }
internal enum LockeJudgment { None, Identity, Custody }

internal sealed class LockeCargoIdentityState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "JOHN_LOCKE__SELF_AND_OTHER__CORE";
    public const string EdgeId = "WESTERN_EDGE_026";
    public const string SecondActEdgeId = "WESTERN_EDGE_091";

    /// <summary>The act this instance was presented in; one means the act-one question.</summary>
    public int ActIndex { get; set; } = 1;

    public string CompletionEdgeId => ActIndex == 2 ? SecondActEdgeId : EdgeId;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public LockeCargoStage Stage { get; set; }
    public LockeCargoAction PendingAction { get; set; }
    public List<LockeCargoAction> ConfirmedActions { get; set; } = [];
    public LockeEvidence Evidence { get; set; }
    public LockeJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Inspecting { get; set; }
    public BerkeleyPerceivedMarkState? Berkeley { get; set; }
    public bool Closed { get; set; }

    public static LockeCargoIdentityState Create(string instanceId) => CreateAt(instanceId, 1);

    public static LockeCargoIdentityState CreateAt(string instanceId, int actIndex) => new()
    {
        InstanceId = instanceId,
        Berkeley = BerkeleyPerceivedMarkState.Create($"{instanceId}:BERKELEY"),
        ActIndex = actIndex,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        Berkeley?.NormalizeAfterLoad();
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Evidence) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 5
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Inspecting && (Berkeley is null || Berkeley.Closed)
            || Berkeley is not null && !Berkeley.IsUsable()
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
            [LockeCargoAction.InspectScreen, LockeCargoAction.Leave],
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
            case LockeCargoAction.InspectScreen:
                state.Inspecting = true; break;
            case LockeCargoAction.Leave:
                state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LockeCargoAction.None;
    }

    public static bool TryApplyBerkeley(LockeCargoIdentityState state, BerkeleyMarkAction action)
    {
        if (!state.IsUsable() || !state.Inspecting || state.Berkeley is not { } berkeley
            || berkeley.Closed || !BerkeleyPerceivedMarkFlow.TryPrepare(berkeley, action)) return false;
        BerkeleyPerceivedMarkFlow.Apply(berkeley, action);
        if (berkeley.Closed) state.Inspecting = false;
        return state.IsUsable();
    }

    public static bool TryCommitPending(LockeCargoIdentityState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == LockeCargoAction.None) return false;
        LockeCargoAction action = state.PendingAction;
        bool adopts = action is LockeCargoAction.ReturnByMemory or LockeCargoAction.SealAndWitness or LockeCargoAction.ConfirmDutyOnly;
        bool declines = action == LockeCargoAction.KeepDescartes;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.CompletionEdgeId)
            && !graph.TryApply(journey, state.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(1)
            && !graph.TryDecline(journey, "Question", 1)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(LockeCargoIdentityState state, WesternJourneyState journey)
    {
        int act = state.ActIndex;
        if (!state.IsUsable() || journey.RetainedEnding || journey.LastFixedAct != act) return false;
        // Act one's question names the route; a room the route places later has no
        // question of its own to resolve.
        bool asked = act == 1 && journey.ResolvedQuestionActs.Contains(1);
        // Act one's question names the route, so the room may only open after it
        // is answered; a room the route places later is opened by the boundary.
        bool named = act != 1 || asked;
        bool adopted = journey.CompletedEdgeIds.Contains(state.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return !adopted && !asked && journey.CurrentNodeId == LockeCargoIdentityState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && named
                && journey.CurrentNodeId == LockeCargoIdentityState.AdoptedNodeId;
        return !adopted && named && journey.CurrentNodeId == LockeCargoIdentityState.SourceNodeId;
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
