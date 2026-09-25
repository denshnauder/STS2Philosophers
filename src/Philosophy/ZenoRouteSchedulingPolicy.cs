using System.Security.Cryptography;
using System.Text;

namespace STS2Philosophers;

internal enum ZenoRouteSchedulingDecision
{
    Ignore,
    CompleteInterval,
    CompleteIntervalAndClaimOpening,
    ClaimOpening,
    TerminateRun,
    ExistingClaim,
    Frozen,
    Invalid,
}

internal sealed record ZenoRouteSchedulingContext(
    bool IsRunTerminated = false,
    string? CompletedRoomReceiptId = null,
    string? SafeBoundaryDestinationId = null,
    string? BossDestinationId = null,
    string? ActExitDestinationId = null);

internal sealed record ZenoRouteSchedulingPlan(
    ZenoRouteSchedulingDecision Decision,
    string? CompletedRoomReceiptId = null,
    ZenoOpeningTrigger? OpeningTrigger = null,
    ZenoResumeDestination? ResumeDestination = null,
    string? ResumeDestinationId = null,
    string? EventInstanceId = null);

internal enum ZenoRouteSchedulingExecutionStatus
{
    Ignored,
    Completed,
    ExistingClaim,
    Frozen,
    Rejected,
}

internal sealed record ZenoRouteSchedulingExecutionResult(
    ZenoRouteSchedulingExecutionStatus Status,
    ZenoRouteSchedulingPlan Plan,
    ZenoRouteFeatureState State,
    ZenoRoutePersistenceCoordinationResult? Persistence = null);

internal static class ZenoRouteSchedulingPolicy
{
    private const int MaxRuntimeTokenLength = 128;

    public static ZenoRouteSchedulingPlan CreatePlan(
        ZenoRouteFeatureState state,
        ZenoRouteSchedulingContext context,
        ZenoRouteValidationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) ||
            !HasValidTokens(context))
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Invalid);
        }

        if (state.PendingOperation is not null)
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Frozen);
        }

        if (state.Route is not { } route)
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Ignore);
        }

        if (route.Stage is ZenoRouteStage.OpeningClaimed or
            ZenoRouteStage.EventActive or
            ZenoRouteStage.OutcomeCommitted or
            ZenoRouteStage.ZenoClosed)
        {
            return ExistingClaim(route);
        }

        if (route.Stage == ZenoRouteStage.RunTerminated)
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Ignore);
        }

        if (route.Stage is not (ZenoRouteStage.WaitingInterval or ZenoRouteStage.ReadyToClaim))
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Ignore);
        }

        if (context.IsRunTerminated)
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.TerminateRun);
        }

        string eventInstanceId = CreateEventInstanceId(route);
        if (context.ActExitDestinationId is { } actExitDestinationId)
        {
            return Claim(
                ZenoOpeningTrigger.BeforeActExit,
                ZenoResumeDestination.ActExit,
                actExitDestinationId,
                eventInstanceId);
        }

        if (context.BossDestinationId is { } bossDestinationId)
        {
            return Claim(
                ZenoOpeningTrigger.BeforeBoss,
                ZenoResumeDestination.Boss,
                bossDestinationId,
                eventInstanceId);
        }

        if (route.Stage == ZenoRouteStage.ReadyToClaim)
        {
            if (context.CompletedRoomReceiptId is { } repeatedReceipt &&
                !string.Equals(repeatedReceipt, route.CompletedRoomReceiptId, StringComparison.Ordinal))
            {
                return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Invalid);
            }

            return context.SafeBoundaryDestinationId is { } readyDestinationId
                ? Claim(
                    ZenoOpeningTrigger.SafeBoundary,
                    ZenoResumeDestination.Map,
                    readyDestinationId,
                    eventInstanceId,
                    route.CompletedRoomReceiptId)
                : new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Ignore);
        }

        if (context.CompletedRoomReceiptId is not { } completedRoomReceiptId)
        {
            return new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Ignore);
        }

        return context.SafeBoundaryDestinationId is { } safeBoundaryDestinationId
            ? new ZenoRouteSchedulingPlan(
                ZenoRouteSchedulingDecision.CompleteIntervalAndClaimOpening,
                completedRoomReceiptId,
                ZenoOpeningTrigger.SafeBoundary,
                ZenoResumeDestination.Map,
                safeBoundaryDestinationId,
                eventInstanceId)
            : new ZenoRouteSchedulingPlan(
                ZenoRouteSchedulingDecision.CompleteInterval,
                completedRoomReceiptId);
    }

    internal static string CreateEventInstanceId(ZenoRouteState route)
    {
        string identity = string.Join(
            "|",
            "ZENO_ASSENT_BOUNDARY_V1",
            route.RunId,
            route.RouteEdgeId,
            route.TerminalNodeId);
        return $"ZENO_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}";
    }

    private static ZenoRouteSchedulingPlan Claim(
        ZenoOpeningTrigger trigger,
        ZenoResumeDestination destination,
        string destinationId,
        string eventInstanceId,
        string? completedRoomReceiptId = null) =>
        new(
            ZenoRouteSchedulingDecision.ClaimOpening,
            completedRoomReceiptId,
            trigger,
            destination,
            destinationId,
            eventInstanceId);

    private static ZenoRouteSchedulingPlan ExistingClaim(ZenoRouteState route) =>
        new(
            ZenoRouteSchedulingDecision.ExistingClaim,
            route.CompletedRoomReceiptId,
            route.OpeningTrigger,
            route.ResumeDestination,
            route.ResumeDestinationId,
            route.EventInstanceId);

    private static bool HasValidTokens(ZenoRouteSchedulingContext context) =>
        IsOptionalRuntimeToken(context.CompletedRoomReceiptId) &&
        IsOptionalRuntimeToken(context.SafeBoundaryDestinationId) &&
        IsOptionalRuntimeToken(context.BossDestinationId) &&
        IsOptionalRuntimeToken(context.ActExitDestinationId);

    private static bool IsOptionalRuntimeToken(string? value) =>
        value is null ||
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxRuntimeTokenLength &&
        value.All(character => !char.IsControl(character));
}

