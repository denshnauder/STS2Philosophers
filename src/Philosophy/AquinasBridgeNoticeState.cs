namespace STS2Philosophers;

internal enum AquinasBridgeNoticeStage
{
    Invitation, Audit, BrokenBridge, ResultPublished, ResultEmergency, ResultKept, ResultDeclined,
}

internal enum AquinasBridgeNoticeAction
{
    None, AcceptInvitation, DeclineInvitation, AuditAuthority, AuditPublication,
    PublishCorrection, VerifyException, KeepOldRule, Leave,
}

internal enum AquinasAudit { None, Authority, Publication }
internal enum AquinasRule { None, Published, Emergency, Kept, Declined }

internal sealed class AquinasBridgeNoticeState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "THOMAS_AQUINAS__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_016";
    public const string CompletionEdgeId = "WESTERN_EDGE_045";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public AquinasBridgeNoticeStage Stage { get; set; }
    public AquinasBridgeNoticeAction PendingAction { get; set; }
    public List<AquinasBridgeNoticeAction> ConfirmedActions { get; set; } = [];
    public AquinasAudit Audit { get; set; }
    public AquinasRule Rule { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static AquinasBridgeNoticeState Create(string id) => new() { InstanceId = id };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Audit) || !Enum.IsDefined(Rule)
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != AquinasBridgeNoticeAction.None
            || PendingAction != AquinasBridgeNoticeAction.None
                && !AquinasBridgeNoticeFlow.Options(Stage).Contains(PendingAction)) return false;
        AquinasBridgeNoticeAction[] history = Closed
            ? ConfirmedActions.Take(ConfirmedActions.Count - 1).ToArray()
            : ConfirmedActions.ToArray();
        if (Closed && (ConfirmedActions.Count == 0
            || ConfirmedActions[^1] != AquinasBridgeNoticeAction.Leave)) return false;
        AquinasBridgeNoticeAction auditAction = Audit switch
        {
            AquinasAudit.Authority => AquinasBridgeNoticeAction.AuditAuthority,
            AquinasAudit.Publication => AquinasBridgeNoticeAction.AuditPublication,
            _ => AquinasBridgeNoticeAction.None,
        };
        AquinasBridgeNoticeAction finalAction = Rule switch
        {
            AquinasRule.Published => AquinasBridgeNoticeAction.PublishCorrection,
            AquinasRule.Emergency => AquinasBridgeNoticeAction.VerifyException,
            AquinasRule.Kept => AquinasBridgeNoticeAction.KeepOldRule,
            AquinasRule.Declined => AquinasBridgeNoticeAction.DeclineInvitation,
            _ => AquinasBridgeNoticeAction.None,
        };
        AquinasBridgeNoticeAction[] expected = Stage switch
        {
            AquinasBridgeNoticeStage.Invitation => [],
            AquinasBridgeNoticeStage.Audit => [AquinasBridgeNoticeAction.AcceptInvitation],
            AquinasBridgeNoticeStage.BrokenBridge when auditAction != AquinasBridgeNoticeAction.None =>
                [AquinasBridgeNoticeAction.AcceptInvitation, auditAction],
            AquinasBridgeNoticeStage.ResultDeclined => [AquinasBridgeNoticeAction.DeclineInvitation],
            >= AquinasBridgeNoticeStage.ResultPublished and <= AquinasBridgeNoticeStage.ResultKept
                when auditAction != AquinasBridgeNoticeAction.None && finalAction != AquinasBridgeNoticeAction.None =>
                [AquinasBridgeNoticeAction.AcceptInvitation, auditAction, finalAction],
            _ => [AquinasBridgeNoticeAction.None],
        };
        bool result = Stage >= AquinasBridgeNoticeStage.ResultPublished;
        bool adopted = Stage is AquinasBridgeNoticeStage.ResultPublished or AquinasBridgeNoticeStage.ResultEmergency;
        return history.SequenceEqual(expected) && FixedSlotResolved == result && EdgeCommitted == adopted
            && Rule == (Stage switch
            {
                AquinasBridgeNoticeStage.ResultPublished => AquinasRule.Published,
                AquinasBridgeNoticeStage.ResultEmergency => AquinasRule.Emergency,
                AquinasBridgeNoticeStage.ResultKept => AquinasRule.Kept,
                AquinasBridgeNoticeStage.ResultDeclined => AquinasRule.Declined,
                _ => AquinasRule.None,
            })
            && (Stage is not AquinasBridgeNoticeStage.Invitation and not AquinasBridgeNoticeStage.Audit
                || Audit == AquinasAudit.None)
            && (Stage != AquinasBridgeNoticeStage.ResultDeclined || Audit == AquinasAudit.None)
            && (!Closed || result);
    }
}

