namespace STS2Philosophers;

internal static class ZenoRouteStateService
{
    public static ZenoRouteTransitionResult PrepareStay(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.Stay,
            ZenoRouteStage.Unresolved,
            ZenoRouteStage.DiogenesClosed,
            route => Advance(route, ZenoRouteStage.DiogenesClosed),
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareSwitch(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoAssentMaterialSnapshot material,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.Switch,
            ZenoRouteStage.Unresolved,
            ZenoRouteStage.WaitingInterval,
            route => Advance(route, ZenoRouteStage.WaitingInterval) with { Material = material },
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareIntervalCompletion(
        ZenoRouteFeatureState state,
        long expectedRevision,
        string completedRoomReceiptId,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.IntervalCompleted,
            ZenoRouteStage.WaitingInterval,
            ZenoRouteStage.ReadyToClaim,
            route => Advance(route, ZenoRouteStage.ReadyToClaim) with
            {
                CompletedRoomReceiptId = completedRoomReceiptId,
            },
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareOpeningClaim(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoOpeningTrigger trigger,
        ZenoResumeDestination destination,
        string resumeDestinationId,
        string eventInstanceId,
        ZenoRouteValidationCatalog catalog)
    {
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidFeature);
        }

        if (state.Route is null)
        {
            return Reject(state, ZenoRouteTransitionFailure.RouteMissing);
        }

        if (state.Route.Stage is not (ZenoRouteStage.WaitingInterval or ZenoRouteStage.ReadyToClaim))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidSourceStage);
        }

        ZenoRouteStage sourceStage = state.Route.Stage;
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.OpeningClaimed,
            sourceStage,
            ZenoRouteStage.OpeningClaimed,
            route => Advance(route, ZenoRouteStage.OpeningClaimed) with
            {
                OpeningTrigger = trigger,
                ResumeDestination = destination,
                ResumeDestinationId = resumeDestinationId,
                EventInstanceId = eventInstanceId,
            },
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareEventEstablished(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.EventEstablished,
            ZenoRouteStage.OpeningClaimed,
            ZenoRouteStage.EventActive,
            route => Advance(route, ZenoRouteStage.EventActive),
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareRunTermination(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoRouteValidationCatalog catalog)
    {
        if (state.Route?.Stage is not (ZenoRouteStage.WaitingInterval or ZenoRouteStage.ReadyToClaim))
        {
            return Reject(state, state.Route is null
                ? ZenoRouteTransitionFailure.RouteMissing
                : ZenoRouteTransitionFailure.InvalidSourceStage);
        }

        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.RunTerminated,
            state.Route.Stage,
            ZenoRouteStage.RunTerminated,
            route => Advance(route, ZenoRouteStage.RunTerminated),
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareOutcome(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoAssentOutcome outcome,
        string? currentStatementAfterId,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.OutcomeCommitted,
            ZenoRouteStage.EventActive,
            ZenoRouteStage.OutcomeCommitted,
            route => Advance(route, ZenoRouteStage.OutcomeCommitted) with
            {
                Outcome = outcome,
                CurrentStatementAfterId = currentStatementAfterId,
            },
            catalog);
    }

    public static ZenoRouteTransitionResult PrepareClose(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoRouteValidationCatalog catalog)
    {
        return Prepare(
            state,
            expectedRevision,
            ZenoRouteOperationKind.EventClosed,
            ZenoRouteStage.OutcomeCommitted,
            ZenoRouteStage.ZenoClosed,
            route => Advance(route, ZenoRouteStage.ZenoClosed),
            catalog);
    }

    public static ZenoRouteTransitionResult Commit(
        ZenoRouteFeatureState state,
        long operationId,
        string candidateDigest,
        ZenoRouteValidationCatalog catalog)
    {
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidFeature);
        }

        if (state.Route is null)
        {
            return Reject(state, ZenoRouteTransitionFailure.RouteMissing);
        }

