using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class ZenoRouteStateServiceChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string PriorStatement = "STATEMENT_PRIOR";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string LaterOutcome = "OUTCOME_PUBLIC";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [PriorStatement, CurrentStatement, NarrowStatement],
        [LaterOutcome]);

    public static void Run()
    {
        FullSwitchChainIsMonotonicAndIdempotent();
        StayAndBossFallbackUseTheirOnlyLegalTargets();
        StaleAndConflictingRequestsAreRejected();
        PendingPayloadIsStrictAndOpaqueToFailedCommits();
        Console.WriteLine("Zeno route state service checks passed: seven prepared operations, monotonic commits and idempotent retries.");
    }

    private static void FullSwitchChainIsMonotonicAndIdempotent()
    {
        ZenoRouteFeatureState state = CreateUnresolved();
        ZenoAssentMaterialSnapshot material = CreateMaterial();

        ZenoRouteTransitionResult switchPrepared = ZenoRouteStateService.PrepareSwitch(state, 0, material, Catalog);
        AssertPrepared(switchPrepared, ZenoRouteOperationKind.Switch, ZenoRouteStage.WaitingInterval);
        Assert(switchPrepared.State.Route?.Stage == ZenoRouteStage.Unresolved, "Preparing Switch must not overwrite the source route.");

        ZenoRouteTransitionResult repeatedPrepare = ZenoRouteStateService.PrepareSwitch(
            switchPrepared.State,
            0,
            material,
            Catalog);
        Assert(repeatedPrepare.Status == ZenoRouteTransitionStatus.PendingReused, "The same Switch callback should reuse its pending record.");
        state = Commit(switchPrepared.State);
        AssertStage(state, ZenoRouteStage.WaitingInterval, 1, 1);

        state = PrepareAndCommit(
            ZenoRouteStateService.PrepareIntervalCompletion(state, 1, "ROOM_RECEIPT_001", Catalog),
            ZenoRouteOperationKind.IntervalCompleted,
            ZenoRouteStage.ReadyToClaim);
        state = PrepareAndCommit(
            ZenoRouteStateService.PrepareOpeningClaim(
                state,
                2,
                ZenoOpeningTrigger.SafeBoundary,
                ZenoResumeDestination.Map,
                "MAP_AFTER_ROOM_001",
                "ZENO_EVENT_001",
                Catalog),
            ZenoRouteOperationKind.OpeningClaimed,
            ZenoRouteStage.OpeningClaimed);
        state = PrepareAndCommit(
            ZenoRouteStateService.PrepareEventEstablished(state, 3, Catalog),
            ZenoRouteOperationKind.EventEstablished,
            ZenoRouteStage.EventActive);
        state = PrepareAndCommit(
            ZenoRouteStateService.PrepareOutcome(
                state,
                4,
                ZenoAssentOutcome.Narrow,
                NarrowStatement,
                Catalog),
            ZenoRouteOperationKind.OutcomeCommitted,
            ZenoRouteStage.OutcomeCommitted);
        state = PrepareAndCommit(
            ZenoRouteStateService.PrepareClose(state, 5, Catalog),
            ZenoRouteOperationKind.EventClosed,
            ZenoRouteStage.ZenoClosed);

        AssertStage(state, ZenoRouteStage.ZenoClosed, 6, 6);
        Assert(state.Route?.Material?.Digest == material.Digest, "Every operation must preserve the frozen Switch material.");
        Assert(state.Route?.CompletedRoomReceiptId == "ROOM_RECEIPT_001", "Opening and closing must preserve the room receipt.");
        Assert(state.Route?.ResumeDestinationId == "MAP_AFTER_ROOM_001", "Closing must preserve the original destination credential.");
    }

    private static void StayAndBossFallbackUseTheirOnlyLegalTargets()
    {
        ZenoRouteFeatureState stay = PrepareAndCommit(
            ZenoRouteStateService.PrepareStay(CreateUnresolved(), 0, Catalog),
            ZenoRouteOperationKind.Stay,
            ZenoRouteStage.DiogenesClosed);
        AssertStage(stay, ZenoRouteStage.DiogenesClosed, 1, 1);

        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(CreateUnresolved(), 0, CreateMaterial(), Catalog).State);
        ZenoRouteTransitionResult safeBoundaryTooEarly = ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.SafeBoundary,
            ZenoResumeDestination.Map,
            "MAP_TOO_EARLY",
            "ZENO_EVENT_INVALID",
            Catalog);
        AssertRejected(safeBoundaryTooEarly, ZenoRouteTransitionFailure.InvalidCandidate);

        ZenoRouteFeatureState bossClaim = PrepareAndCommit(
            ZenoRouteStateService.PrepareOpeningClaim(
                waiting,
                1,
                ZenoOpeningTrigger.BeforeBoss,
                ZenoResumeDestination.Boss,
                "ACT_THREE_BOSS_001",
                "ZENO_EVENT_BOSS_001",
                Catalog),
            ZenoRouteOperationKind.OpeningClaimed,
            ZenoRouteStage.OpeningClaimed);
        Assert(bossClaim.Route?.CompletedRoomReceiptId is null, "Boss fallback must not invent a completed-room receipt.");
    }

    private static void StaleAndConflictingRequestsAreRejected()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoAssentMaterialSnapshot material = CreateMaterial();
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareSwitch(initial, 0, material, Catalog);

        ZenoRouteTransitionResult conflict = ZenoRouteStateService.PrepareStay(prepared.State, 0, Catalog);
        AssertRejected(conflict, ZenoRouteTransitionFailure.PendingConflict);
        Assert(conflict.State.PendingOperation?.Kind == ZenoRouteOperationKind.Switch, "A losing callback must not replace the winning pending operation.");

        ZenoRouteFeatureState waiting = Commit(prepared.State);
        AssertRejected(
            ZenoRouteStateService.PrepareIntervalCompletion(waiting, 0, "ROOM_STALE", Catalog),
            ZenoRouteTransitionFailure.StaleRevision);
        AssertRejected(
            ZenoRouteStateService.PrepareStay(waiting, 1, Catalog),
            ZenoRouteTransitionFailure.InvalidSourceStage);
    }

    private static void PendingPayloadIsStrictAndOpaqueToFailedCommits()
    {
        ZenoRouteTransitionResult prepared = ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog);
        ZenoRoutePendingOperation pending = prepared.State.PendingOperation ?? throw new InvalidOperationException("Expected pending operation.");

        string encoded = ZenoRouteStateCodec.Encode(prepared.State, Catalog);
        ZenoRouteDecodeResult decoded = ZenoRouteStateCodec.Decode(encoded, Catalog);
        Assert(decoded.Classification == ZenoRoutePayloadClassification.Current, "A valid pending operation should round-trip.");
        Assert(decoded.State?.PendingOperation?.CandidateDigest == pending.CandidateDigest, "Pending candidate digest should round-trip exactly.");

        JsonObject tampered = JsonNode.Parse(encoded)?.AsObject() ?? throw new InvalidOperationException("Expected JSON object.");
        tampered["zenoRoutePendingOperation"]!["candidateDigest"] = "00";
        ZenoRouteDecodeResult rejectedPayload = ZenoRouteStateCodec.Decode(tampered.ToJsonString(), Catalog);
        Assert(rejectedPayload.Classification == ZenoRoutePayloadClassification.Invalid, "A changed candidate digest must invalidate the pending payload.");

        ZenoRouteTransitionResult wrongCommit = ZenoRouteStateService.Commit(
            prepared.State,
            pending.OperationId,
            "00",
            Catalog);
        AssertRejected(wrongCommit, ZenoRouteTransitionFailure.OperationMismatch);
        Assert(wrongCommit.State.PendingOperation == pending, "A failed commit must retain the original pending operation.");

        ZenoRouteFeatureState committed = Commit(prepared.State);
        ZenoRouteTransitionResult repeatedCommit = ZenoRouteStateService.Commit(
            committed,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog);
        Assert(repeatedCommit.Status == ZenoRouteTransitionStatus.CommitReused, "A repeated confirmed commit should return the existing result.");
        Assert(ReferenceEquals(repeatedCommit.State, committed), "A repeated commit must not create another state revision.");
    }

    private static ZenoRouteFeatureState PrepareAndCommit(
        ZenoRouteTransitionResult prepared,
        ZenoRouteOperationKind kind,
        ZenoRouteStage targetStage)
    {
        AssertPrepared(prepared, kind, targetStage);
        return Commit(prepared.State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ?? throw new InvalidOperationException("Expected pending operation.");
        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog);
        Assert(committed.Status == ZenoRouteTransitionStatus.Committed, "A matching prepared operation should commit.");
        Assert(committed.State.PendingOperation is null, "Commit should clear only the matching pending operation.");
        return committed.State;
    }

    private static void AssertPrepared(
        ZenoRouteTransitionResult result,
        ZenoRouteOperationKind kind,
        ZenoRouteStage targetStage)
    {
        Assert(result.Status == ZenoRouteTransitionStatus.Prepared, $"{kind} should prepare.");
        Assert(result.State.PendingOperation?.Kind == kind, $"{kind} should retain its operation kind.");
        Assert(result.State.PendingOperation?.TargetStage == targetStage, $"{kind} should retain its only target stage.");
    }

    private static void AssertRejected(ZenoRouteTransitionResult result, ZenoRouteTransitionFailure failure)
    {
        Assert(result.Status == ZenoRouteTransitionStatus.Rejected, $"Expected rejection for {failure}.");
        Assert(result.Failure == failure, $"Expected {failure}, got {result.Failure}.");
    }

    private static void AssertStage(
        ZenoRouteFeatureState state,
        ZenoRouteStage stage,
        long revision,
        long operationId)
    {
        Assert(state.Route?.Stage == stage, $"Expected stage {stage}.");
        Assert(state.Route?.Revision == revision, $"Expected revision {revision}.");
        Assert(state.Route?.LastCommittedOperationId == operationId, $"Expected operation {operationId}.");
    }

    private static ZenoRouteFeatureState CreateUnresolved()
    {
        ZenoRouteState route = new(
            ZenoRouteState.CurrentVersion,
            ZenoRouteStage.Unresolved,
            0,
            0,
            "RUN_20260924_SERVICE_001",
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
            [PriorStatement, CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            LaterOutcome);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
