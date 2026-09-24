using STS2Philosophers;

internal static class ZenoRoutePersistenceConfirmationChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement, NarrowStatement],
        []);

    public static void Run()
    {
        SaveTaskCompletionIsNeverConfirmation();
        ExactPreparedReadBackConfirmsOnce();
        UnknownPreparedResultKeepsStableRetryIdentity();
        MismatchDuplicateAndUnavailableReadBackRemainUnknown();
        ExplicitFailureRequiresProofOfNoPersistence();
        StaleAdapterResultCannotConfirmAnotherRequest();
        CommittedCheckpointHasDistinctIdentityAndExactReadBack();
        InvalidStatesCannotCreateRequests();

        Console.WriteLine("Zeno persistence confirmation checks passed: strict three-state evidence and stable retry identity.");
    }

    private static void SaveTaskCompletionIsNeverConfirmation()
    {
        (ZenoRouteFeatureState prepared, ZenoRoutePersistenceRequest request) = CreatePrepared();
        ZenoRoutePersistenceResult result = ZenoRoutePersistenceConfirmation.FromSaveTaskCompletion(request);

        Assert(prepared.PendingOperation is not null, "The request fixture should retain its write-ahead record.");
        Assert(result.Status == ZenoRoutePersistenceStatus.Unknown,
            "Awaiting the save task must not claim durable confirmation.");
    }

    private static void ExactPreparedReadBackConfirmsOnce()
    {
        (ZenoRouteFeatureState prepared, ZenoRoutePersistenceRequest request) = CreatePrepared();
        ZenoRoutePersistenceResult result = ZenoRoutePersistenceConfirmation.FromReadBack(
            request,
            1,
            ZenoRoutePayloadClassification.Current,
            prepared,
            Catalog);

        Assert(result.Status == ZenoRoutePersistenceStatus.Confirmed,
            "One current marker with the exact prepared record should confirm persistence.");
        Assert(result.RequestId == request.RequestId, "The result should retain the request identity.");
    }

    private static void UnknownPreparedResultKeepsStableRetryIdentity()
    {
        (ZenoRouteFeatureState prepared, ZenoRoutePersistenceRequest request) = CreatePrepared();
        Assert(
            ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(prepared, Catalog, out ZenoRoutePersistenceRequest? retry),
            "The same prepared record should remain eligible for retry.");

        Assert(retry?.RequestId == request.RequestId,
            "Retrying an unknown result must reuse the same deterministic request identity.");
        Assert(retry?.OperationId == request.OperationId && retry.CandidateDigest == request.CandidateDigest,
            "Retrying must retain the same operation and candidate.");
    }

    private static void MismatchDuplicateAndUnavailableReadBackRemainUnknown()
    {
        (ZenoRouteFeatureState prepared, ZenoRoutePersistenceRequest request) = CreatePrepared();
        ZenoRouteFeatureState committed = Commit(prepared);

        AssertUnknownReadBack(request, 2, ZenoRoutePayloadClassification.Current, prepared,
            "Duplicate current markers must not confirm persistence.");
        AssertUnknownReadBack(request, 1, ZenoRoutePayloadClassification.UnknownNewer, prepared,
            "An unknown marker version must not confirm persistence.");
        AssertUnknownReadBack(request, 1, ZenoRoutePayloadClassification.Current, committed,
            "A different checkpoint must not confirm the prepared request.");
        AssertUnknownReadBack(request, 0, ZenoRoutePayloadClassification.PreFeatureLegacy, null,
            "Unavailable or absent read-back evidence must remain unknown.");
    }

    private static void ExplicitFailureRequiresProofOfNoPersistence()
    {
        (_, ZenoRoutePersistenceRequest request) = CreatePrepared();

        Assert(
            ZenoRoutePersistenceConfirmation.FromExplicitFailure(request, true).Status ==
            ZenoRoutePersistenceStatus.Failed,
            "A failure may be final only when non-persistence is confirmed.");
        Assert(
            ZenoRoutePersistenceConfirmation.FromExplicitFailure(request, false).Status ==
            ZenoRoutePersistenceStatus.Unknown,
            "A reported error without non-persistence proof must remain unknown.");
    }

    private static void StaleAdapterResultCannotConfirmAnotherRequest()
    {
        (_, ZenoRoutePersistenceRequest request) = CreatePrepared();
        ZenoRoutePersistenceResult stale = new(
            "ZENO_ROUTE_SAVE_V1_STALE",
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceResult validated =
            ZenoRoutePersistenceConfirmation.ValidateAdapterResult(request, stale);

        Assert(validated.Status == ZenoRoutePersistenceStatus.Unknown,
            "A receipt for another request must not confirm this checkpoint.");
        Assert(validated.RequestId == request.RequestId,
            "A rejected stale receipt should return the active request identity.");
    }

    private static void CommittedCheckpointHasDistinctIdentityAndExactReadBack()
    {
        (ZenoRouteFeatureState prepared, ZenoRoutePersistenceRequest preparedRequest) = CreatePrepared();
        ZenoRouteFeatureState committed = Commit(prepared);
        Assert(
            ZenoRoutePersistenceConfirmation.TryCreateCommittedRequest(
                committed,
                Catalog,
                out ZenoRoutePersistenceRequest? committedRequest),
            "A valid committed state should create its persistence request.");

        Assert(committedRequest is not null, "The committed request should be returned.");
        Assert(committedRequest!.RequestId != preparedRequest.RequestId,
            "Prepared and committed checkpoints must not share an identity.");
        Assert(
            ZenoRoutePersistenceConfirmation.FromReadBack(
                committedRequest,
                1,
                ZenoRoutePayloadClassification.Current,
                committed,
                Catalog).Status == ZenoRoutePersistenceStatus.Confirmed,
            "The exact committed checkpoint should confirm persistence.");
    }

    private static void InvalidStatesCannotCreateRequests()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        Assert(!ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(initial, Catalog, out _),
            "A route without a write-ahead record cannot create a prepared request.");
        Assert(!ZenoRoutePersistenceConfirmation.TryCreateCommittedRequest(initial, Catalog, out _),
            "An operation-zero route cannot create a committed request.");
    }

    private static void AssertUnknownReadBack(
        ZenoRoutePersistenceRequest request,
        int markerCount,
        ZenoRoutePayloadClassification classification,
        ZenoRouteFeatureState? state,
        string message)
    {
        ZenoRoutePersistenceResult result = ZenoRoutePersistenceConfirmation.FromReadBack(
            request,
            markerCount,
            classification,
            state,
            Catalog);
        Assert(result.Status == ZenoRoutePersistenceStatus.Unknown, message);
    }

    private static (ZenoRouteFeatureState State, ZenoRoutePersistenceRequest Request) CreatePrepared()
    {
        ZenoRouteTransitionResult transition = ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog);
        Assert(transition.Status == ZenoRouteTransitionStatus.Prepared, "The fixture should prepare Switch.");
        Assert(
            ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                transition.State,
                Catalog,
                out ZenoRoutePersistenceRequest? request),
            "A valid write-ahead record should create a persistence request.");
        return (transition.State, request!);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected a write-ahead record.");
        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog);
        Assert(committed.Status == ZenoRouteTransitionStatus.Committed, "The fixture should commit Switch.");
        return committed.State;
    }

    private static ZenoRouteFeatureState CreateUnresolved()
    {
        ZenoRouteState route = new(
            ZenoRouteState.CurrentVersion,
            ZenoRouteStage.Unresolved,
            0,
            0,
            "RUN_20260924_PERSISTENCE_001",
            2,
            ZenoRouteIds.RouteEdge,
            ZenoRouteIds.TerminalNode,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        return new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, route);
    }

    private static ZenoAssentMaterialSnapshot CreateMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            null);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
