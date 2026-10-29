namespace STS2Philosophers;

internal enum AustinSpeechActStage
{
    Station,
    Evidence,
    ResultCompleted,
    ResultWithdrawn,
    ResultPending,
}

internal enum AustinSpeechActAction
{
    None,
    CheckAuthorization,
    CheckPresence,
    CheckProcedure,
    CompleteProcedure,
    Withdraw,
    RecordPromise,
    Leave,
}

internal enum AustinSpeechActOutcome
{
    None,
    Completed,
    Withdrawn,
    Pending,
}

internal sealed class AustinSpeechActState : IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public AustinSpeechActStage Stage { get; set; }
    public AustinSpeechActAction PendingAction { get; set; }
    public List<AustinSpeechActAction> ConfirmedActions { get; set; } = [];
    public bool AuthorizationChecked { get; set; }
    public bool PresenceChecked { get; set; }
    public bool ProcedureChecked { get; set; }
    public int CheckCount { get; set; }
    public bool UnauthorizedSignatureDiscovered { get; set; }
    public AustinSpeechActOutcome Outcome { get; set; }
    public bool PriorityPassSpent { get; set; }
    public bool PendingPromise { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public bool Closed { get; set; }

    public static AustinSpeechActState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = AustinSpeechActStage.Station,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Outcome) || ConfirmedActions is null
            || ConfirmedActions.Count > 6 || CheckCount is < 0 or > 2
            || (CostAttempted && Outcome == AustinSpeechActOutcome.None)
            || (CostPaid && !CostAttempted)
            || UnauthorizedSignatureDiscovered != (CheckCount == 2)
            || PriorityPassSpent != (Outcome == AustinSpeechActOutcome.Completed)
            || PendingPromise != (Outcome == AustinSpeechActOutcome.Pending)
            || Outcome switch
            {
                AustinSpeechActOutcome.None => Stage is not (AustinSpeechActStage.Station
                    or AustinSpeechActStage.Evidence),
                AustinSpeechActOutcome.Completed => Stage != AustinSpeechActStage.ResultCompleted,
                AustinSpeechActOutcome.Withdrawn => Stage != AustinSpeechActStage.ResultWithdrawn,
                AustinSpeechActOutcome.Pending => Stage != AustinSpeechActStage.ResultPending,
                _ => true,
            }) return false;

        AustinSpeechActStage replay = AustinSpeechActStage.Station;
        int checks = 0;
        bool authorization = false, presence = false, procedure = false, closed = false;
        foreach (AustinSpeechActAction action in ConfirmedActions)
        {
            if (closed || !Available(replay, checks, authorization, presence, procedure).Contains(action))
                return false;
            switch (action)
            {
                case AustinSpeechActAction.CheckAuthorization:
                    authorization = true; checks++; break;
                case AustinSpeechActAction.CheckPresence:
                    presence = true; checks++; break;
                case AustinSpeechActAction.CheckProcedure:
                    procedure = true; checks++; break;
            }
            replay = Next(replay, checks, action);
            closed = action == AustinSpeechActAction.Leave;
        }
        return replay == Stage && closed == Closed && checks == CheckCount
            && authorization == AuthorizationChecked && presence == PresenceChecked
            && procedure == ProcedureChecked
            && (PendingAction == AustinSpeechActAction.None
                || !Closed && Available(Stage, CheckCount, AuthorizationChecked,
                    PresenceChecked, ProcedureChecked).Contains(PendingAction));
    }

    public decimal Cost => Outcome == AustinSpeechActOutcome.Withdrawn ? 10 : 0;

    public static IReadOnlyList<AustinSpeechActAction> Available(AustinSpeechActStage stage,
        int checkCount, bool authorization, bool presence, bool procedure) => stage switch
    {
        AustinSpeechActStage.Station when checkCount < 2 =>
            new[]
            {
                AustinSpeechActAction.CheckAuthorization,
                AustinSpeechActAction.CheckPresence,
                AustinSpeechActAction.CheckProcedure,
            }.Where(action => action switch
            {
                AustinSpeechActAction.CheckAuthorization => !authorization,
                AustinSpeechActAction.CheckPresence => !presence,
                AustinSpeechActAction.CheckProcedure => !procedure,
                _ => false,
            }).ToArray(),
        AustinSpeechActStage.Evidence =>
            [AustinSpeechActAction.CompleteProcedure, AustinSpeechActAction.Withdraw,
                AustinSpeechActAction.RecordPromise],
        AustinSpeechActStage.ResultCompleted or AustinSpeechActStage.ResultWithdrawn
            or AustinSpeechActStage.ResultPending => [AustinSpeechActAction.Leave],
        _ => [],
    };

    public static AustinSpeechActStage Next(AustinSpeechActStage stage, int checks,
        AustinSpeechActAction action) => (stage, action) switch
    {
        (AustinSpeechActStage.Station, AustinSpeechActAction.CheckAuthorization)
            or (AustinSpeechActStage.Station, AustinSpeechActAction.CheckPresence)
            or (AustinSpeechActStage.Station, AustinSpeechActAction.CheckProcedure) =>
            checks >= 2 ? AustinSpeechActStage.Evidence : AustinSpeechActStage.Station,
        (AustinSpeechActStage.Evidence, AustinSpeechActAction.CompleteProcedure) =>
            AustinSpeechActStage.ResultCompleted,
        (AustinSpeechActStage.Evidence, AustinSpeechActAction.Withdraw) =>
            AustinSpeechActStage.ResultWithdrawn,
        (AustinSpeechActStage.Evidence, AustinSpeechActAction.RecordPromise) =>
            AustinSpeechActStage.ResultPending,
        (AustinSpeechActStage.ResultCompleted or AustinSpeechActStage.ResultWithdrawn
            or AustinSpeechActStage.ResultPending, AustinSpeechActAction.Leave) => stage,
        _ => throw new InvalidOperationException($"Invalid Austin transition {stage}/{action}."),
    };
}

