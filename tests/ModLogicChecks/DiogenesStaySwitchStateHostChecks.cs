using STS2Philosophers;

internal static class DiogenesStaySwitchStateHostChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement],
        []);

    public static void Run()
    {
        StayAndSwitchUseTheExistingPersistenceTransaction();
        DuplicateConfirmationKeepsTheFirstWinner();
        FailedOrUnknownPersistenceNeverShowsSuccess();
        Console.WriteLine("Diogenes stay/switch host checks passed: persisted route winner, idempotent confirmation and frozen failure handling.");
    }

    private static void StayAndSwitchUseTheExistingPersistenceTransaction()
    {
        ScriptedAdapter stayAdapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        DiogenesStaySwitchStateHost stayHost = CreateHost(stayAdapter);
        Assert(stayHost.CommitStayAsync().GetAwaiter().GetResult(),
            "Stay must succeed only after the prepared and committed checkpoints persist.");
        Assert(stayHost.CurrentState.Route?.Stage == ZenoRouteStage.DiogenesClosed &&
               stayHost.CurrentState.Route.Material is null,
            "Stay must close future switching without adding Zeno material.");
        AssertCheckpoints(stayAdapter, "Stay");

        ScriptedAdapter switchAdapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        DiogenesStaySwitchStateHost switchHost = CreateHost(switchAdapter);
        Assert(switchHost.CommitSwitchAsync().GetAwaiter().GetResult(),
            "Switch must succeed only after the existing persistence transaction confirms.");
        Assert(switchHost.CurrentState.Route is
               {
                   Stage: ZenoRouteStage.WaitingInterval,
                   Material: not null,
               },
            "Switch must stop at WaitingInterval and must not open the Zeno event page.");
        AssertCheckpoints(switchAdapter, "Switch");
    }

    private static void DuplicateConfirmationKeepsTheFirstWinner()
    {
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        DiogenesStaySwitchStateHost host = CreateHost(adapter);

        Assert(host.CommitSwitchAsync().GetAwaiter().GetResult(),
            "The first Switch confirmation should commit.");
        Assert(host.CommitSwitchAsync().GetAwaiter().GetResult() && adapter.Requests.Count == 2,
            "A duplicate callback for the committed winner must reuse it without another save.");
        Assert(!host.CommitStayAsync().GetAwaiter().GetResult() &&
               host.CurrentState.Route?.Stage == ZenoRouteStage.WaitingInterval,
            "A competing callback must not replace the committed route winner.");
    }

    private static void FailedOrUnknownPersistenceNeverShowsSuccess()
    {
        ScriptedAdapter failedAdapter = new(ZenoRoutePersistenceStatus.Failed);
        DiogenesStaySwitchStateHost failedHost = CreateHost(failedAdapter);
        Assert(!failedHost.CommitStayAsync().GetAwaiter().GetResult() &&
               failedHost.CurrentState.Route?.Stage == ZenoRouteStage.Unresolved &&
               failedHost.CurrentState.PendingOperation is null,
            "A preparation proven not persisted must return to the unresolved confirmation state.");

        ScriptedAdapter unknownAdapter = new(ZenoRoutePersistenceStatus.Unknown);
        DiogenesStaySwitchStateHost unknownHost = CreateHost(unknownAdapter);
        Assert(!unknownHost.CommitSwitchAsync().GetAwaiter().GetResult() &&
               unknownHost.CurrentState.PendingOperation is not null,
            "An unknown persistence result must retain its write-ahead record and show no success.");
        Assert(!unknownHost.CommitSwitchAsync().GetAwaiter().GetResult() &&
               unknownAdapter.Requests.Count == 1,
            "Repeated confirmation must not bypass or silently retry a frozen transaction.");
    }

    private static DiogenesStaySwitchStateHost CreateHost(ScriptedAdapter adapter)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(
            DiogenesStaySwitchPageFlowChecks.CreateUnresolved(),
            Catalog);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                adapter,
                out ZenoRoutePersistenceRuntime? runtime) &&
               runtime is not null,
            "The unresolved route fixture must create a persistence runtime.");
        return new DiogenesStaySwitchStateHost(
            runtime!,
            Catalog,
            DiogenesStaySwitchPageFlowChecks.CreateMaterial());
    }

    private static void AssertCheckpoints(ScriptedAdapter adapter, string operation)
    {
        Assert(adapter.Requests.Select(request => request.Checkpoint).SequenceEqual(
                [ZenoRoutePersistenceCheckpoint.Prepared, ZenoRoutePersistenceCheckpoint.Committed]),
            $"{operation} must use the write-ahead and committed checkpoints in order.");
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
