namespace STS2Philosophers;

internal enum ZenoRouteRecoveryAction
{
    Isolate,
    RestoreDiogenesChoice,
    RetryPendingTransaction,
    WaitForNextRoom,
    WaitForSafeClaim,
    ResumeSameEvent,
    RestoreMaterialReview,
    RestoreCommittedOutcome,
    CompleteClosing,
    RemainTerminal,
}

internal enum ZenoRouteEventPresence
{
    None,
    Diogenes,
    MatchingZeno,
    Conflicting,
}

internal enum ZenoRouteDestinationPresence
{
    NotApplicable,
    Pending,
    Matching,
    Conflicting,
}

internal sealed record ZenoRouteRecoveryScene(
    ZenoRouteEventPresence EventPresence,
    ZenoRouteDestinationPresence DestinationPresence);

internal sealed record ZenoRouteRecoveryPlan(
    ZenoRouteRecoveryAction Action,
    long? OperationId = null,
    string? EventInstanceId = null,
    ZenoAssentOutcome? Outcome = null,
    ZenoResumeDestination? ResumeDestination = null,
    string? ResumeDestinationId = null);

internal static class ZenoRouteRecoveryPlanner
{
    private static readonly ZenoRouteRecoveryPlan Isolation = new(ZenoRouteRecoveryAction.Isolate);

    public static ZenoRouteRecoveryPlan CreatePlan(
        ZenoRouteFeatureState state,
        ZenoRouteRecoveryScene scene,
        ZenoRouteValidationCatalog catalog)
    {
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) || state.Route is null ||
            !Enum.IsDefined(scene.EventPresence) || !Enum.IsDefined(scene.DestinationPresence) ||
            scene.EventPresence == ZenoRouteEventPresence.Conflicting ||
            scene.DestinationPresence == ZenoRouteDestinationPresence.Conflicting)
        {
            return Isolation;
        }

        return state.PendingOperation is null
            ? PlanStable(state.Route, scene)
            : PlanPending(state.PendingOperation, scene);
    }

    private static ZenoRouteRecoveryPlan PlanStable(
        ZenoRouteState route,
        ZenoRouteRecoveryScene scene)
    {
        return route.Stage switch
        {
            ZenoRouteStage.Unresolved when
                scene.EventPresence == ZenoRouteEventPresence.Diogenes &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable =>
                Create(ZenoRouteRecoveryAction.RestoreDiogenesChoice, route),

            ZenoRouteStage.DiogenesClosed when
                scene.EventPresence is ZenoRouteEventPresence.None or ZenoRouteEventPresence.Diogenes &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable =>
                Create(ZenoRouteRecoveryAction.RemainTerminal, route),

            ZenoRouteStage.WaitingInterval when
                scene.EventPresence is ZenoRouteEventPresence.None or ZenoRouteEventPresence.Diogenes &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable =>
                Create(ZenoRouteRecoveryAction.WaitForNextRoom, route),

            ZenoRouteStage.ReadyToClaim when
                scene.EventPresence == ZenoRouteEventPresence.None &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable =>
                Create(ZenoRouteRecoveryAction.WaitForSafeClaim, route),

            ZenoRouteStage.OpeningClaimed when IsPausedZenoScene(scene) =>
                Create(ZenoRouteRecoveryAction.ResumeSameEvent, route),

            ZenoRouteStage.EventActive when IsPausedZenoScene(scene) =>
                Create(ZenoRouteRecoveryAction.RestoreMaterialReview, route),

            ZenoRouteStage.OutcomeCommitted when IsPausedZenoScene(scene) =>
                Create(ZenoRouteRecoveryAction.RestoreCommittedOutcome, route),

            ZenoRouteStage.ZenoClosed when
                scene.EventPresence == ZenoRouteEventPresence.None &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.Matching =>
                Create(ZenoRouteRecoveryAction.RemainTerminal, route),

            ZenoRouteStage.RunTerminated when
                scene.EventPresence == ZenoRouteEventPresence.None &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable =>
                Create(ZenoRouteRecoveryAction.RemainTerminal, route),

            _ => Isolation,
        };
    }

    private static ZenoRouteRecoveryPlan PlanPending(
        ZenoRoutePendingOperation pending,
        ZenoRouteRecoveryScene scene)
    {
        ZenoRouteState candidate = pending.Candidate;
        bool canRetry = pending.Kind switch
        {
            ZenoRouteOperationKind.Stay or ZenoRouteOperationKind.Switch =>
                scene.EventPresence == ZenoRouteEventPresence.Diogenes &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable,

            ZenoRouteOperationKind.IntervalCompleted =>
                scene.EventPresence == ZenoRouteEventPresence.None &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.NotApplicable,

            ZenoRouteOperationKind.OpeningClaimed =>
                scene.EventPresence == ZenoRouteEventPresence.None &&
                scene.DestinationPresence == ZenoRouteDestinationPresence.Pending,

            ZenoRouteOperationKind.EventEstablished or ZenoRouteOperationKind.OutcomeCommitted =>
                IsPausedZenoScene(scene),

            _ => false,
        };

        if (canRetry)
        {
            return Create(ZenoRouteRecoveryAction.RetryPendingTransaction, candidate, pending.OperationId);
        }

        if (pending.Kind == ZenoRouteOperationKind.EventClosed && IsValidClosingScene(scene))
        {
            return Create(ZenoRouteRecoveryAction.CompleteClosing, candidate, pending.OperationId);
        }

        return Isolation;
    }

    private static bool IsPausedZenoScene(ZenoRouteRecoveryScene scene) =>
        scene.EventPresence is ZenoRouteEventPresence.None or ZenoRouteEventPresence.MatchingZeno &&
        scene.DestinationPresence == ZenoRouteDestinationPresence.Pending;

    private static bool IsValidClosingScene(ZenoRouteRecoveryScene scene) =>
        (scene.EventPresence == ZenoRouteEventPresence.MatchingZeno &&
         scene.DestinationPresence == ZenoRouteDestinationPresence.Pending) ||
        (scene.EventPresence == ZenoRouteEventPresence.None &&
         scene.DestinationPresence is ZenoRouteDestinationPresence.Pending or ZenoRouteDestinationPresence.Matching);

    private static ZenoRouteRecoveryPlan Create(
        ZenoRouteRecoveryAction action,
        ZenoRouteState route,
        long? operationId = null) =>
        new(
            action,
            operationId,
            route.EventInstanceId,
            route.Outcome,
            route.ResumeDestination,
            route.ResumeDestinationId);
}