        if (state.PendingOperation is null)
        {
            bool alreadyCommitted = state.Route.LastCommittedOperationId == operationId &&
                                    string.Equals(
                                        ZenoRouteStateCodec.ComputeCandidateDigest(state.Route),
                                        candidateDigest,
                                        StringComparison.Ordinal);
            return alreadyCommitted
                ? new ZenoRouteTransitionResult(
                    ZenoRouteTransitionStatus.CommitReused,
                    ZenoRouteTransitionFailure.None,
                    state)
                : Reject(state, ZenoRouteTransitionFailure.OperationMismatch);
        }

        ZenoRoutePendingOperation pending = state.PendingOperation;
        if (pending.OperationId != operationId ||
            !string.Equals(pending.CandidateDigest, candidateDigest, StringComparison.Ordinal))
        {
            return Reject(state, ZenoRouteTransitionFailure.OperationMismatch);
        }

        ZenoRouteFeatureState committed = state with
        {
            Route = pending.Candidate,
            PendingOperation = null,
        };
        if (!ZenoRouteStateCodec.IsValidFeature(committed, catalog))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidCandidate);
        }

        return new ZenoRouteTransitionResult(
            ZenoRouteTransitionStatus.Committed,
            ZenoRouteTransitionFailure.None,
            committed);
    }

    private static ZenoRouteTransitionResult Prepare(
        ZenoRouteFeatureState state,
        long expectedRevision,
        ZenoRouteOperationKind kind,
        ZenoRouteStage sourceStage,
        ZenoRouteStage targetStage,
        Func<ZenoRouteState, ZenoRouteState> createCandidate,
        ZenoRouteValidationCatalog catalog)
    {
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidFeature);
        }

        if (state.Route is null)
        {
            return Reject(state, ZenoRouteTransitionFailure.RouteMissing);
        }

        ZenoRouteState route = state.Route;
        if (route.Revision != expectedRevision)
        {
            return Reject(state, ZenoRouteTransitionFailure.StaleRevision);
        }

        if (route.Stage != sourceStage)
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidSourceStage);
        }

        if (route.Revision == long.MaxValue || route.LastCommittedOperationId == long.MaxValue)
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidCandidate);
        }

        ZenoRouteState candidate = createCandidate(route);
        string candidateDigest = ZenoRouteStateCodec.ComputeCandidateDigest(candidate);
        ZenoRoutePendingOperation pending = new(
            route.LastCommittedOperationId + 1,
            kind,
            route.Revision,
            sourceStage,
            targetStage,
            candidate,
            candidateDigest);

        if (state.PendingOperation is not null)
        {
            ZenoRoutePendingOperation existing = state.PendingOperation;
            bool sameOperation = existing.OperationId == pending.OperationId &&
                                 existing.Kind == pending.Kind &&
                                 existing.SourceRevision == pending.SourceRevision &&
                                 existing.SourceStage == pending.SourceStage &&
                                 existing.TargetStage == pending.TargetStage &&
                                 string.Equals(existing.CandidateDigest, pending.CandidateDigest, StringComparison.Ordinal);
            return sameOperation
                ? new ZenoRouteTransitionResult(
                    ZenoRouteTransitionStatus.PendingReused,
                    ZenoRouteTransitionFailure.None,
                    state)
                : Reject(state, ZenoRouteTransitionFailure.PendingConflict);
        }

        ZenoRouteFeatureState prepared = state with { PendingOperation = pending };
        if (!ZenoRouteStateCodec.IsValidFeature(prepared, catalog))
        {
            return Reject(state, ZenoRouteTransitionFailure.InvalidCandidate);
        }

        return new ZenoRouteTransitionResult(
            ZenoRouteTransitionStatus.Prepared,
            ZenoRouteTransitionFailure.None,
            prepared);
    }

    private static ZenoRouteState Advance(ZenoRouteState route, ZenoRouteStage targetStage)
    {
        return route with
        {
            Stage = targetStage,
            Revision = route.Revision + 1,
            LastCommittedOperationId = route.LastCommittedOperationId + 1,
        };
    }

    private static ZenoRouteTransitionResult Reject(
        ZenoRouteFeatureState state,
        ZenoRouteTransitionFailure failure)
    {
        return new ZenoRouteTransitionResult(ZenoRouteTransitionStatus.Rejected, failure, state);
    }
}
