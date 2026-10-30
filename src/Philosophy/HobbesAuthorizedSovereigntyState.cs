using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HobbesSovereigntyStage
{
    Invitation,
    Announcement,
    ResultKept,
    ResultAccepted,
    ResultException,
    ResultWithdrawn,
}

internal enum HobbesSovereigntyAction
{
    None,
    DelegateNightWatch,
    DelegatePassageList,
    KeepBoth,
    AcceptPublicRule,
    RequestException,
    WithdrawCooperation,
    Leave,
}

internal enum HobbesDelegatedScope
{
    None,
    NightWatch,
    PassageList,
}

internal sealed class HobbesAuthorizedSovereigntyState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "THOMAS_HOBBES__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_017";
    public const string CompletionEdgeId = "WESTERN_EDGE_018";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HobbesSovereigntyStage Stage { get; set; }
    public HobbesSovereigntyAction PendingAction { get; set; }
    public List<HobbesSovereigntyAction> ConfirmedActions { get; set; } = [];
    public HobbesDelegatedScope Scope { get; set; }
    public bool RuleRevised { get; set; }
    public bool PublicProtectionLost { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static HobbesAuthorizedSovereigntyState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = HobbesSovereigntyStage.Invitation,
    };

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
            || !Enum.IsDefined(Scope) || ConfirmedActions is null
            || ConfirmedActions.Count > 4 || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != HobbesSovereigntyAction.None
            || PendingAction != HobbesSovereigntyAction.None
                && !HobbesAuthorizedSovereigntyFlow.Options(Stage).Contains(PendingAction))
        {
            return false;
        }

        var replay = Create(InstanceId);
        foreach (HobbesSovereigntyAction action in ConfirmedActions)
        {
            if (replay.Closed || !HobbesAuthorizedSovereigntyFlow.Options(replay.Stage).Contains(action)) return false;
            HobbesAuthorizedSovereigntyFlow.Apply(replay, action);
        }

        return Stage == replay.Stage && Scope == replay.Scope && RuleRevised == replay.RuleRevised
            && PublicProtectionLost == replay.PublicProtectionLost
            && FixedSlotResolved == replay.FixedSlotResolved
            && EdgeCommitted == replay.EdgeCommitted && Closed == replay.Closed;
    }
}

internal static class HobbesAuthorizedSovereigntyFlow
{
    public static IReadOnlyList<HobbesSovereigntyAction> Options(HobbesSovereigntyStage stage) => stage switch
    {
        HobbesSovereigntyStage.Invitation =>
            [HobbesSovereigntyAction.DelegateNightWatch, HobbesSovereigntyAction.DelegatePassageList, HobbesSovereigntyAction.KeepBoth],
        HobbesSovereigntyStage.Announcement =>
            [HobbesSovereigntyAction.AcceptPublicRule, HobbesSovereigntyAction.RequestException, HobbesSovereigntyAction.WithdrawCooperation],
        HobbesSovereigntyStage.ResultKept or HobbesSovereigntyStage.ResultAccepted
            or HobbesSovereigntyStage.ResultException or HobbesSovereigntyStage.ResultWithdrawn =>
            [HobbesSovereigntyAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HobbesAuthorizedSovereigntyState state, HobbesSovereigntyAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HobbesSovereigntyAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HobbesAuthorizedSovereigntyState state, HobbesSovereigntyAction action)
    {
        switch (action)
        {
            case HobbesSovereigntyAction.DelegateNightWatch:
                state.Scope = HobbesDelegatedScope.NightWatch;
                state.Stage = HobbesSovereigntyStage.Announcement;
                break;
            case HobbesSovereigntyAction.DelegatePassageList:
                state.Scope = HobbesDelegatedScope.PassageList;
                state.Stage = HobbesSovereigntyStage.Announcement;
                break;
            case HobbesSovereigntyAction.KeepBoth:
                state.Stage = HobbesSovereigntyStage.ResultKept;
                state.FixedSlotResolved = true;
                break;
            case HobbesSovereigntyAction.AcceptPublicRule:
                state.Stage = HobbesSovereigntyStage.ResultAccepted;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case HobbesSovereigntyAction.RequestException:
                state.Stage = HobbesSovereigntyStage.ResultException;
                state.RuleRevised = true;
                state.FixedSlotResolved = true;
                state.EdgeCommitted = true;
                break;
            case HobbesSovereigntyAction.WithdrawCooperation:
                state.Stage = HobbesSovereigntyStage.ResultWithdrawn;
                state.PublicProtectionLost = true;
                state.FixedSlotResolved = true;
                break;
            case HobbesSovereigntyAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HobbesSovereigntyAction.None;
    }

    public static bool TryCommitPending(HobbesAuthorizedSovereigntyState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HobbesSovereigntyAction.None) return false;
        HobbesSovereigntyAction action = state.PendingAction;
        bool adopts = action is HobbesSovereigntyAction.AcceptPublicRule or HobbesSovereigntyAction.RequestException;
        bool declines = action is HobbesSovereigntyAction.KeepBoth or HobbesSovereigntyAction.WithdrawCooperation;
        if (adopts && !journey.CompletedEdgeIds.Contains(HobbesAuthorizedSovereigntyState.CompletionEdgeId)
            && !graph.TryApply(journey, HobbesAuthorizedSovereigntyState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HobbesAuthorizedSovereigntyState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_016")
            || !journey.CompletedEdgeIds.Contains(HobbesAuthorizedSovereigntyState.RequiredEdgeId)) return false;

        bool adopted = journey.CompletedEdgeIds.Contains(HobbesAuthorizedSovereigntyState.CompletionEdgeId);
        if (!state.FixedSlotResolved)
        {
            return journey.LastFixedAct >= 2
                && journey.CurrentNodeId == HobbesAuthorizedSovereigntyState.SourceNodeId
                && !adopted && !journey.DeclinedFixedActs.Contains(3);
        }

        if (adopted)
        {
            return state.EdgeCommitted && !state.PublicProtectionLost
                && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HobbesAuthorizedSovereigntyState.AdoptedNodeId;
        }

        return !state.EdgeCommitted && state.PublicProtectionLost == (state.Stage == HobbesSovereigntyStage.ResultWithdrawn)
            && journey.LastFixedAct == 3
            && journey.CurrentNodeId == HobbesAuthorizedSovereigntyState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(3);
    }

    public static string DescriptionKey(HobbesAuthorizedSovereigntyState state) => state.Stage switch
    {
        HobbesSovereigntyStage.Invitation => "INVITATION",
        HobbesSovereigntyStage.Announcement => state.Scope == HobbesDelegatedScope.PassageList
            ? "ANNOUNCEMENT_PASSAGE_LIST"
            : "ANNOUNCEMENT_NIGHT_WATCH",
        HobbesSovereigntyStage.ResultKept => "RESULT_KEPT",
        HobbesSovereigntyStage.ResultAccepted => "RESULT_ACCEPTED",
        HobbesSovereigntyStage.ResultException => "RESULT_EXCEPTION",
        HobbesSovereigntyStage.ResultWithdrawn => "RESULT_WITHDRAWN",
        _ => "INVITATION",
    };

    public static string StageKey(HobbesSovereigntyStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HobbesSovereigntyAction action) => UpperSnake(action.ToString());

    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
