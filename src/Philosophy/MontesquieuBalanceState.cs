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
    /// <summary>The act this instance occupies: 1 for the question, 2 or 3 on a route.</summary>
    public int ActIndex { get; set; } = 1;
    /// <summary>The fixed edge this instance commits, chosen by the act it serves.</summary>
    public string EdgeId { get; set; } = CompletionEdgeId;
    public MontesquieuBalanceStage Stage { get; set; }
    public MontesquieuBalanceAction PendingAction { get; set; }
    public List<MontesquieuBalanceAction> ConfirmedActions { get; set; } = [];
    public MontesquieuBalanceStandard Standard { get; set; }
    public bool Inspecting { get; set; }
    public VoltaireToleranceState? Voltaire { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static MontesquieuBalanceState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static MontesquieuBalanceState CreateAt(string instanceId, int actIndex, string edgeId) => new()
    {
        InstanceId = instanceId,
        ActIndex = actIndex,
        EdgeId = edgeId,
        Voltaire = VoltaireToleranceState.Create($"{instanceId}:VOLTAIRE"),
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 1;
        ConfirmedActions ??= [];
        Voltaire?.NormalizeAfterLoad();
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || string.IsNullOrWhiteSpace(EdgeId)
            || Inspecting && (Voltaire is null || Voltaire.Closed)
            || Voltaire is not null && !Voltaire.IsUsable()
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
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex + 1)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex + 1)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(MontesquieuBalanceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        int declined = state.ActIndex + 1;
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted && !journey.DeclinedFixedActs.Contains(declined)
                && journey.CurrentNodeId == MontesquieuBalanceState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == state.ActIndex + 1
                && journey.CurrentNodeId == MontesquieuBalanceState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex + 1 && journey.DeclinedFixedActs.Contains(declined)
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
