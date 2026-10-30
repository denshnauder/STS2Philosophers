using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SmithSympathyStage { Invitation, ResultSympathy, ResultExchange, ResultArrangement }
internal enum SmithSympathyAction { None, SympathyOrder, ExchangeOrder, PreserveArrangement, Leave }
internal enum SmithSympathyStandard { None, Sympathy, Exchange, Arrangement }

/// <summary>
/// Smith's first-act room on the freedom-and-institutions line. The player
/// either adopts the sympathy route (sympathy as order or exchange as order) or
/// keeps Plato's arrangement and gives up the act's fixed slot.
/// </summary>
internal sealed class SmithSympathyState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "ADAM_SMITH__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_054";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public SmithSympathyStage Stage { get; set; }
    public SmithSympathyAction PendingAction { get; set; }
    public List<SmithSympathyAction> ConfirmedActions { get; set; } = [];
    public SmithSympathyStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static SmithSympathyState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != SmithSympathyAction.None
            || PendingAction != SmithSympathyAction.None
                && !SmithSympathyFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (SmithSympathyAction action in ConfirmedActions)
        {
            if (replay.Closed || !SmithSympathyFlow.Options(replay.Stage).Contains(action)) return false;
            SmithSympathyFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class SmithSympathyFlow
{
    public static IReadOnlyList<SmithSympathyAction> Options(SmithSympathyStage stage) => stage switch
    {
        SmithSympathyStage.Invitation =>
            [SmithSympathyAction.SympathyOrder, SmithSympathyAction.ExchangeOrder,
                SmithSympathyAction.PreserveArrangement],
        SmithSympathyStage.ResultSympathy or SmithSympathyStage.ResultExchange
            or SmithSympathyStage.ResultArrangement => [SmithSympathyAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(SmithSympathyState state, SmithSympathyAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SmithSympathyAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(SmithSympathyState state, SmithSympathyAction action)
    {
        switch (action)
        {
            case SmithSympathyAction.SympathyOrder:
                state.Standard = SmithSympathyStandard.Sympathy;
                state.EdgeCommitted = true;
                state.Stage = SmithSympathyStage.ResultSympathy;
                break;
            case SmithSympathyAction.ExchangeOrder:
                state.Standard = SmithSympathyStandard.Exchange;
                state.EdgeCommitted = true;
                state.Stage = SmithSympathyStage.ResultExchange;
                break;
            case SmithSympathyAction.PreserveArrangement:
                state.Standard = SmithSympathyStandard.Arrangement;
                state.FixedDeclined = true;
                state.Stage = SmithSympathyStage.ResultArrangement;
                break;
            case SmithSympathyAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SmithSympathyAction.None;
    }

    public static bool TryCommitPending(SmithSympathyState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == SmithSympathyAction.None) return false;
        SmithSympathyAction action = state.PendingAction;
        bool adopts = action is SmithSympathyAction.SympathyOrder or SmithSympathyAction.ExchangeOrder;
        bool declines = action == SmithSympathyAction.PreserveArrangement;
        if (adopts && !journey.CompletedEdgeIds.Contains(SmithSympathyState.CompletionEdgeId)
            && !graph.TryApply(journey, SmithSympathyState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(SmithSympathyState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(SmithSympathyState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == SmithSympathyState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == SmithSympathyState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == SmithSympathyState.SourceNodeId;
    }

    public static string DescriptionKey(SmithSympathyState state) => state.Stage switch
    {
        SmithSympathyStage.Invitation => "INVITATION",
        SmithSympathyStage.ResultSympathy => "RESULT_SEPARATION",
        SmithSympathyStage.ResultExchange => "RESULT_BALANCE",
        SmithSympathyStage.ResultArrangement => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(SmithSympathyStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(SmithSympathyAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
