using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace STS2Philosophers;

internal enum ZenoRoutePersistenceCheckpoint
{
    Prepared,
    Committed,
}

internal enum ZenoRoutePersistenceStatus
{
    Confirmed,
    Failed,
    Unknown,
}

internal sealed record ZenoRoutePersistenceRequest(
    string RequestId,
    string RunId,
    int FeatureGeneration,
    ZenoRoutePersistenceCheckpoint Checkpoint,
    long OperationId,
    string CandidateDigest,
    ZenoRouteStage ExpectedStage,
    long ExpectedRevision);

internal sealed record ZenoRoutePersistenceResult(
    string RequestId,
    ZenoRoutePersistenceStatus Status);

internal interface IZenoRoutePersistenceConfirmationAdapter
{
    Task<ZenoRoutePersistenceResult> PersistAsync(
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken = default);
}

internal static class ZenoRoutePersistenceConfirmation
{
    private const string RequestIdentityVersion = "ZENO_ROUTE_SAVE_V1";

    public static bool TryCreatePreparedRequest(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog,
        out ZenoRoutePersistenceRequest? request)
    {
        request = null;
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) ||
            state.Route is null ||
            state.PendingOperation is null)
        {
            return false;
        }

        ZenoRoutePendingOperation pending = state.PendingOperation;
        request = CreateRequest(
            state.Route.RunId,
            state.FeatureGeneration,
            ZenoRoutePersistenceCheckpoint.Prepared,
            pending.OperationId,
            pending.CandidateDigest,
            state.Route.Stage,
            state.Route.Revision);
        return true;
    }

    public static bool TryCreateCommittedRequest(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog,
        out ZenoRoutePersistenceRequest? request)
    {
        request = null;
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) ||
            state.Route is null ||
            state.PendingOperation is not null ||
            state.Route.LastCommittedOperationId <= 0)
        {
            return false;
        }

        ZenoRouteState route = state.Route;
        request = CreateRequest(
            route.RunId,
            state.FeatureGeneration,
            ZenoRoutePersistenceCheckpoint.Committed,
            route.LastCommittedOperationId,
            ZenoRouteStateCodec.ComputeCandidateDigest(route),
            route.Stage,
            route.Revision);
        return true;
    }

    public static ZenoRoutePersistenceResult FromReadBack(
        ZenoRoutePersistenceRequest request,
        int markerCount,
        ZenoRoutePayloadClassification classification,
        ZenoRouteFeatureState? readBackState,
        ZenoRouteValidationCatalog catalog)
    {
        bool confirmed = markerCount == 1 &&
                         classification == ZenoRoutePayloadClassification.Current &&
                         readBackState is not null &&
                         Matches(request, readBackState, catalog);
        return Result(
            request,
            confirmed
                ? ZenoRoutePersistenceStatus.Confirmed
                : ZenoRoutePersistenceStatus.Unknown);
    }

    public static ZenoRoutePersistenceResult FromExplicitFailure(
        ZenoRoutePersistenceRequest request,
        bool confirmedNotPersisted)
    {
        return Result(
            request,
            confirmedNotPersisted
                ? ZenoRoutePersistenceStatus.Failed
                : ZenoRoutePersistenceStatus.Unknown);
    }

    public static ZenoRoutePersistenceResult FromSaveTaskCompletion(
        ZenoRoutePersistenceRequest request)
    {
        return Result(request, ZenoRoutePersistenceStatus.Unknown);
    }

    public static ZenoRoutePersistenceResult ValidateAdapterResult(
        ZenoRoutePersistenceRequest request,
        ZenoRoutePersistenceResult result)
    {
        return string.Equals(result.RequestId, request.RequestId, StringComparison.Ordinal)
            ? result
            : Result(request, ZenoRoutePersistenceStatus.Unknown);
    }

    private static bool Matches(
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog)
    {
        if (!ZenoRouteStateCodec.IsValidFeature(state, catalog) ||
            state.FeatureGeneration != request.FeatureGeneration ||
            state.Route is null ||
            !string.Equals(state.Route.RunId, request.RunId, StringComparison.Ordinal) ||
            state.Route.Stage != request.ExpectedStage ||
            state.Route.Revision != request.ExpectedRevision)
        {
            return false;
        }

        return request.Checkpoint switch
        {
            ZenoRoutePersistenceCheckpoint.Prepared =>
                state.PendingOperation is not null &&
                state.PendingOperation.OperationId == request.OperationId &&
                string.Equals(
                    state.PendingOperation.CandidateDigest,
                    request.CandidateDigest,
                    StringComparison.Ordinal),
            ZenoRoutePersistenceCheckpoint.Committed =>
                state.PendingOperation is null &&
                state.Route.LastCommittedOperationId == request.OperationId &&
                string.Equals(
                    ZenoRouteStateCodec.ComputeCandidateDigest(state.Route),
                    request.CandidateDigest,
                    StringComparison.Ordinal),
            _ => false,
        };
    }

    private static ZenoRoutePersistenceRequest CreateRequest(
        string runId,
        int featureGeneration,
        ZenoRoutePersistenceCheckpoint checkpoint,
        long operationId,
        string candidateDigest,
        ZenoRouteStage expectedStage,
        long expectedRevision)
    {
        string identityPayload = string.Join(
            "\n",
            RequestIdentityVersion,
            runId,
            featureGeneration.ToString(CultureInfo.InvariantCulture),
            ((int)checkpoint).ToString(CultureInfo.InvariantCulture),
            operationId.ToString(CultureInfo.InvariantCulture),
            candidateDigest,
            ((int)expectedStage).ToString(CultureInfo.InvariantCulture),
            expectedRevision.ToString(CultureInfo.InvariantCulture));
        string requestId = $"{RequestIdentityVersion}_{Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identityPayload)))}";
        return new ZenoRoutePersistenceRequest(
            requestId,
            runId,
            featureGeneration,
            checkpoint,
            operationId,
            candidateDigest,
            expectedStage,
            expectedRevision);
    }

    private static ZenoRoutePersistenceResult Result(
        ZenoRoutePersistenceRequest request,
        ZenoRoutePersistenceStatus status)
    {
        return new ZenoRoutePersistenceResult(request.RequestId, status);
    }
}
