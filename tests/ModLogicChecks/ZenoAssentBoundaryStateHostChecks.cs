namespace STS2Philosophers;

internal static class ZenoAssentBoundaryStateHostChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string EventInstanceId = "ZENO_EVENT_HOST_001";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement, NarrowStatement],
        []);

    public static void Run()
    {
        EveryLegalOutcomeUsesThePersistenceRuntime();
        FailedAndUnknownPersistenceNeverShowSuccess();
        EventIdentityAndOutcomeLegalityStayStrict();
        DuplicateConfirmationReadsTheCommittedWinner();
        Console.WriteLine("Zeno page state host checks passed: five persisted outcomes, strict identity and non-success freeze handling.");
    }

    private static void EveryLegalOutcomeUsesThePersistenceRuntime()
    {
        (ZenoAssentOutcome Outcome, ZenoAssentMaterialSnapshot Material, string? StatementAfter)[] cases =
        [
            (ZenoAssentOutcome.Keep, CreateMaterial(), CurrentStatement),
            (ZenoAssentOutcome.Narrow, CreateMaterial(), NarrowStatement),
            (ZenoAssentOutcome.Withdraw, CreateMaterial(), null),
            (ZenoAssentOutcome.NoReassent, CreateActionWithoutStatement(), null),
            (ZenoAssentOutcome.NoAssent, CreateNoMaterial(), null),
        ];

        foreach ((ZenoAssentOutcome outcome, ZenoAssentMaterialSnapshot material, string? statementAfter) in cases)
        {
            ScriptedAdapter adapter = new(
                ZenoRoutePersistenceStatus.Confirmed,
                ZenoRoutePersistenceStatus.Confirmed);
            ZenoAssentBoundaryStateHost host = CreateHost(material, adapter, out _);

            bool committed = host.CommitOutcomeAsync(outcome).GetAwaiter().GetResult();

            Assert(committed, $"{outcome} must report success only after both persistence checkpoints confirm.");
            Assert(adapter.Requests.Select(request => request.Checkpoint).SequenceEqual(
                    [ZenoRoutePersistenceCheckpoint.Prepared, ZenoRoutePersistenceCheckpoint.Committed]),
                $"{outcome} must use the existing write-ahead and committed checkpoints in order.");
            Assert(host.CurrentState.PendingOperation is null &&
                   host.CurrentState.Route is { Stage: ZenoRouteStage.OutcomeCommitted } route &&
                   route.Outcome == outcome &&
                   route.CurrentStatementAfterId == statementAfter,
                $"{outcome} must leave exactly its approved current statement in the committed route.");
        }
    }

    private static void FailedAndUnknownPersistenceNeverShowSuccess()
    {
        ScriptedAdapter failedAdapter = new(ZenoRoutePersistenceStatus.Failed);
        ZenoAssentBoundaryStateHost failedHost = CreateHost(
            CreateMaterial(),
            failedAdapter,
            out _);
        Assert(!failedHost.CommitOutcomeAsync(ZenoAssentOutcome.Keep).GetAwaiter().GetResult(),
            "A preparation proven not persisted must keep the confirmation page from claiming success.");
        Assert(failedHost.CurrentState.Route?.Stage == ZenoRouteStage.EventActive &&
               failedHost.CurrentState.PendingOperation is null,
            "A released preparation must restore the active event source state.");

        ScriptedAdapter unknownAdapter = new(ZenoRoutePersistenceStatus.Unknown);
        ZenoAssentBoundaryStateHost unknownHost = CreateHost(
            CreateMaterial(),
            unknownAdapter,
            out _);
        Assert(!unknownHost.CommitOutcomeAsync(ZenoAssentOutcome.Keep).GetAwaiter().GetResult(),
            "An unknown persistence result must not enter Zr.");
        Assert(unknownHost.CurrentState.PendingOperation is not null,
            "An unknown result must retain its write-ahead record for the recovery boundary.");
        Assert(!unknownHost.CommitOutcomeAsync(ZenoAssentOutcome.Keep).GetAwaiter().GetResult() &&
               unknownAdapter.Requests.Count == 1,
            "Repeated page confirmation must not bypass or silently retry a frozen request.");
    }

    private static void EventIdentityAndOutcomeLegalityStayStrict()
    {
        ScriptedAdapter wrongIdentityAdapter = new();
        ZenoAssentBoundaryStateHost wrongIdentity = CreateHost(
            CreateMaterial(),
            wrongIdentityAdapter,
            out _,
            "ANOTHER_ZENO_EVENT");
        Assert(!wrongIdentity.CommitOutcomeAsync(ZenoAssentOutcome.Keep).GetAwaiter().GetResult() &&
               wrongIdentityAdapter.Requests.Count == 0,
            "A page host for another event instance must not write this route.");

        ScriptedAdapter illegalAdapter = new();
        ZenoAssentBoundaryStateHost illegal = CreateHost(
            CreateActionWithoutStatement(),
            illegalAdapter,
            out _);
        Assert(!illegal.CommitOutcomeAsync(ZenoAssentOutcome.Narrow).GetAwaiter().GetResult() &&
               illegalAdapter.Requests.Count == 0,
            "The host must reuse strict route validation instead of persisting an illegal visible outcome.");
    }

    private static void DuplicateConfirmationReadsTheCommittedWinner()
    {
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoAssentBoundaryStateHost host = CreateHost(CreateMaterial(), adapter, out _);

        Assert(host.CommitOutcomeAsync(ZenoAssentOutcome.Withdraw).GetAwaiter().GetResult(),
            "The first confirmation should commit its winner.");
        Assert(host.CommitOutcomeAsync(ZenoAssentOutcome.Withdraw).GetAwaiter().GetResult() &&
               adapter.Requests.Count == 2,
            "A duplicate callback for the committed winner must reuse it without another save.");
        Assert(!host.CommitOutcomeAsync(ZenoAssentOutcome.Keep).GetAwaiter().GetResult() &&
               host.CurrentState.Route?.Outcome == ZenoAssentOutcome.Withdraw,
            "A later competing outcome must not replace the committed winner.");
    }

    private static ZenoAssentBoundaryStateHost CreateHost(
        ZenoAssentMaterialSnapshot material,
        ScriptedAdapter adapter,
        out ZenoRoutePersistenceRuntime runtime,
        string hostEventInstanceId = EventInstanceId)
    {
        ZenoRouteFeatureState active = CreateActive(material);
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(active, Catalog);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                adapter,
                out ZenoRoutePersistenceRuntime? created) && created is not null,
            "The active route fixture must create a persistence runtime.");
        runtime = created!;
        return new ZenoAssentBoundaryStateHost(runtime, Catalog, hostEventInstanceId);
    }

    private static ZenoRouteFeatureState CreateActive(ZenoAssentMaterialSnapshot material)
    {
        ZenoRouteFeatureState unresolved = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260925_HOST_001",
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
        ZenoRouteFeatureState opening = Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "ACT_THREE_BOSS_001",
            EventInstanceId,
            Catalog).State);
        return Commit(ZenoRouteStateService.PrepareEventEstablished(opening, 2, Catalog).State);
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

    private static ZenoAssentMaterialSnapshot CreateActionWithoutStatement() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [],
            null,
            null,
            null);

    private static ZenoAssentMaterialSnapshot CreateNoMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            null,
            true,
            [],
            null,
            null,
            null);

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
