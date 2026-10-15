namespace STS2Philosophers;

internal enum HanFeiPublicStandardStage
{
    Invitation,
    XunziHearing,
    RuleDeclaration,
    QuotaCase,
    PublicStockCase,
    ResultXunziContext,
    ResultXunziRole,
    ResultHanFeiQuota,
    ResultHanFeiPublicStock,
    ResultRuleRevoked,
}

internal enum HanFeiPublicStandardAction
{
    None,
    StayWithXunzi,
    TestPublicStandard,
    InterpretRescueAsDuty,
    KeepRoleBoundary,
    DeclareQuotaStandard,
    DeclarePublicStockStandard,
    EnforceDeclaredStandard,
    RevokeDeclaredStandard,
    Leave,
}

internal enum HanFeiPublicStandardKind
{
    None,
    Quota,
    PublicStock,
}

internal enum HanFeiRouteDecision
{
    Undecided,
    Xunzi,
    HanFei,
}

internal sealed class HanFeiPublicStandardState
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HanFeiPublicStandardStage Stage { get; set; }
    public HanFeiPublicStandardAction PendingAction { get; set; }
    public List<HanFeiPublicStandardAction> ConfirmedActions { get; set; } = [];
    public HanFeiPublicStandardKind DeclaredStandard { get; set; }
    public HanFeiRouteDecision RouteDecision { get; set; }
    public bool PublicStandardDeclared { get; set; }
    public bool RuleRevokedAfterDisclosure { get; set; }
    public bool PracticeCompleted { get; set; }
    public bool Closed { get; set; }

    public static HanFeiPublicStandardState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = HanFeiPublicStandardStage.Invitation,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        bool resultStage = HanFeiPublicStandardFlow.IsResultStage(Stage);
        bool routeMatchesResult = Stage switch
        {
            HanFeiPublicStandardStage.ResultXunziContext or
            HanFeiPublicStandardStage.ResultXunziRole or
            HanFeiPublicStandardStage.ResultRuleRevoked => RouteDecision == HanFeiRouteDecision.Xunzi,
            HanFeiPublicStandardStage.ResultHanFeiQuota or
            HanFeiPublicStandardStage.ResultHanFeiPublicStock => RouteDecision == HanFeiRouteDecision.HanFei,
            _ => RouteDecision == HanFeiRouteDecision.Undecided,
        };
        bool standardMatchesStage = Stage switch
        {
            HanFeiPublicStandardStage.QuotaCase or
            HanFeiPublicStandardStage.ResultHanFeiQuota =>
                DeclaredStandard == HanFeiPublicStandardKind.Quota,
            HanFeiPublicStandardStage.PublicStockCase or
            HanFeiPublicStandardStage.ResultHanFeiPublicStock =>
                DeclaredStandard == HanFeiPublicStandardKind.PublicStock,
            HanFeiPublicStandardStage.ResultRuleRevoked =>
                DeclaredStandard is HanFeiPublicStandardKind.Quota
                    or HanFeiPublicStandardKind.PublicStock,
            _ => DeclaredStandard == HanFeiPublicStandardKind.None,
        };

        return Version == CurrentVersion
            && !string.IsNullOrWhiteSpace(InstanceId)
            && Enum.IsDefined(Stage)
            && Enum.IsDefined(PendingAction)
            && Enum.IsDefined(DeclaredStandard)
            && Enum.IsDefined(RouteDecision)
            && ConfirmedActions.All(Enum.IsDefined)
            && PublicStandardDeclared == (DeclaredStandard != HanFeiPublicStandardKind.None)
            && RuleRevokedAfterDisclosure
                == (Stage == HanFeiPublicStandardStage.ResultRuleRevoked)
            && PracticeCompleted == resultStage
            && routeMatchesResult
            && standardMatchesStage
            && (!Closed || resultStage);
    }
}

