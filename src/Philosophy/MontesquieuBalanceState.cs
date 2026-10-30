using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MontesquieuBalanceStage { Invitation, ResultSeparation, ResultBalance, ResultPrivilege }
internal enum MontesquieuBalanceAction { None, SeparatePowers, BalanceFactions, PreservePrivilege, Leave }
internal enum MontesquieuBalanceStandard { None, Separation, Balance, Privilege }

/// <summary>
/// Montesquieu's first-act room on the freedom-and-institutions line. The player
/// either adopts the balance route (separate powers or mixed factions) or keeps
/// Plato's arrangement and gives up the act's fixed slot.
/// </summary>
internal sealed class MontesquieuBalanceState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "MONTESQUIEU__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_051";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public MontesquieuBalanceStage Stage { get; set; }
    public MontesquieuBalanceAction PendingAction { get; set; }
    public List<MontesquieuBalanceAction> ConfirmedActions { get; set; } = [];
    public MontesquieuBalanceStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static MontesquieuBalanceState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != MontesquieuBalanceAction.None
            || PendingAction != MontesquieuBalanceAction.None
                && !MontesquieuBalanceFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (MontesquieuBalanceAction action in ConfirmedActions)
        {
            if (replay.Closed || !MontesquieuBalanceFlow.Options(replay.Stage).Contains(action)) return false;
            MontesquieuBalanceFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class MontesquieuBalanceFlow
{
    public static IReadOnlyList<MontesquieuBalanceAction> Options(MontesquieuBalanceStage stage) => stage switch
    {
        MontesquieuBalanceStage.Invitation =>
            [MontesquieuBalanceAction.SeparatePowers, MontesquieuBalanceAction.BalanceFactions,
                MontesquieuBalanceAction.PreservePrivilege],
        MontesquieuBalanceStage.ResultSeparation or MontesquieuBalanceStage.ResultBalance
            or MontesquieuBalanceStage.ResultPrivilege => [MontesquieuBalanceAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(MontesquieuBalanceState state, MontesquieuBalanceAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MontesquieuBalanceAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(MontesquieuBalanceState state, MontesquieuBalanceAction action)
    {
        switch (action)
        {
            case MontesquieuBalanceAction.SeparatePowers:
                state.Standard = MontesquieuBalanceStandard.Separation;
                state.EdgeCommitted = true;
                state.Stage = MontesquieuBalanceStage.ResultSeparation;
                break;
            case MontesquieuBalanceAction.BalanceFactions:
                state.Standard = MontesquieuBalanceStandard.Balance;
                state.EdgeCommitted = true;
                state.Stage = MontesquieuBalanceStage.ResultBalance;
                break;
            case MontesquieuBalanceAction.PreservePrivilege:
                state.Standard = MontesquieuBalanceStandard.Privilege;
                state.FixedDeclined = true;
                state.Stage = MontesquieuBalanceStage.ResultPrivilege;
                break;
            case MontesquieuBalanceAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = MontesquieuBalanceAction.None;
    }

    public static bool TryCommitPending(MontesquieuBalanceState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == MontesquieuBalanceAction.None) return false;
        MontesquieuBalanceAction action = state.PendingAction;
        bool adopts = action is MontesquieuBalanceAction.SeparatePowers or MontesquieuBalanceAction.BalanceFactions;
        bool declines = action == MontesquieuBalanceAction.PreservePrivilege;
        if (adopts && !journey.CompletedEdgeIds.Contains(MontesquieuBalanceState.CompletionEdgeId)
            && !graph.TryApply(journey, MontesquieuBalanceState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(MontesquieuBalanceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(MontesquieuBalanceState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == MontesquieuBalanceState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == MontesquieuBalanceState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == MontesquieuBalanceState.SourceNodeId;
    }

    public static string DescriptionKey(MontesquieuBalanceState state) => state.Stage switch
    {
        MontesquieuBalanceStage.Invitation => "INVITATION",
        MontesquieuBalanceStage.ResultSeparation => "RESULT_SEPARATION",
        MontesquieuBalanceStage.ResultBalance => "RESULT_BALANCE",
        MontesquieuBalanceStage.ResultPrivilege => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(MontesquieuBalanceStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MontesquieuBalanceAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
