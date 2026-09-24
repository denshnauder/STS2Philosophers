namespace STS2Philosophers;

internal static class ZenoRoutePersistenceAdapterChecks
{
    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>("SOCRATES_COMMITMENT", 1)],
        ["ACTION_BLOCK"],
        ["STATEMENT_WIDE", "STATEMENT_NARROW"],
        ["OUTCOME_SUCCESS"]);

    public static void Run()
    {
        ExactAuthoritativeReadBackConfirms();
        DuplicateAndMismatchedReadBackRemainUnknown();
        ReadAndSaveFailuresRemainUnknown();
        RejectedBindingAndEarlyCancellationDoNotSave();
        MismatchedRequestDoesNotSave();
        BoundCatalogReachesTheSharedMarkerCarrier();
        Console.WriteLine("Zeno route persistence adapter checks passed.");
    }

    private static void ExactAuthoritativeReadBackConfirms()
    {
        (ZenoRouteFeatureState state, ZenoRoutePersistenceRequest request) = CreatePrepared();
        FakeGateway gateway = new();
        gateway.ReadBack = CurrentMarker(state);
        ZenoRoutePersistenceResult result = new ZenoRoutePersistenceAdapter(gateway, Catalog)
            .PersistAsync(request, state).GetAwaiter().GetResult();

        Assert(result.Status == ZenoRoutePersistenceStatus.Confirmed,
            "One exact authoritative marker must confirm persistence.");
        Assert(gateway.BoundState == state && gateway.SaveCalls == 1 && gateway.ReadCalls == 1,
            "The adapter must bind first, call one save, then perform one readback.");
    }

    private static void DuplicateAndMismatchedReadBackRemainUnknown()
    {
        (ZenoRouteFeatureState state, ZenoRoutePersistenceRequest request) = CreatePrepared();
        string entry = CurrentMarker(state).MarkerEntries.Single();

        FakeGateway duplicate = new()
        {
            ReadBack = new ZenoRouteSaveReadBack(true, [entry, entry]),
        };
        Assert(new ZenoRoutePersistenceAdapter(duplicate, Catalog)
                .PersistAsync(request, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "Duplicate markers must not confirm persistence.");

        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            state,
            state.PendingOperation!.OperationId,
            state.PendingOperation.CandidateDigest,
            Catalog);
        FakeGateway mismatch = new() { ReadBack = CurrentMarker(committed.State) };
        Assert(new ZenoRoutePersistenceAdapter(mismatch, Catalog)
                .PersistAsync(request, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "A valid but different checkpoint must not confirm persistence.");
    }

    private static void ReadAndSaveFailuresRemainUnknown()
    {
        (ZenoRouteFeatureState state, ZenoRoutePersistenceRequest request) = CreatePrepared();
        FakeGateway unreadable = new() { ReadBack = ZenoRouteSaveReadBack.Unavailable };
        Assert(new ZenoRoutePersistenceAdapter(unreadable, Catalog)
                .PersistAsync(request, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "An unavailable authoritative read must remain unknown.");

        FakeGateway throwing = new() { ThrowOnSave = true };
        Assert(new ZenoRoutePersistenceAdapter(throwing, Catalog)
                .PersistAsync(request, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "A save exception cannot prove that nothing was persisted.");
        Assert(throwing.SaveCalls == 1 && throwing.ReadCalls == 0,
            "A failed save attempt must not fabricate a readback.");
    }

    private static void RejectedBindingAndEarlyCancellationDoNotSave()
    {
        (ZenoRouteFeatureState state, ZenoRoutePersistenceRequest request) = CreatePrepared();
        FakeGateway rejected = new() { BindAccepted = false };
        Assert(new ZenoRoutePersistenceAdapter(rejected, Catalog)
                .PersistAsync(request, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "A state that cannot be bound to the current run must remain unknown.");
        Assert(rejected.SaveCalls == 0 && rejected.ReadCalls == 0,
            "Rejected binding must not touch the save engine.");

        FakeGateway cancelled = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert(new ZenoRoutePersistenceAdapter(cancelled, Catalog)
                .PersistAsync(request, state, cancellation.Token).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "Cancellation before binding must remain unknown.");
        Assert(cancelled.BoundState is null && cancelled.SaveCalls == 0 && cancelled.ReadCalls == 0,
            "Early cancellation must not mutate the current run or save it.");
    }

    private static void MismatchedRequestDoesNotSave()
    {
        (ZenoRouteFeatureState state, ZenoRoutePersistenceRequest request) = CreatePrepared();
        FakeGateway gateway = new() { ReadBack = CurrentMarker(state) };
        ZenoRoutePersistenceRequest stale = request with { RequestId = "STALE_REQUEST" };
        Assert(new ZenoRoutePersistenceAdapter(gateway, Catalog)
                .PersistAsync(stale, state).GetAwaiter().GetResult().Status ==
                    ZenoRoutePersistenceStatus.Unknown,
            "A request that does not describe the supplied state must remain unknown.");
        Assert(gateway.BoundState is null && gateway.SaveCalls == 0 && gateway.ReadCalls == 0,
            "A mismatched request must be rejected before the run is mutated.");
    }

    private static void BoundCatalogReachesTheSharedMarkerCarrier()
    {
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            "SOCRATES_COMMITMENT",
            "ACTION_BLOCK",
            false,
            ["STATEMENT_WIDE"],
            "STATEMENT_WIDE",
            "STATEMENT_NARROW",
            "OUTCOME_SUCCESS");
        ZenoRouteFeatureState initial = CreateInitial();
        ZenoRouteTransitionResult prepared =
            ZenoRouteStateService.PrepareSwitch(initial, 0, material, Catalog);
        PhilosophyRunState runState = new();
        runState.SetCurrentZenoRouteState(prepared.State, Catalog);

        IReadOnlyList<string> entries = PhilosophyRunStateMarkerCarrier.GetEntriesForSave(runState);
        PhilosophyRunStateMarkerRestoreResult restored =
            PhilosophyRunStateMarkerCarrier.Restore(entries, Catalog);
        bool requestRestored = restored.State?.ZenoRoutePayload.State is { } restoredState &&
                               ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                                   restoredState,
                                   Catalog,
                                   out ZenoRoutePersistenceRequest? restoredRequest) &&
                               ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                                   prepared.State,
                                   Catalog,
                                   out ZenoRoutePersistenceRequest? preparedRequest) &&
                               restoredRequest?.RequestId == preparedRequest?.RequestId;
        Assert(restored.Classification == PhilosophyRunStateMarkerClassification.Current &&
               requestRestored,
            "The catalog bound with the current state must be used by the normal shared marker save path.");
    }

    private static ZenoRouteSaveReadBack CurrentMarker(ZenoRouteFeatureState state)
    {
        PhilosophyRunState runState = new();
        runState.SetCurrentZenoRouteState(state);
        return new ZenoRouteSaveReadBack(
            true,
            PhilosophyRunStateMarkerCarrier.GetEntriesForSave(runState, Catalog));
    }

    private static (ZenoRouteFeatureState State, ZenoRoutePersistenceRequest Request) CreatePrepared()
    {
        ZenoRouteFeatureState initial = CreateInitial();
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareStay(initial, 0, Catalog);
        Assert(prepared.Status == ZenoRouteTransitionStatus.Prepared,
            "Adapter fixture must prepare a valid checkpoint.");
        Assert(ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                prepared.State,
                Catalog,
                out ZenoRoutePersistenceRequest? request),
            "Adapter fixture must create a persistence request.");
        return (prepared.State, request!);
    }

    private static ZenoRouteFeatureState CreateInitial()
    {
        return new ZenoRouteFeatureState(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_ADAPTER",
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

    private sealed class FakeGateway : IZenoRouteSaveGateway
    {
        public bool BindAccepted { get; init; } = true;
        public bool ThrowOnSave { get; init; }
        public ZenoRouteSaveReadBack ReadBack { get; set; } = ZenoRouteSaveReadBack.Unavailable;
        public ZenoRouteFeatureState? BoundState { get; private set; }
        public int SaveCalls { get; private set; }
        public int ReadCalls { get; private set; }

        public bool TryBindState(ZenoRouteFeatureState state)
        {
            if (!BindAccepted)
            {
                return false;
            }

            BoundState = state;
            return true;
        }

        public Task SaveRunAsync()
        {
            SaveCalls++;
            return ThrowOnSave
                ? Task.FromException(new IOException("simulated save failure"))
                : Task.CompletedTask;
        }

        public ZenoRouteSaveReadBack ReadCurrentRunSave()
        {
            ReadCalls++;
            return ReadBack;
        }
    }
}
