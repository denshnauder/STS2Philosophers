using STS2Philosophers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class DiogenesStaySwitchEntryIntegrationChecks
{
    private static readonly ZenoRouteValidationCatalog Catalog = ZenoRouteValidationCatalogs.SaveRestore;

    public static void Run()
    {
        EntrySnapshotsRestoreBeforeChoice();
        StayNeverSchedulesZeno();
        SwitchWaitsForAnotherRoomAndKeepsOneMaterial();
        SavedPendingSwitchRetriesTheSameOperation();
        UnknownFinalCheckpointCannotBeConfirmedByAnotherClick();
        ClosedAndCrossRouteRecordsAreChecked();
        PendingSwitchCannotSubstituteAnotherFrozenMaterial();
        ProductionRoutesCannotRestoreWithoutTheirEntry();
        Console.WriteLine("Diogenes entry integration checks passed: shared restore, durable choice and delayed single scheduling.");
    }

    private static void EntrySnapshotsRestoreBeforeChoice()
    {
        foreach (SocratesVirtueAction action in new[]
                 { SocratesVirtueAction.None, SocratesVirtueAction.Continued, SocratesVirtueAction.Retreated })
        {
            PhilosophyRunState shared = CreateEntry(action);
            var entry = shared.DiogenesStaySwitchEntry!;
            PhilosophyRunState restored = RoundTrip(shared);
            Assert(restored.DiogenesStaySwitchEntry!.Material.Digest == entry.Material.Digest &&
                   restored.DiogenesStaySwitchEntry.Facts == entry.Facts &&
                   restored.DiogenesStaySwitchEntry.SourceRoomId == 17,
                "The pre-choice snapshot must recover the original immutable entry facts.");
            Assert(DiogenesStaySwitchPageFlow.TryRestore(restored.ZenoRoutePayload.State!, Catalog, out var page) &&
                   page.Page == DiogenesStaySwitchPage.RouteChoice,
                "A restored initial entry must return to the uncommitted route choice.");
            Assert(restored.WesternJourney!.CompletedEdgeIds.Count == 0 &&
                   restored.CurrentDoctrine!.DoctrineId == "SOCRATES_BALANCE_WEIGHT",
                "Loading the native entry snapshot cannot fill historical graph edges or replace the doctrine.");
        }
    }

    private static void StayNeverSchedulesZeno()
    {
        PhilosophyRunState shared = RoundTrip(CreateEntry(SocratesVirtueAction.Continued));
        RecordingAdapter adapter = new(shared);
        ZenoRoutePersistenceRuntime runtime = Runtime(shared, adapter);
        DiogenesStaySwitchStateHost host = Host(shared, runtime);
        Assert(host.CommitStayAsync().GetAwaiter().GetResult(), "Stay should commit its existing two checkpoints.");
        Assert(adapter.Requests.Count == 2 && host.CommitStayAsync().GetAwaiter().GetResult() &&
               adapter.Requests.Count == 2 && !host.CommitSwitchAsync().GetAwaiter().GetResult(),
            "Repeated Stay reuses its winner, while a later Switch cannot change it.");
        Assert(host.CurrentState.Route!.Material is null &&
               host.CurrentState.Route.Stage == ZenoRouteStage.DiogenesClosed,
            "Stay must not copy entry material into a Zeno reservation.");
        ZenoRouteSchedulingRuntime scheduler = new(runtime, Catalog);
        foreach (var context in new[]
                 { new ZenoRouteSchedulingContext(CompletedRoomReceiptId: "later_room", SafeBoundaryDestinationId: "map"),
                     new ZenoRouteSchedulingContext(BossDestinationId: "boss"),
                     new ZenoRouteSchedulingContext(ActExitDestinationId: "exit") })
        {
            Assert(scheduler.ExecuteAsync(context).GetAwaiter().GetResult().Status ==
                   ZenoRouteSchedulingExecutionStatus.Ignored,
                "Stay must ignore ordinary, Boss and act-exit Zeno claims.");
        }
        Assert(adapter.Requests.Count == 2, "Ignored scheduling must not write another transaction.");
    }

    private static void SwitchWaitsForAnotherRoomAndKeepsOneMaterial()
    {
        foreach (SocratesVirtueAction action in new[] { SocratesVirtueAction.None, SocratesVirtueAction.Retreated })
        {
            PhilosophyRunState shared = RoundTrip(CreateEntry(action));
            string digest = shared.DiogenesStaySwitchEntry!.Material.Digest;
            RecordingAdapter adapter = new(shared);
            ZenoRoutePersistenceRuntime runtime = Runtime(shared, adapter);
            DiogenesStaySwitchStateHost host = Host(shared, runtime);
            Assert(host.CommitSwitchAsync().GetAwaiter().GetResult() &&
                   host.CurrentState.Route!.Stage == ZenoRouteStage.WaitingInterval,
                "Switch must reserve a later event rather than claiming the opening in its own room.");
            Assert(host.CommitSwitchAsync().GetAwaiter().GetResult() && adapter.Requests.Count == 2 &&
                   !host.CommitStayAsync().GetAwaiter().GetResult(),
                "Duplicate confirmation must keep one material snapshot and the first route winner.");

            shared.DiogenesStaySwitchEntry = shared.DiogenesStaySwitchEntry with { Closed = true };
            PhilosophyRunState afterClose = RoundTrip(shared);
            Assert(afterClose.DiogenesStaySwitchEntry!.Closed &&
                   afterClose.ZenoRoutePayload.State!.Route!.Material!.Digest == digest,
                "The exit marker and reservation snapshot must survive the native saved boundary.");
            Assert(!ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                       new(2, 1, 17, true, true, ZenoRouteObservedEventKind.None), out _, 17),
                "The source MapRoom cannot supply a completed non-route room.");
            Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                       new(2, 1, 17, true, false, ZenoRouteObservedEventKind.None), out var sourcePlan, 17) &&
                   sourcePlan!.Context.CompletedRoomReceiptId is null,
                "Even a completed source room cannot be mistaken for the later interval room.");
            ZenoRouteSchedulingRuntime scheduler = new(runtime, Catalog);
            Assert(scheduler.ExecuteAsync(sourcePlan!.Context).GetAwaiter().GetResult().Status ==
                   ZenoRouteSchedulingExecutionStatus.Ignored && adapter.Requests.Count == 2,
                "Returning to the source boundary must leave the reservation waiting without a write.");

            Assert(ZenoRouteNativeTriggerPolicy.TryCreateMapPlan(
                       new(2, 1, 18, true, false, ZenoRouteObservedEventKind.Unrelated), out var laterPlan, 17),
                "A completed later non-route room should provide the normal claim boundary.");
            ZenoRouteSchedulingExecutionResult opened = scheduler.ExecuteAsync(laterPlan!.Context).GetAwaiter().GetResult();
            Assert(opened.Status == ZenoRouteSchedulingExecutionStatus.Completed &&
                   opened.State.Route!.Stage == ZenoRouteStage.OpeningClaimed &&
                   opened.State.Route.CompletedRoomReceiptId == "COMPLETED_ACT_2_ROOM_18" &&
                   opened.State.Route.Material!.Digest == digest,
                "The later room must complete the interval and claim exactly the frozen material.");
            int writes = adapter.Requests.Count;
            string eventId = opened.State.Route!.EventInstanceId!;
            var duplicate = scheduler.ExecuteAsync(laterPlan.Context).GetAwaiter().GetResult();
            Assert(duplicate.Status == ZenoRouteSchedulingExecutionStatus.ExistingClaim &&
                   duplicate.State.Route!.EventInstanceId == eventId && adapter.Requests.Count == writes,
                "Repeated map callbacks must retain the same event instance without another transaction.");
            Assert(RoundTrip(shared).ZenoRoutePayload.State!.Route!.Material!.Digest == digest,
                "The completed opening claim must still decode against the production catalog.");
        }
    }

    private static void SavedPendingSwitchRetriesTheSameOperation()
    {
        PhilosophyRunState shared = CreateEntry(SocratesVirtueAction.Continued);
        RecordingAdapter unknown = new(shared, ZenoRoutePersistenceStatus.Unknown);
        ZenoRoutePersistenceRuntime runtime = Runtime(shared, unknown);
        Assert(!Host(shared, runtime).CommitSwitchAsync().GetAwaiter().GetResult() &&
               runtime.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.Switch,
            "An unknown Switch save must preserve its prepared transaction and show no success.");
        string originalRequest = unknown.Requests.Single().RequestId;
        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(unknown.Snapshots.Single());
        RecordingAdapter confirmed = new(restored);
        ZenoRoutePersistenceRuntime recovered = Runtime(restored, confirmed);
        Assert(recovered.PendingRequestId == originalRequest &&
               recovered.RetryAsync().GetAwaiter().GetResult().Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
               confirmed.Requests.First().RequestId == originalRequest,
            "Native snapshot recovery must retry the original pending Switch identity.");
        Assert(DiogenesStaySwitchPageFlow.TryRestore(recovered.CurrentState, Catalog, out var result) &&
               result.Page == DiogenesStaySwitchPage.ResultSwitch &&
               recovered.CurrentState.Route!.Material!.Digest == restored.DiogenesStaySwitchEntry!.Material.Digest,
            "Recovery must display the original Switch result with the one frozen entry material.");
        int writes = confirmed.Requests.Count;
        Assert(Host(restored, recovered).CommitSwitchAsync().GetAwaiter().GetResult() &&
               confirmed.Requests.Count == writes,
            "A repeated confirmation after recovery must not save another Switch.");
    }

    private static void ClosedAndCrossRouteRecordsAreChecked()
    {
        PhilosophyRunState closed = CreateEntry(SocratesVirtueAction.None);
        RecordingAdapter adapter = new(closed);
        Assert(Host(closed, Runtime(closed, adapter)).CommitStayAsync().GetAwaiter().GetResult(), "Stay fixture should commit.");
        closed.DiogenesStaySwitchEntry = closed.DiogenesStaySwitchEntry! with { Closed = true };
        Assert(RoundTrip(closed).DiogenesStaySwitchEntry!.Closed, "A closed Stay result must round-trip.");
        PhilosophyRunState unresolved = CreateEntry(SocratesVirtueAction.None);
        unresolved.DiogenesStaySwitchEntry = unresolved.DiogenesStaySwitchEntry! with { Closed = true };
        AssertEncodeRejects(unresolved, "A closed marker cannot be paired with an uncommitted choice.");
        PhilosophyRunState foreign = CreateEntry(SocratesVirtueAction.None);
        foreign.SetCurrentZenoRouteState(foreign.ZenoRoutePayload.State! with
        { Route = foreign.ZenoRoutePayload.State.Route! with { RunId = "DIOGENES_SOCRATES_99" } }, Catalog);
        AssertEncodeRejects(foreign, "An entry cannot attach itself to another route identity.");
    }

    private static void UnknownFinalCheckpointCannotBeConfirmedByAnotherClick()
    {
        foreach (bool chooseSwitch in new[] { false, true })
        {
            PhilosophyRunState shared = CreateEntry(SocratesVirtueAction.Continued);
            RecordingAdapter adapter = new(shared,
                ZenoRoutePersistenceStatus.Confirmed, ZenoRoutePersistenceStatus.Unknown);
            ZenoRoutePersistenceRuntime runtime = Runtime(shared, adapter);
            DiogenesStaySwitchStateHost host = Host(shared, runtime);
            bool first = (chooseSwitch ? host.CommitSwitchAsync() : host.CommitStayAsync()).GetAwaiter().GetResult();
            Assert(!first && runtime.CurrentState.PendingOperation is null && runtime.PendingRequestId is not null &&
                   runtime.PendingCheckpoint == ZenoRoutePersistenceCheckpoint.Committed,
                "An unknown final checkpoint must withhold success even after the in-memory target stage exists.");
            string requestId = runtime.PendingRequestId!;
            bool repeated = (chooseSwitch ? host.CommitSwitchAsync() : host.CommitStayAsync()).GetAwaiter().GetResult();
            Assert(!repeated && adapter.Requests.Count == 2 && runtime.PendingRequestId == requestId,
                "Repeating the chosen button cannot confirm an unknown final save or bypass its explicit retry.");
            Assert(runtime.RetryAsync().GetAwaiter().GetResult().Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
                   adapter.Requests.Last().RequestId == requestId && runtime.PendingRequestId is null,
                "An explicit retry must finish the same final-checkpoint request.");
            int writes = adapter.Requests.Count;
            Assert((chooseSwitch ? host.CommitSwitchAsync() : host.CommitStayAsync()).GetAwaiter().GetResult() &&
                   adapter.Requests.Count == writes,
                "Only after confirmed retry may the chosen result be reused without another save.");
        }
    }

    private static void PendingSwitchCannotSubstituteAnotherFrozenMaterial()
    {
        PhilosophyRunState continued = CreateEntry(SocratesVirtueAction.Continued);
        PhilosophyRunState retreated = CreateEntry(SocratesVirtueAction.Retreated);
        ZenoRouteTransitionResult wrong = ZenoRouteStateService.PrepareSwitch(
            continued.ZenoRoutePayload.State!, 0, retreated.DiogenesStaySwitchEntry!.Material, Catalog);
        continued.SetCurrentZenoRouteState(wrong.State, Catalog);
        AssertEncodeRejects(continued, "A pending Switch cannot replace the entry's frozen material at save time.");

        retreated.SetCurrentZenoRouteState(ZenoRouteStateService.PrepareSwitch(
            retreated.ZenoRoutePayload.State!, 0, retreated.DiogenesStaySwitchEntry.Material, Catalog).State, Catalog);
        string valid = PhilosophyRunStateCodec.Encode(retreated);
        JsonObject root = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromHexString(valid)))!.AsObject();
        root["DiogenesStaySwitchEntry"] = JsonSerializer.SerializeToNode(CreateEntry(SocratesVirtueAction.Continued).DiogenesStaySwitchEntry);
        string mixed = Convert.ToHexString(Encoding.UTF8.GetBytes(root.ToJsonString()));
        AssertDecodeRejects(mixed, "A pending Switch with another valid entry snapshot must be rejected on restore.");
    }

    private static void ProductionRoutesCannotRestoreWithoutTheirEntry()
    {
        foreach (int stage in new[] { 0, 1, 2 })
        {
            PhilosophyRunState shared = CreateEntry(SocratesVirtueAction.Continued);
            ZenoRouteFeatureState initial = shared.ZenoRoutePayload.State!;
            if (stage != 0)
            {
                ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareSwitch(
                    initial, 0, shared.DiogenesStaySwitchEntry!.Material, Catalog);
                ZenoRouteFeatureState feature = prepared.State;
                if (stage == 2)
                {
                    ZenoRoutePendingOperation operation = feature.PendingOperation!;
                    feature = ZenoRouteStateService.Commit(feature, operation.OperationId, operation.CandidateDigest, Catalog).State;
                }
                shared.SetCurrentZenoRouteState(feature, Catalog);
            }

            string encoded = PhilosophyRunStateCodec.Encode(shared);
            JsonObject root = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromHexString(encoded)))!.AsObject();
            root.Remove("DiogenesStaySwitchEntry");
            AssertDecodeRejects(Convert.ToHexString(Encoding.UTF8.GetBytes(root.ToJsonString())),
                "An unresolved, pending or waiting production route must reject a missing entry on restore.");
            shared.DiogenesStaySwitchEntry = null;
            AssertEncodeRejects(shared,
                "An unresolved, pending or waiting production route cannot be saved without its entry identity.");
        }
    }

    private static PhilosophyRunState CreateEntry(SocratesVirtueAction action)
    {
        PhilosophyRunState shared = new()
        {
            CurrentDoctrine = new() { ThinkerId = "SOCRATES", DoctrineId = "SOCRATES_BALANCE_WEIGHT" },
            WesternJourney = new() { CurrentNodeId = SocratesVirtueUpstreamRecord.VirtueRouteNodeId, LastFixedAct = 1 },
        };
        if (action != SocratesVirtueAction.None)
        {
            shared.SocratesVirtueUpstream = SocratesVirtueUpstreamRecord.Open("real_opportunity", "real_combat", 1);
            shared.SocratesVirtueUpstream.Commit("real_opportunity", action, 7, 1);
        }
        Assert(DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, new(2, 1, true, false, 17), out var entry),
            "The integration fixture must meet the real production entry policy.");
        shared.DiogenesStaySwitchEntry = entry;
        shared.SetCurrentZenoRouteState(DiogenesStaySwitchEntryPolicy.CreateInitialState(entry!), Catalog);
        return shared;
    }

    private static PhilosophyRunState RoundTrip(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static ZenoRoutePersistenceRuntime Runtime(PhilosophyRunState shared, RecordingAdapter adapter)
    {
        Assert(ZenoRoutePersistenceRuntime.TryCreate(shared, Catalog, adapter, out var runtime) && runtime is not null,
            "A restored production entry must bind its persistence runtime.");
        return runtime!;
    }

    private static DiogenesStaySwitchStateHost Host(PhilosophyRunState shared, ZenoRoutePersistenceRuntime runtime) =>
        new(runtime, Catalog, shared.DiogenesStaySwitchEntry!.Material);

    private static void AssertEncodeRejects(PhilosophyRunState state, string message)
    {
        try { PhilosophyRunStateCodec.Encode(state); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void AssertDecodeRejects(string encoded, string message)
    {
        try { PhilosophyRunStateCodec.Decode(encoded); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RecordingAdapter(PhilosophyRunState shared, params ZenoRoutePersistenceStatus[] statuses)
        : IZenoRoutePersistenceConfirmationAdapter
    {
        private readonly Queue<ZenoRoutePersistenceStatus> _statuses = new(statuses);
        internal List<ZenoRoutePersistenceRequest> Requests { get; } = [];
        internal List<string> Snapshots { get; } = [];

        public Task<ZenoRoutePersistenceResult> PersistAsync(ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state, CancellationToken cancellationToken = default)
        {
            shared.SetCurrentZenoRouteState(state, Catalog);
            Requests.Add(request);
            Snapshots.Add(PhilosophyRunStateCodec.Encode(shared));
            return Task.FromResult(new ZenoRoutePersistenceResult(request.RequestId,
                _statuses.Count == 0 ? ZenoRoutePersistenceStatus.Confirmed : _statuses.Dequeue()));
        }
    }
}
