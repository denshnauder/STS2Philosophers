namespace STS2Philosophers;

internal static class ZenoRoutePersistenceRuntimeChecks
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
        StableStateCreatesOneRuntimeWithoutRetry();
        SavedPreparationRestoresItsOriginalRetryIdentity();
        UnknownResultFreezesAndRetriesInsideTheSameRuntime();
        ReleasedPreparationRestoresTheSharedSourceState();
        RuntimeCatalogRecoversAValidMaterialMarker();
        InvalidOrMissingRoutesDoNotCreateRuntime();
        Console.WriteLine("Zeno persistence runtime checks passed: shared ownership, recovery and retry identity.");
    }

    private static void StableStateCreatesOneRuntimeWithoutRetry()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        ScriptedAdapter adapter = new();

        Assert(ZenoRoutePersistenceRuntime.TryCreate(shared, Catalog, adapter, out ZenoRoutePersistenceRuntime? runtime),
            "A valid current route should create its per-run runtime.");
        Assert(runtime is not null && runtime.PendingRequestId is null && runtime.PendingCheckpoint is null,
            "A stable route should not invent a recovery request.");
        ZenoRoutePersistenceCoordinationResult retry = runtime!.RetryAsync().GetAwaiter().GetResult();
        Assert(retry.Status == ZenoRoutePersistenceCoordinationStatus.NoPendingRetry && adapter.Requests.Count == 0,
            "A stable runtime must not touch persistence when no retry exists.");
    }

    private static void SavedPreparationRestoresItsOriginalRetryIdentity()
    {
        ZenoRouteTransitionResult prepared = PrepareSwitch(CreateUnresolved());
        Assert(ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                prepared.State,
                Catalog,
                out ZenoRoutePersistenceRequest? original),
            "The saved preparation fixture must have a deterministic request.");
        PhilosophyRunState shared = Shared(prepared.State);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);

        Assert(ZenoRoutePersistenceRuntime.TryCreate(shared, Catalog, adapter, out ZenoRoutePersistenceRuntime? runtime),
            "A valid saved preparation should restore a runtime.");
        Assert(runtime?.PendingCheckpoint == ZenoRoutePersistenceCheckpoint.Prepared &&
               runtime.PendingRequestId == original?.RequestId,
            "Restored preparation must expose the same request identity before retry.");

        ZenoRoutePersistenceCoordinationResult result = runtime!.RetryAsync().GetAwaiter().GetResult();
        Assert(result.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
               adapter.Requests.First().RequestId == original?.RequestId,
            "Recovery must retry the saved preparation instead of creating another operation.");
        Assert(shared.ZenoRoutePayload.State?.Route?.Stage == ZenoRouteStage.WaitingInterval &&
               shared.ZenoRoutePayload.State.PendingOperation is null,
            "Completed recovery must synchronize the committed target into shared run state.");
    }

    private static void UnknownResultFreezesAndRetriesInsideTheSameRuntime()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Unknown,
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(shared, Catalog, adapter, out ZenoRoutePersistenceRuntime? runtime),
            "The runtime fixture should be valid.");

        ZenoRoutePersistenceCoordinationResult frozen = runtime!.ExecuteAsync(PrepareSwitch(initial))
            .GetAwaiter().GetResult();
        Assert(frozen.IsFrozen && runtime.PendingRequestId == frozen.RequestId,
            "An unknown save must remain owned by the same runtime as a frozen retry.");
        Assert(shared.ZenoRoutePayload.State?.PendingOperation is not null,
            "The shared state must retain the frozen write-ahead record.");

        ZenoRoutePersistenceCoordinationResult completed = runtime.RetryAsync().GetAwaiter().GetResult();
        Assert(completed.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
               adapter.Requests[1].RequestId == frozen.RequestId,
            "The runtime retry must reuse the frozen request identity.");
    }

    private static void ReleasedPreparationRestoresTheSharedSourceState()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        ScriptedAdapter adapter = new(ZenoRoutePersistenceStatus.Failed);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(shared, Catalog, adapter, out ZenoRoutePersistenceRuntime? runtime),
            "The runtime fixture should be valid.");

        ZenoRoutePersistenceCoordinationResult released = runtime!.ExecuteAsync(PrepareSwitch(initial))
            .GetAwaiter().GetResult();
        Assert(released.Status == ZenoRoutePersistenceCoordinationStatus.PreparationReleased &&
               shared.ZenoRoutePayload.State?.Route?.Stage == ZenoRouteStage.Unresolved &&
               shared.ZenoRoutePayload.State.PendingOperation is null,
            "Confirmed non-persistence must restore the shared source state, not leave a prepared shadow.");
    }

    private static void InvalidOrMissingRoutesDoNotCreateRuntime()
    {
        PhilosophyRunState legacy = new();
        Assert(!ZenoRoutePersistenceRuntime.TryCreate(
                legacy,
                Catalog,
                new ScriptedAdapter(),
                out _),
            "A pre-feature run must not receive a Zeno runtime.");

        PhilosophyRunState notStarted = Shared(
            new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, null));
        Assert(!ZenoRoutePersistenceRuntime.TryCreate(
                notStarted,
                Catalog,
                new ScriptedAdapter(),
                out _),
            "A current payload without a Zeno route must wait for the business entry transition.");
    }

    private static void RuntimeCatalogRecoversAValidMaterialMarker()
    {
        ZenoRouteTransitionResult prepared = PrepareSwitch(CreateUnresolved());
        PhilosophyRunState encodedState = Shared(prepared.State);
        IReadOnlyList<string> entries =
            PhilosophyRunStateMarkerCarrier.GetEntriesForSave(encodedState);
        PhilosophyRunStateMarkerRestoreResult emptyCatalogRestore =
            PhilosophyRunStateMarkerCarrier.Restore(entries);
        Assert(emptyCatalogRestore.Classification == PhilosophyRunStateMarkerClassification.InvalidCurrent &&
               emptyCatalogRestore.State is not null,
            "The early load patch should isolate a material registry it cannot yet validate.");

        Assert(ZenoRoutePersistenceRuntime.TryRecoverIsolatedSharedState(
                emptyCatalogRestore.State!,
                Catalog,
                out PhilosophyRunState? recovered) &&
               recovered?.ZenoRoutePayload.State?.PendingOperation is not null,
            "The per-run production catalog should recover the preserved marker without rewriting it.");
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                recovered!,
                Catalog,
                new ScriptedAdapter(
                    ZenoRoutePersistenceStatus.Confirmed,
                    ZenoRoutePersistenceStatus.Confirmed),
                out ZenoRoutePersistenceRuntime? runtime) &&
               runtime?.PendingCheckpoint == ZenoRoutePersistenceCheckpoint.Prepared,
            "Recovered material state should restore the same prepared runtime checkpoint.");
    }

    private static PhilosophyRunState Shared(ZenoRouteFeatureState state)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(state, Catalog);
        return shared;
    }

    private static ZenoRouteTransitionResult PrepareSwitch(ZenoRouteFeatureState state)
    {
        return ZenoRouteStateService.PrepareSwitch(
            state,
            state.Route!.Revision,
            ZenoRouteStateCodec.CreateMaterialSnapshot(
                1,
                SourceKind,
                ActionFact,
                false,
                [CurrentStatement],
                CurrentStatement,
                NarrowStatement,
                null),
            Catalog);
    }

    private static ZenoRouteFeatureState CreateUnresolved()
    {
        return new ZenoRouteFeatureState(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_RUNTIME_001",
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
                null));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ScriptedAdapter(params ZenoRoutePersistenceStatus[] statuses)
        : IZenoRoutePersistenceConfirmationAdapter
    {
        private readonly Queue<ZenoRoutePersistenceStatus> _statuses = new(statuses);

        public List<ZenoRoutePersistenceRequest> Requests { get; } = [];

        public Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            ZenoRoutePersistenceStatus status = _statuses.Count > 0
                ? _statuses.Dequeue()
                : throw new InvalidOperationException("No scripted persistence result remains.");
            return Task.FromResult(new ZenoRoutePersistenceResult(request.RequestId, status));
        }
    }
}