internal sealed class ZenoRouteSchedulingRuntime(
    ZenoRoutePersistenceRuntime runtime,
    ZenoRouteValidationCatalog catalog)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ZenoRouteSchedulingExecutionResult> ExecuteAsync(
        ZenoRouteSchedulingContext context,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!runtime.Owns(catalog))
            {
                return Result(
                    ZenoRouteSchedulingExecutionStatus.Rejected,
                    new ZenoRouteSchedulingPlan(ZenoRouteSchedulingDecision.Invalid));
            }

            ZenoRouteSchedulingPlan plan = ZenoRouteSchedulingPolicy.CreatePlan(
                runtime.CurrentState,
                context,
                catalog);
            return plan.Decision switch
            {
                ZenoRouteSchedulingDecision.Ignore =>
                    Result(ZenoRouteSchedulingExecutionStatus.Ignored, plan),
                ZenoRouteSchedulingDecision.ExistingClaim =>
                    Result(ZenoRouteSchedulingExecutionStatus.ExistingClaim, plan),
                ZenoRouteSchedulingDecision.Frozen =>
                    Result(ZenoRouteSchedulingExecutionStatus.Frozen, plan),
                ZenoRouteSchedulingDecision.Invalid =>
                    Result(ZenoRouteSchedulingExecutionStatus.Rejected, plan),
                ZenoRouteSchedulingDecision.CompleteInterval =>
                    await ExecuteIntervalCompletionAsync(plan, false, cancellationToken),
                ZenoRouteSchedulingDecision.CompleteIntervalAndClaimOpening =>
                    await ExecuteIntervalCompletionAsync(plan, true, cancellationToken),
                ZenoRouteSchedulingDecision.ClaimOpening =>
                    await ExecuteClaimAsync(plan, cancellationToken),
                ZenoRouteSchedulingDecision.TerminateRun =>
                    await ExecuteTerminationAsync(plan, cancellationToken),
                _ => Result(ZenoRouteSchedulingExecutionStatus.Rejected, plan),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ZenoRouteSchedulingExecutionResult> ExecuteIntervalCompletionAsync(
        ZenoRouteSchedulingPlan plan,
        bool continueToClaim,
        CancellationToken cancellationToken)
    {
        ZenoRouteState? route = runtime.CurrentState.Route;
        if (route is null || plan.CompletedRoomReceiptId is null)
        {
            return Result(ZenoRouteSchedulingExecutionStatus.Rejected, plan);
        }

        ZenoRoutePersistenceCoordinationResult interval = await runtime.ExecuteAsync(
            ZenoRouteStateService.PrepareIntervalCompletion(
                runtime.CurrentState,
                route.Revision,
                plan.CompletedRoomReceiptId,
                catalog),
            cancellationToken);
        if (interval.Status != ZenoRoutePersistenceCoordinationStatus.Completed)
        {
            return FromPersistence(plan, interval);
        }

        return continueToClaim
            ? await ExecuteClaimAsync(plan, cancellationToken)
            : Result(ZenoRouteSchedulingExecutionStatus.Completed, plan, interval);
    }

    private async Task<ZenoRouteSchedulingExecutionResult> ExecuteClaimAsync(
        ZenoRouteSchedulingPlan plan,
        CancellationToken cancellationToken)
    {
        ZenoRouteState? route = runtime.CurrentState.Route;
        if (route is null ||
            plan.OpeningTrigger is null ||
            plan.ResumeDestination is null ||
            plan.ResumeDestinationId is null ||
            plan.EventInstanceId is null)
        {
            return Result(ZenoRouteSchedulingExecutionStatus.Rejected, plan);
        }

        ZenoRoutePersistenceCoordinationResult claim = await runtime.ExecuteAsync(
            ZenoRouteStateService.PrepareOpeningClaim(
                runtime.CurrentState,
                route.Revision,
                plan.OpeningTrigger.Value,
                plan.ResumeDestination.Value,
                plan.ResumeDestinationId,
                plan.EventInstanceId,
                catalog),
            cancellationToken);
        return FromPersistence(plan, claim);
    }

    private async Task<ZenoRouteSchedulingExecutionResult> ExecuteTerminationAsync(
        ZenoRouteSchedulingPlan plan,
        CancellationToken cancellationToken)
    {
        ZenoRouteState? route = runtime.CurrentState.Route;
        if (route is null)
        {
            return Result(ZenoRouteSchedulingExecutionStatus.Rejected, plan);
        }

        ZenoRoutePersistenceCoordinationResult termination = await runtime.ExecuteAsync(
            ZenoRouteStateService.PrepareRunTermination(
                runtime.CurrentState,
                route.Revision,
                catalog),
            cancellationToken);
        return FromPersistence(plan, termination);
    }

    private ZenoRouteSchedulingExecutionResult FromPersistence(
        ZenoRouteSchedulingPlan plan,
        ZenoRoutePersistenceCoordinationResult persistence) =>
        Result(
            persistence.Status switch
            {
                ZenoRoutePersistenceCoordinationStatus.Completed =>
                    ZenoRouteSchedulingExecutionStatus.Completed,
                ZenoRoutePersistenceCoordinationStatus.Frozen =>
                    ZenoRouteSchedulingExecutionStatus.Frozen,
                _ => ZenoRouteSchedulingExecutionStatus.Rejected,
            },
            plan,
            persistence);

    private ZenoRouteSchedulingExecutionResult Result(
        ZenoRouteSchedulingExecutionStatus status,
        ZenoRouteSchedulingPlan plan,
        ZenoRoutePersistenceCoordinationResult? persistence = null) =>
        new(status, plan, runtime.CurrentState, persistence);
}
