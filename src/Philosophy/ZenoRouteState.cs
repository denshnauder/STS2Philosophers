namespace STS2Philosophers;

internal static class ZenoRouteFeatureGeneration
{
    public const int Current = 1;
}

internal static class ZenoRouteIds
{
    public const string RouteEdge = "WESTERN_EDGE_011";
    public const string TerminalNode = "ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE";
}

internal enum ZenoRouteStage
{
    Unresolved,
    DiogenesClosed,
    WaitingInterval,
    ReadyToClaim,
    OpeningClaimed,
    EventActive,
    OutcomeCommitted,
    ZenoClosed,
    RunTerminated,
}

internal enum ZenoAssentOutcome
{
    Keep,
    Narrow,
    Withdraw,
    NoReassent,
    NoAssent,
}

internal enum ZenoOpeningTrigger
{
    SafeBoundary,
    BeforeBoss,
    BeforeActExit,
}

internal enum ZenoResumeDestination
{
    Map,
    Boss,
    ActExit,
}

internal enum ZenoRoutePayloadClassification
{
    PreFeatureLegacy,
    Current,
    UnsupportedOlder,
    UnknownNewer,
    Invalid,
}

internal enum ZenoRouteOperationKind
{
    Stay,
    Switch,
    IntervalCompleted,
    OpeningClaimed,
    EventEstablished,
    OutcomeCommitted,
    EventClosed,
}

internal enum ZenoRouteTransitionStatus
{
    Prepared,
    PendingReused,
    Committed,
    CommitReused,
    Rejected,
}

internal enum ZenoRouteTransitionFailure
{
    None,
    InvalidFeature,
    RouteMissing,
    StaleRevision,
    InvalidSourceStage,
    PendingConflict,
    OperationMismatch,
    InvalidCandidate,
}

internal sealed record ZenoRouteFeatureState(
    int FeatureGeneration,
    ZenoRouteState? Route,
    ZenoRoutePendingOperation? PendingOperation = null);

internal sealed record ZenoRouteState(
    int Version,
    ZenoRouteStage Stage,
    long Revision,
    long LastCommittedOperationId,
    string RunId,
    int ActIndex,
    string RouteEdgeId,
    string TerminalNodeId,
    ZenoAssentMaterialSnapshot? Material,
    string? CompletedRoomReceiptId,
    ZenoOpeningTrigger? OpeningTrigger,
    ZenoResumeDestination? ResumeDestination,
    string? ResumeDestinationId,
    string? EventInstanceId,
    ZenoAssentOutcome? Outcome,
    string? CurrentStatementAfterId)
{
    public const int CurrentVersion = 1;
}

internal sealed class ZenoAssentMaterialSnapshot
{
    public ZenoAssentMaterialSnapshot(
        int sourceVersion,
        string sourceKind,
        string? actionFactId,
        bool isNoMaterial,
        IEnumerable<string> publicHistoryIds,
        string? currentStatementId,
        string? narrowStatementId,
        string? laterOutcomeId,
        string digest)
    {
        SourceVersion = sourceVersion;
        SourceKind = sourceKind;
        ActionFactId = actionFactId;
        IsNoMaterial = isNoMaterial;
        PublicHistoryIds = publicHistoryIds.ToArray();
        CurrentStatementId = currentStatementId;
        NarrowStatementId = narrowStatementId;
        LaterOutcomeId = laterOutcomeId;
        Digest = digest;
    }

    public int SourceVersion { get; }
    public string SourceKind { get; }
    public string? ActionFactId { get; }
    public bool IsNoMaterial { get; }
    public IReadOnlyList<string> PublicHistoryIds { get; }
    public string? CurrentStatementId { get; }
    public string? NarrowStatementId { get; }
    public string? LaterOutcomeId { get; }
    public string Digest { get; }
}

internal sealed record ZenoRoutePendingOperation(
    long OperationId,
    ZenoRouteOperationKind Kind,
    long SourceRevision,
    ZenoRouteStage SourceStage,
    ZenoRouteStage TargetStage,
    ZenoRouteState Candidate,
    string CandidateDigest);

internal sealed record ZenoRouteTransitionResult(
    ZenoRouteTransitionStatus Status,
    ZenoRouteTransitionFailure Failure,
    ZenoRouteFeatureState State)
{
    public bool IsAccepted => Status != ZenoRouteTransitionStatus.Rejected;
}

internal sealed class ZenoRouteValidationCatalog
{
    private readonly Dictionary<string, int> _sourceVersions;
    private readonly HashSet<string> _actionFactIds;
    private readonly HashSet<string> _statementIds;
    private readonly HashSet<string> _laterOutcomeIds;

    public ZenoRouteValidationCatalog(
        IEnumerable<KeyValuePair<string, int>> sourceVersions,
        IEnumerable<string> actionFactIds,
        IEnumerable<string> statementIds,
        IEnumerable<string> laterOutcomeIds)
    {
        _sourceVersions = sourceVersions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _actionFactIds = actionFactIds.ToHashSet(StringComparer.Ordinal);
        _statementIds = statementIds.ToHashSet(StringComparer.Ordinal);
        _laterOutcomeIds = laterOutcomeIds.ToHashSet(StringComparer.Ordinal);
    }

    public bool IsKnownSource(string sourceKind, int sourceVersion) =>
        _sourceVersions.TryGetValue(sourceKind, out int knownVersion) && knownVersion == sourceVersion;

    public bool IsKnownActionFact(string value) => _actionFactIds.Contains(value);
    public bool IsKnownStatement(string value) => _statementIds.Contains(value);
    public bool IsKnownLaterOutcome(string value) => _laterOutcomeIds.Contains(value);
}

internal sealed record ZenoRouteDecodeResult(
    ZenoRoutePayloadClassification Classification,
    ZenoRouteFeatureState? State,
    string? OpaquePayload);
