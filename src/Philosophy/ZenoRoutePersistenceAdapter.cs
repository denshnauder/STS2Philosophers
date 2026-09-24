namespace STS2Philosophers;

internal sealed record ZenoRouteSaveReadBack(
    bool IsAvailable,
    IReadOnlyList<string> MarkerEntries)
{
    public static ZenoRouteSaveReadBack Unavailable { get; } = new(false, []);
}

internal interface IZenoRouteSaveGateway
{
    bool TryBindState(ZenoRouteFeatureState state);

    Task SaveRunAsync();

    ZenoRouteSaveReadBack ReadCurrentRunSave();
}

internal sealed class ZenoRoutePersistenceAdapter : IZenoRoutePersistenceConfirmationAdapter
{
    private readonly IZenoRouteSaveGateway _gateway;
    private readonly ZenoRouteValidationCatalog _catalog;

    public ZenoRoutePersistenceAdapter(
        IZenoRouteSaveGateway gateway,
        ZenoRouteValidationCatalog catalog)
    {
        _gateway = gateway;
        _catalog = catalog;
    }

    public async Task<ZenoRoutePersistenceResult> PersistAsync(
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MatchesRequest(request, state))
            {
                return Unknown(request);
            }

            if (!_gateway.TryBindState(state))
            {
                return Unknown(request);
            }

            await _gateway.SaveRunAsync();

            ZenoRouteSaveReadBack readBack = _gateway.ReadCurrentRunSave();
            if (!readBack.IsAvailable)
            {
                return Unknown(request);
            }

            PhilosophyRunStateMarkerRestoreResult restored =
                PhilosophyRunStateMarkerCarrier.Restore(readBack.MarkerEntries, _catalog);
            return ZenoRoutePersistenceConfirmation.FromReadBack(
                request,
                restored.MarkerCount,
                restored.State?.ZenoRoutePayload.Classification ??
                    ZenoRoutePayloadClassification.PreFeatureLegacy,
                restored.State?.ZenoRoutePayload.State,
                _catalog);
        }
        catch (Exception)
        {
            return Unknown(request);
        }
    }

    private static ZenoRoutePersistenceResult Unknown(ZenoRoutePersistenceRequest request)
    {
        return new ZenoRoutePersistenceResult(
            request.RequestId,
            ZenoRoutePersistenceStatus.Unknown);
    }

    private bool MatchesRequest(
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state)
    {
        ZenoRoutePersistenceRequest? expected = null;
        bool created = request.Checkpoint switch
        {
            ZenoRoutePersistenceCheckpoint.Prepared =>
                ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                    state,
                    _catalog,
                    out expected),
            ZenoRoutePersistenceCheckpoint.Committed =>
                ZenoRoutePersistenceConfirmation.TryCreateCommittedRequest(
                    state,
                    _catalog,
                    out expected),
            _ => false,
        };
        return created &&
               expected is not null &&
               string.Equals(expected.RequestId, request.RequestId, StringComparison.Ordinal);
    }
}
