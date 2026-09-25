namespace STS2Philosophers;

internal static class ZenoAssentBoundaryEstablishmentChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string EventInstanceId = "ZENO_EVENT_ESTABLISH_001";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [],
        []);

    public static void Run()
    {
        ConfirmationMakesTheSameInstanceReady();
        PreparationFailureReleasesWithoutReplacingTheInstance();
        UnknownPersistenceFreezesBeforeRoomEntry();
        WrongCatalogAndConflictingIdentityAreRejected();
        Console.WriteLine("Zeno event establishment checks passed: two confirmed saves gate room readiness and failures never expose an event.");
    }

    private static void ConfirmationMakesTheSameInstanceReady()
    {
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(adapter);
        ZenoAssentBoundaryCreationCache<object> cache = new();
        int creations = 0;

        ZenoAssentBoundaryEstablishmentResult<object> first = Execute(
            runtime,
            cache,
            _ =>
            {
                creations++;
                return new object();
            });
        ZenoAssentBoundaryEstablishmentResult<object> duplicate = Execute(
            runtime,
            cache,
            _ => throw new InvalidOperationException("A ready duplicate must not construct again."));

        Assert(first.IsReady && duplicate.IsReady &&
               ReferenceEquals(first.RouteEvent, duplicate.RouteEvent) &&
               creations == 1,
            "A fully confirmed event must expose one stable mutable instance on duplicate callbacks.");
        Assert(adapter.Requests.Select(request => request.Checkpoint).SequenceEqual(
                [ZenoRoutePersistenceCheckpoint.Prepared, ZenoRoutePersistenceCheckpoint.Committed]),
            "Event establishment must persist its write-ahead and committed checkpoints in order exactly once.");
        Assert(runtime.CurrentState is
               {
                   PendingOperation: null,
                   Route.Stage: ZenoRouteStage.EventActive,
               } && runtime.PendingRequestId is null,
            "Room readiness requires a stable EventActive route with no frozen persistence request.");
    }

    private static void PreparationFailureReleasesWithoutReplacingTheInstance()
    {
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Failed,
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(adapter);
        ZenoAssentBoundaryCreationCache<object> cache = new();
        int creations = 0;

        ZenoAssentBoundaryEstablishmentResult<object> failed = Execute(
            runtime,
            cache,
            _ =>
            {
                creations++;
                return new object();
            });
        ZenoAssentBoundaryEstablishmentResult<object> retried = Execute(
            runtime,
            cache,
            _ => throw new InvalidOperationException("A released retry must reuse the prepared model."));

        Assert(failed.Status == ZenoAssentBoundaryEstablishmentStatus.PreparationReleased &&
               failed.RouteEvent is null &&
               retried.IsReady &&
               creations == 1,
            "A proven preparation failure must expose nothing and a later retry must reuse the same instance.");
    }

    private static void UnknownPersistenceFreezesBeforeRoomEntry()
    {
        ScriptedAdapter preparedUnknown = new(ZenoRoutePersistenceStatus.Unknown);
        ZenoRoutePersistenceRuntime preparedRuntime = CreateRuntime(preparedUnknown);
        ZenoAssentBoundaryCreationCache<object> preparedCache = new();
        ZenoAssentBoundaryEstablishmentResult<object> preparedResult = Execute(
            preparedRuntime,
            preparedCache,
            _ => new object());
        ZenoAssentBoundaryEstablishmentResult<object> preparedDuplicate = Execute(
            preparedRuntime,
            preparedCache,
            _ => throw new InvalidOperationException("Frozen establishment must not construct again."));

        Assert(preparedResult.Status == ZenoAssentBoundaryEstablishmentStatus.Frozen &&
               preparedDuplicate.Status == ZenoAssentBoundaryEstablishmentStatus.Frozen &&
               preparedResult.RouteEvent is null &&
               preparedRuntime.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventEstablished,
            "An unknown prepared save must retain its operation and never expose the event to a room entry.");

        ScriptedAdapter committedUnknown = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Unknown);
        ZenoRoutePersistenceRuntime committedRuntime = CreateRuntime(committedUnknown);
        ZenoAssentBoundaryEstablishmentResult<object> committedResult = Execute(
            committedRuntime,
            new ZenoAssentBoundaryCreationCache<object>(),
            _ => new object());
        Assert(committedResult.Status == ZenoAssentBoundaryEstablishmentStatus.Frozen &&
               committedResult.RouteEvent is null &&
               committedRuntime.CurrentState.Route?.Stage == ZenoRouteStage.EventActive &&
               committedRuntime.PendingRequestId is not null,
            "An unknown committed save must not confuse in-memory EventActive with durable room readiness.");
    }

    private static void WrongCatalogAndConflictingIdentityAreRejected()
    {
        ScriptedAdapter adapter = new();
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(adapter);
        ZenoRouteValidationCatalog wrongCatalog = new(
            [new KeyValuePair<string, int>(SourceKind, 1)],
            [ActionFact],
            [],
            []);
        ZenoAssentBoundaryEstablishmentResult<object> wrongCatalogResult =
            ZenoAssentBoundaryEstablishment.ExecuteAsync(
                runtime,
                wrongCatalog,
                new ZenoAssentBoundaryCreationCache<object>(),
                _ => new object()).GetAwaiter().GetResult();
        Assert(wrongCatalogResult.Status == ZenoAssentBoundaryEstablishmentStatus.Rejected &&
               adapter.Requests.Count == 0,
            "A structurally similar but unowned validation catalog must not start persistence.");

        ZenoAssentBoundaryCreationCache<object> conflictingCache = new();
        Assert(conflictingCache.TryGetOrCreate(
                new ZenoAssentBoundaryCreationPlan("ANOTHER_ZENO_EVENT"),
                () => new object(),
                out _),
            "The conflict fixture must reserve another event identity.");
        ZenoAssentBoundaryEstablishmentResult<object> conflict = Execute(
            runtime,
            conflictingCache,
            _ => new object());
        Assert(conflict.Status == ZenoAssentBoundaryEstablishmentStatus.Rejected &&
               adapter.Requests.Count == 0,
            "A cached competing identity must block persistence and never expose either instance.");
    }

    private static ZenoAssentBoundaryEstablishmentResult<object> Execute(
        ZenoRoutePersistenceRuntime runtime,
        ZenoAssentBoundaryCreationCache<object> cache,
        Func<ZenoAssentBoundaryCreationPlan, object> create) =>
        ZenoAssentBoundaryEstablishment.ExecuteAsync(
            runtime,
            Catalog,
            cache,
            create).GetAwaiter().GetResult();

    private static ZenoRoutePersistenceRuntime CreateRuntime(ScriptedAdapter adapter)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(CreateOpening(), Catalog);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                adapter,
                out ZenoRoutePersistenceRuntime? runtime) && runtime is not null,
            "The confirmed opening fixture must create its one per-run runtime.");
        return runtime!;
    }

    private static ZenoRouteFeatureState CreateOpening()
    {
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [],
            null,
            null,
            null);
        ZenoRouteFeatureState unresolved = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260925_ESTABLISH_001",
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
        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(unresolved, 0, material, Catalog).State);
        return Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            waiting.Route!.Revision,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "ACT_THREE_BOSS_001",
            EventInstanceId,
            Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected a prepared route operation.");
        return ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog).State;
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
