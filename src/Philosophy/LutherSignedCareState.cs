using System.Text.Json.Serialization;

namespace STS2Philosophers;
internal enum LutherSignedCareStage { Invitation, Declaration, Need, Service, ResultSwitched, ResultKept, ResultDeclined }
internal enum LutherSignedCareAction { None, Accept, Decline, ReadText, CheckScope, SignAll, RefuseAndHelp, LimitAndHelp, KeepSignature, CarryWater, CarryBedding, Leave }
internal enum LutherDeclaration { None, Read, Checked, Signed }
internal enum LutherResponse { None, Refused, Limited, Kept }
internal enum LutherAid { None, Water, Bedding }

internal sealed class LutherSignedCareState : IJsonOnDeserialized
{
    public const string SourceNodeId = "AUGUSTINE_OF_HIPPO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string TargetNodeId = "MARTIN_LUTHER__SELF_AND_OTHER__CORE";
    public const string EdgeId = "WESTERN_EDGE_047";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string AugustineInstanceId { get; set; } = string.Empty;
    public LutherSignedCareStage Stage { get; set; } = (LutherSignedCareStage)(-1);
    public LutherSignedCareAction PendingAction { get; set; } = (LutherSignedCareAction)(-1);
    public List<LutherSignedCareAction> ConfirmedActions { get; set; } = null!;
    public LutherDeclaration Declaration { get; set; }
    public LutherResponse Response { get; set; }
    public LutherAid Aid { get; set; }
    public bool QuestionReserved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }
    public static LutherSignedCareState Create(string id, string augustineId) => new()
    {
        Version = 1, InstanceId = id, AugustineInstanceId = augustineId,
        Stage = LutherSignedCareStage.Invitation, PendingAction = LutherSignedCareAction.None,
        ConfirmedActions = [], Closed = false,
    };
    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty; AugustineInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();
    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(AugustineInstanceId)
            || !_closed.HasValue || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 5) return false;
        var replay = Create(InstanceId, AugustineInstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed || !LutherSignedCareFlow.Options(replay.Stage).Contains(action)) return false;
            LutherSignedCareFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Declaration == replay.Declaration && Response == replay.Response
            && Aid == replay.Aid && QuestionReserved == replay.QuestionReserved && EdgeCommitted == replay.EdgeCommitted
            && Closed == replay.Closed && (PendingAction == LutherSignedCareAction.None
                || !Closed && LutherSignedCareFlow.Options(Stage).Contains(PendingAction));
    }
}
internal static class LutherSignedCareFlow
{
    public static IReadOnlyList<LutherSignedCareAction> Options(LutherSignedCareStage stage) => stage switch
    {
        LutherSignedCareStage.Invitation => [LutherSignedCareAction.Accept, LutherSignedCareAction.Decline],
        LutherSignedCareStage.Declaration => [LutherSignedCareAction.ReadText, LutherSignedCareAction.CheckScope, LutherSignedCareAction.SignAll],
        LutherSignedCareStage.Need => [LutherSignedCareAction.RefuseAndHelp, LutherSignedCareAction.LimitAndHelp, LutherSignedCareAction.KeepSignature],
        LutherSignedCareStage.Service => [LutherSignedCareAction.CarryWater, LutherSignedCareAction.CarryBedding],
        >= LutherSignedCareStage.ResultSwitched and <= LutherSignedCareStage.ResultDeclined => [LutherSignedCareAction.Leave],
        _ => [],
    };
    internal static void Apply(LutherSignedCareState s, LutherSignedCareAction action)
    {
        switch (action)
        {
            case LutherSignedCareAction.Accept: s.QuestionReserved = true; s.Stage = LutherSignedCareStage.Declaration; break;
            case LutherSignedCareAction.Decline: s.Stage = LutherSignedCareStage.ResultDeclined; break;
            case LutherSignedCareAction.ReadText: s.Declaration = LutherDeclaration.Read; s.Stage = LutherSignedCareStage.Need; break;
            case LutherSignedCareAction.CheckScope: s.Declaration = LutherDeclaration.Checked; s.Stage = LutherSignedCareStage.Need; break;
            case LutherSignedCareAction.SignAll: s.Declaration = LutherDeclaration.Signed; s.Stage = LutherSignedCareStage.Need; break;
            case LutherSignedCareAction.RefuseAndHelp: s.Response = LutherResponse.Refused; s.Stage = LutherSignedCareStage.Service; break;
            case LutherSignedCareAction.LimitAndHelp: s.Response = LutherResponse.Limited; s.Stage = LutherSignedCareStage.Service; break;
            case LutherSignedCareAction.KeepSignature: s.Response = LutherResponse.Kept; s.Stage = LutherSignedCareStage.ResultKept; break;
            case LutherSignedCareAction.CarryWater: s.Aid = LutherAid.Water; s.EdgeCommitted = true; s.Stage = LutherSignedCareStage.ResultSwitched; break;
            case LutherSignedCareAction.CarryBedding: s.Aid = LutherAid.Bedding; s.EdgeCommitted = true; s.Stage = LutherSignedCareStage.ResultSwitched; break;
            case LutherSignedCareAction.Leave: s.Closed = true; break;
        }
        s.ConfirmedActions.Add(action); s.PendingAction = LutherSignedCareAction.None;
    }
    public static bool IsRouteConsistent(LutherSignedCareState s, WesternJourneyState journey, AugustinePromiseState predecessor)
    {
        if (!s.IsUsable() || !predecessor.IsUsable() || !predecessor.Closed || !predecessor.EdgeCommitted
            || s.AugustineInstanceId != predecessor.InstanceId || predecessor.Work is not (AugustineWork.Public or AugustineWork.Private)
            || !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_019") || !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_020")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_021") || !journey.ResolvedQuestionActs.Contains(1)
            || journey.DeclinedFixedActs.Contains(2) || !journey.SeenThinkerIds.Contains("AUGUSTINE_OF_HIPPO")
            || journey.LastFixedAct != 2 || journey.RetainedEnding) return false;
        return journey.ResolvedQuestionActs.Contains(2) == s.QuestionReserved
            && journey.CompletedEdgeIds.Contains(LutherSignedCareState.EdgeId) == s.EdgeCommitted
            && journey.CurrentNodeId == (s.EdgeCommitted ? LutherSignedCareState.TargetNodeId : LutherSignedCareState.SourceNodeId)
            && journey.SeenThinkerIds.Contains("MARTIN_LUTHER") == s.EdgeCommitted;
    }
    public static bool TryPrepare(LutherSignedCareState s, LutherSignedCareAction action)
    {
        if (!s.IsUsable() || s.Closed || s.PendingAction != LutherSignedCareAction.None || !Options(s.Stage).Contains(action)) return false;
        s.PendingAction = action; return true;
    }
    public static bool TryCommitPending(LutherSignedCareState s, WesternJourneyState journey, AugustinePromiseState predecessor, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(s, journey, predecessor) || s.Closed || s.PendingAction == LutherSignedCareAction.None) return false;
        var action = s.PendingAction;
        if (action == LutherSignedCareAction.Accept && !graph.TryReserveQuestion(journey, 2)) return false;
        if (action is LutherSignedCareAction.CarryWater or LutherSignedCareAction.CarryBedding
            && !graph.TryCompleteReservedQuestion(journey, LutherSignedCareState.EdgeId)) return false;
        Apply(s, action); return IsRouteConsistent(s, journey, predecessor);
    }
    public static string StageKey(LutherSignedCareStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(LutherSignedCareAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(LutherSignedCareState state) => state.Stage switch
    {
        LutherSignedCareStage.Need => $"NEED_{UpperSnake(state.Declaration.ToString())}",
        LutherSignedCareStage.Service => $"SERVICE_{UpperSnake(state.Response.ToString())}",
        LutherSignedCareStage.ResultSwitched => $"RESULT_SWITCHED_{UpperSnake(state.Aid.ToString())}",
        _ => StageKey(state.Stage),
    };
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