internal static class AustinSpeechActFlow
{
    public static bool TryPrepare(AustinSpeechActState state, AustinSpeechActAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AustinSpeechActAction.None
            || !AustinSpeechActState.Available(state.Stage, state.CheckCount,
                state.AuthorizationChecked, state.PresenceChecked, state.ProcedureChecked)
                .Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(AustinSpeechActState state, WesternJourneyState journey)
    {
        if (!IsRouteConsistent(state, journey)
            || state.PendingAction == AustinSpeechActAction.None
            || !AustinSpeechActState.Available(state.Stage, state.CheckCount,
                state.AuthorizationChecked, state.PresenceChecked, state.ProcedureChecked)
                .Contains(state.PendingAction)) return false;
        AustinSpeechActAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = AustinSpeechActAction.None;
        return true;
    }

    public static bool IsRouteConsistent(AustinSpeechActState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != AustinSpeechActState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_011")) return false;
        return state.Outcome == AustinSpeechActOutcome.None
            || state.ConfirmedActions.Contains(state.Outcome switch
            {
                AustinSpeechActOutcome.Completed => AustinSpeechActAction.CompleteProcedure,
                AustinSpeechActOutcome.Withdrawn => AustinSpeechActAction.Withdraw,
                AustinSpeechActOutcome.Pending => AustinSpeechActAction.RecordPromise,
                _ => AustinSpeechActAction.None,
            });
    }

    public static string StageKey(AustinSpeechActStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(AustinSpeechActAction action) => ToUpperSnake(action.ToString());
    public static string DescriptionKey(AustinSpeechActState state) => state.Stage switch
    {
        AustinSpeechActStage.Station => state.CheckCount == 0 ? "STATION" : "STATION_CHECKED",
        AustinSpeechActStage.Evidence => "EVIDENCE",
        AustinSpeechActStage.ResultCompleted => "RESULT_COMPLETED",
        AustinSpeechActStage.ResultWithdrawn => "RESULT_WITHDRAWN",
        AustinSpeechActStage.ResultPending => "RESULT_PENDING",
        _ => "STATION",
    };

    private static void Apply(AustinSpeechActState state, AustinSpeechActAction action)
    {
        switch (action)
        {
            case AustinSpeechActAction.CheckAuthorization:
                state.AuthorizationChecked = true; state.CheckCount++; break;
            case AustinSpeechActAction.CheckPresence:
                state.PresenceChecked = true; state.CheckCount++; break;
            case AustinSpeechActAction.CheckProcedure:
                state.ProcedureChecked = true; state.CheckCount++; break;
            case AustinSpeechActAction.CompleteProcedure:
                state.PriorityPassSpent = true;
                state.Outcome = AustinSpeechActOutcome.Completed;
                state.Stage = AustinSpeechActStage.ResultCompleted;
                break;
            case AustinSpeechActAction.Withdraw:
                state.Outcome = AustinSpeechActOutcome.Withdrawn;
                state.Stage = AustinSpeechActStage.ResultWithdrawn;
                break;
            case AustinSpeechActAction.RecordPromise:
                state.PendingPromise = true;
                state.Outcome = AustinSpeechActOutcome.Pending;
                state.Stage = AustinSpeechActStage.ResultPending;
                break;
            case AustinSpeechActAction.Leave:
                state.Closed = true;
                break;
        }
        if (state.CheckCount >= 2 && state.Stage == AustinSpeechActStage.Station)
            state.Stage = AustinSpeechActStage.Evidence;
        if (state.CheckCount == 2) state.UnauthorizedSignatureDiscovered = true;
    }

    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