internal static class AquinasBridgeNoticeFlow
{
    public static IReadOnlyList<AquinasBridgeNoticeAction> Options(AquinasBridgeNoticeStage stage) => stage switch
    {
        AquinasBridgeNoticeStage.Invitation => [AquinasBridgeNoticeAction.AcceptInvitation,
            AquinasBridgeNoticeAction.DeclineInvitation],
        AquinasBridgeNoticeStage.Audit => [AquinasBridgeNoticeAction.AuditAuthority,
            AquinasBridgeNoticeAction.AuditPublication],
        AquinasBridgeNoticeStage.BrokenBridge => [AquinasBridgeNoticeAction.PublishCorrection,
            AquinasBridgeNoticeAction.VerifyException, AquinasBridgeNoticeAction.KeepOldRule],
        >= AquinasBridgeNoticeStage.ResultPublished and <= AquinasBridgeNoticeStage.ResultDeclined =>
            [AquinasBridgeNoticeAction.Leave],
        _ => [],
    };

    public static bool IsRouteConsistent(AquinasBridgeNoticeState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || !journey.CompletedEdgeIds.Contains(AquinasBridgeNoticeState.RequiredEdgeId)
            || journey.RetainedEnding || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_017")) return false;
        if (!state.FixedSlotResolved)
            return journey.LastFixedAct == 2 && journey.CurrentNodeId == AquinasBridgeNoticeState.SourceNodeId
                && !journey.DeclinedFixedActs.Contains(3)
                && !journey.CompletedEdgeIds.Contains(AquinasBridgeNoticeState.CompletionEdgeId);
        return journey.LastFixedAct == 3 && (state.EdgeCommitted
            ? journey.CurrentNodeId == AquinasBridgeNoticeState.AdoptedNodeId
                && journey.CompletedEdgeIds.Contains(AquinasBridgeNoticeState.CompletionEdgeId)
            : journey.CurrentNodeId == AquinasBridgeNoticeState.SourceNodeId
                && journey.DeclinedFixedActs.Contains(3)
                && !journey.CompletedEdgeIds.Contains(AquinasBridgeNoticeState.CompletionEdgeId));
    }

    public static bool TryPrepare(AquinasBridgeNoticeState state, AquinasBridgeNoticeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AquinasBridgeNoticeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(AquinasBridgeNoticeState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == AquinasBridgeNoticeAction.None)
            return false;
        AquinasBridgeNoticeAction action = state.PendingAction;
        bool adopts = action is AquinasBridgeNoticeAction.PublishCorrection or AquinasBridgeNoticeAction.VerifyException;
        bool declines = action is AquinasBridgeNoticeAction.DeclineInvitation or AquinasBridgeNoticeAction.KeepOldRule;
        if (adopts && !graph.TryApply(journey, AquinasBridgeNoticeState.CompletionEdgeId)
            || declines && !graph.TryDecline(journey, "Fixed", 3)) return false;
        switch (action)
        {
            case AquinasBridgeNoticeAction.AcceptInvitation: state.Stage = AquinasBridgeNoticeStage.Audit; break;
            case AquinasBridgeNoticeAction.DeclineInvitation:
                state.Rule = AquinasRule.Declined; state.Stage = AquinasBridgeNoticeStage.ResultDeclined; break;
            case AquinasBridgeNoticeAction.AuditAuthority:
                state.Audit = AquinasAudit.Authority; state.Stage = AquinasBridgeNoticeStage.BrokenBridge; break;
            case AquinasBridgeNoticeAction.AuditPublication:
                state.Audit = AquinasAudit.Publication; state.Stage = AquinasBridgeNoticeStage.BrokenBridge; break;
            case AquinasBridgeNoticeAction.PublishCorrection:
                state.Rule = AquinasRule.Published; state.Stage = AquinasBridgeNoticeStage.ResultPublished; break;
            case AquinasBridgeNoticeAction.VerifyException:
                state.Rule = AquinasRule.Emergency; state.Stage = AquinasBridgeNoticeStage.ResultEmergency; break;
            case AquinasBridgeNoticeAction.KeepOldRule:
                state.Rule = AquinasRule.Kept; state.Stage = AquinasBridgeNoticeStage.ResultKept; break;
            case AquinasBridgeNoticeAction.Leave: state.Closed = true; break;
            default: return false;
        }
        state.FixedSlotResolved |= adopts || declines;
        state.EdgeCommitted |= adopts;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AquinasBridgeNoticeAction.None;
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(AquinasBridgeNoticeStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(AquinasBridgeNoticeAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(AquinasBridgeNoticeState state) =>
        state.Stage == AquinasBridgeNoticeStage.BrokenBridge
            ? $"BROKEN_BRIDGE_{UpperSnake(state.Audit.ToString())}"
            : StageKey(state.Stage);
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
