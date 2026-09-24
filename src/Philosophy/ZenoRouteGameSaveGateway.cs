using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

internal sealed class ZenoRouteGamePersistenceAdapter : IZenoRoutePersistenceConfirmationAdapter
{
    private readonly ZenoRoutePersistenceAdapter _inner;

    public ZenoRouteGamePersistenceAdapter(
        RunState runState,
        ZenoRouteValidationCatalog catalog)
    {
        _inner = new ZenoRoutePersistenceAdapter(
            new ZenoRouteGameSaveGateway(runState, catalog),
            catalog);
    }

    public Task<ZenoRoutePersistenceResult> PersistAsync(
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken = default)
    {
        return _inner.PersistAsync(request, state, cancellationToken);
    }
}

internal sealed class ZenoRouteGameSaveGateway : IZenoRouteSaveGateway
{
    private readonly RunState _runState;
    private readonly ZenoRouteValidationCatalog _catalog;

    public ZenoRouteGameSaveGateway(
        RunState runState,
        ZenoRouteValidationCatalog catalog)
    {
        _runState = runState;
        _catalog = catalog;
    }

    public bool TryBindState(ZenoRouteFeatureState state)
    {
        RunManager runManager = RunManager.Instance;
        if (!runManager.ShouldSave ||
            runManager.NetService.Type != NetGameType.Singleplayer ||
            !ReferenceEquals(runManager.DebugOnlyGetState(), _runState))
        {
            return false;
        }

        PhilosophyRunStateService.GetOrCreate(_runState)
            .SetCurrentZenoRouteState(state, _catalog);
        return true;
    }

    public Task SaveRunAsync()
    {
        return SaveManager.Instance.SaveRun(null);
    }

    public ZenoRouteSaveReadBack ReadCurrentRunSave()
    {
        ReadSaveResult<SerializableRun> result = SaveManager.Instance.LoadRunSave();
        if (result.Status != ReadSaveStatus.Success || result.SaveData is null)
        {
            return ZenoRouteSaveReadBack.Unavailable;
        }

        List<string> markerEntries = result.SaveData.EventsSeen
            .Where(PhilosophyRunStateSaveMarker.IsCategoryMarker)
            .Select(marker => marker.Entry)
            .ToList();
        return new ZenoRouteSaveReadBack(true, markerEntries);
    }
}