internal static class HanFeiPublicStandardFlow
{
    public static bool TryPrepare(
        HanFeiPublicStandardState state,
        HanFeiPublicStandardAction action)
    {
        if (!state.IsUsable()
            || state.Closed
            || state.PendingAction != HanFeiPublicStandardAction.None
            || !Options(state.Stage).Contains(action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(HanFeiPublicStandardState state)
    {
        if (!state.IsUsable()
            || state.PendingAction == HanFeiPublicStandardAction.None
            || !Options(state.Stage).Contains(state.PendingAction))
        {
            return false;
        }

        HanFeiPublicStandardAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = HanFeiPublicStandardAction.None;
        return state.IsUsable();
    }

    public static bool TryClose(HanFeiPublicStandardState state)
    {
        if (!state.IsUsable()
            || state.Closed
            || state.PendingAction != HanFeiPublicStandardAction.None
            || !IsResultStage(state.Stage))
        {
            return false;
        }

        state.Closed = true;
        state.ConfirmedActions.Add(HanFeiPublicStandardAction.Leave);
        return state.IsUsable();
    }

    public static IReadOnlyList<HanFeiPublicStandardAction> Options(
        HanFeiPublicStandardStage stage) => stage switch
    {
        HanFeiPublicStandardStage.Invitation =>
        [
            HanFeiPublicStandardAction.StayWithXunzi,
            HanFeiPublicStandardAction.TestPublicStandard,
        ],
        HanFeiPublicStandardStage.XunziHearing =>
        [
            HanFeiPublicStandardAction.InterpretRescueAsDuty,
            HanFeiPublicStandardAction.KeepRoleBoundary,
        ],
        HanFeiPublicStandardStage.RuleDeclaration =>
        [
            HanFeiPublicStandardAction.DeclareQuotaStandard,
            HanFeiPublicStandardAction.DeclarePublicStockStandard,
        ],
        HanFeiPublicStandardStage.QuotaCase or
        HanFeiPublicStandardStage.PublicStockCase =>
        [
            HanFeiPublicStandardAction.EnforceDeclaredStandard,
            HanFeiPublicStandardAction.RevokeDeclaredStandard,
        ],
        HanFeiPublicStandardStage.ResultXunziContext or
        HanFeiPublicStandardStage.ResultXunziRole or
        HanFeiPublicStandardStage.ResultHanFeiQuota or
        HanFeiPublicStandardStage.ResultHanFeiPublicStock or
        HanFeiPublicStandardStage.ResultRuleRevoked =>
        [HanFeiPublicStandardAction.Leave],
        _ => [],
    };

    public static bool IsResultStage(HanFeiPublicStandardStage stage) => stage is
        HanFeiPublicStandardStage.ResultXunziContext or
        HanFeiPublicStandardStage.ResultXunziRole or
        HanFeiPublicStandardStage.ResultHanFeiQuota or
        HanFeiPublicStandardStage.ResultHanFeiPublicStock or
        HanFeiPublicStandardStage.ResultRuleRevoked;

    public static string StageKey(HanFeiPublicStandardStage stage) =>
        ToUpperSnake(stage.ToString());

    public static string ActionKey(HanFeiPublicStandardAction action) =>
        ToUpperSnake(action.ToString());

    private static void Apply(
        HanFeiPublicStandardState state,
        HanFeiPublicStandardAction action)
    {
        switch (action)
        {
            case HanFeiPublicStandardAction.StayWithXunzi:
                state.Stage = HanFeiPublicStandardStage.XunziHearing;
                break;
            case HanFeiPublicStandardAction.TestPublicStandard:
                state.Stage = HanFeiPublicStandardStage.RuleDeclaration;
                break;
            case HanFeiPublicStandardAction.InterpretRescueAsDuty:
                Complete(state, HanFeiRouteDecision.Xunzi,
                    HanFeiPublicStandardStage.ResultXunziContext);
                break;
            case HanFeiPublicStandardAction.KeepRoleBoundary:
                Complete(state, HanFeiRouteDecision.Xunzi,
                    HanFeiPublicStandardStage.ResultXunziRole);
                break;
            case HanFeiPublicStandardAction.DeclareQuotaStandard:
                Declare(state, HanFeiPublicStandardKind.Quota,
                    HanFeiPublicStandardStage.QuotaCase);
                break;
            case HanFeiPublicStandardAction.DeclarePublicStockStandard:
                Declare(state, HanFeiPublicStandardKind.PublicStock,
                    HanFeiPublicStandardStage.PublicStockCase);
                break;
            case HanFeiPublicStandardAction.EnforceDeclaredStandard:
                Complete(
                    state,
                    HanFeiRouteDecision.HanFei,
                    state.DeclaredStandard == HanFeiPublicStandardKind.Quota
                        ? HanFeiPublicStandardStage.ResultHanFeiQuota
                        : HanFeiPublicStandardStage.ResultHanFeiPublicStock);
                break;
            case HanFeiPublicStandardAction.RevokeDeclaredStandard:
                state.RuleRevokedAfterDisclosure = true;
                Complete(state, HanFeiRouteDecision.Xunzi,
                    HanFeiPublicStandardStage.ResultRuleRevoked);
                break;
            case HanFeiPublicStandardAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Declare(
        HanFeiPublicStandardState state,
        HanFeiPublicStandardKind standard,
        HanFeiPublicStandardStage stage)
    {
        state.DeclaredStandard = standard;
        state.PublicStandardDeclared = true;
        state.Stage = stage;
    }

    private static void Complete(
        HanFeiPublicStandardState state,
        HanFeiRouteDecision route,
        HanFeiPublicStandardStage stage)
    {
        state.RouteDecision = route;
        state.PracticeCompleted = true;
        state.Stage = stage;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) => index > 0 && char.IsUpper(character)
            ? new[] { '_', character }
            : new[] { char.ToUpperInvariant(character) }));
}
