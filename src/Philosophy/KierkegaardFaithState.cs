using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KierkegaardFaithStage { Invitation, ResultFaith, ResultEthical, ResultKept }
internal enum KierkegaardFaithAction { None, FaithLeap, EthicalUniversal, KeepSocrates, Leave }
internal enum KierkegaardFaithStandard { None, Faith, Ethical, Socratic }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class KierkegaardFaithState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE";
    public const string AdoptedNodeId = "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_059";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public KierkegaardFaithStage Stage { get; set; }
    public KierkegaardFaithAction PendingAction { get; set; }
    public List<KierkegaardFaithAction> ConfirmedActions { get; set; } = [];
    public KierkegaardFaithStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static KierkegaardFaithState Create(string instanceId) => CreateAt(instanceId, 2, CompletionEdgeId);

    public static KierkegaardFaithState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 1;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != KierkegaardFaithAction.None
            || PendingAction != KierkegaardFaithAction.None
                && !KierkegaardFaithFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (KierkegaardFaithAction action in ConfirmedActions)
        {
            if (replay.Closed || !KierkegaardFaithFlow.Options(replay.Stage).Contains(action)) return false;
            KierkegaardFaithFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class KierkegaardFaithFlow
{
    public static IReadOnlyList<KierkegaardFaithAction> Options(KierkegaardFaithStage stage) => stage switch
    {
        KierkegaardFaithStage.Invitation =>
            [KierkegaardFaithAction.FaithLeap, KierkegaardFaithAction.EthicalUniversal,
                KierkegaardFaithAction.KeepSocrates],
        KierkegaardFaithStage.ResultFaith or KierkegaardFaithStage.ResultEthical
            or KierkegaardFaithStage.ResultKept => [KierkegaardFaithAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(KierkegaardFaithState state, KierkegaardFaithAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KierkegaardFaithAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(KierkegaardFaithState state, KierkegaardFaithAction action)
    {
        switch (action)
        {
            case KierkegaardFaithAction.FaithLeap:
                state.Standard = KierkegaardFaithStandard.Faith;
                state.EdgeCommitted = true;
                state.Stage = KierkegaardFaithStage.ResultFaith;
                break;
            case KierkegaardFaithAction.EthicalUniversal:
                state.Standard = KierkegaardFaithStandard.Ethical;
                state.EdgeCommitted = true;
                state.Stage = KierkegaardFaithStage.ResultEthical;
                break;
            case KierkegaardFaithAction.KeepSocrates:
                state.Standard = KierkegaardFaithStandard.Socratic;
                state.FixedDeclined = true;
                state.Stage = KierkegaardFaithStage.ResultKept;
                break;
            case KierkegaardFaithAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = KierkegaardFaithAction.None;
    }

    public static bool TryCommitPending(KierkegaardFaithState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == KierkegaardFaithAction.None) return false;
        KierkegaardFaithAction action = state.PendingAction;
        bool adopts = action is KierkegaardFaithAction.FaithLeap or KierkegaardFaithAction.EthicalUniversal;
        bool declines = action == KierkegaardFaithAction.KeepSocrates;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KierkegaardFaithState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        int declined = state.ActIndex;
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted && !journey.DeclinedFixedActs.Contains(declined)
                && journey.CurrentNodeId == KierkegaardFaithState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                && journey.CurrentNodeId == KierkegaardFaithState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex && journey.DeclinedFixedActs.Contains(declined)
            && journey.CurrentNodeId == KierkegaardFaithState.SourceNodeId;
    }

    public static string DescriptionKey(KierkegaardFaithState state) => state.Stage switch
    {
        KierkegaardFaithStage.Invitation => "INVITATION",
        KierkegaardFaithStage.ResultFaith => "RESULT_SEPARATION",
        KierkegaardFaithStage.ResultEthical => "RESULT_BALANCE",
        KierkegaardFaithStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(KierkegaardFaithStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(KierkegaardFaithAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
