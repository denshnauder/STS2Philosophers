using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

internal sealed class ZenoRouteGamePersistenceAdapter : IZenoRoutePersistenceConfirmationAdapter
{
    private readonly ZenoRoutePersistenceAdapter _inner;

    public ZenoRouteGamePersistenceAdapter(
        RunState runState,
        PhilosophyRunState sharedState,
        ZenoRouteValidationCatalog catalog)
    {
        _inner = new ZenoRoutePersistenceAdapter(
            new ZenoRouteGameSaveGateway(runState, sharedState, catalog),
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
    private readonly PhilosophyRunState _sharedState;
    private readonly ZenoRouteValidationCatalog _catalog;

    public ZenoRouteGameSaveGateway(
        RunState runState,
        PhilosophyRunState sharedState,
        ZenoRouteValidationCatalog catalog)
    {
        _runState = runState;
        _sharedState = sharedState;
        _catalog = catalog;
    }

    public bool TryBindState(ZenoRouteFeatureState state)
    {
        RunManager runManager = RunManager.Instance;
        if (!runManager.ShouldSave ||
            runManager.NetService.Type != NetGameType.Singleplayer ||
            !ReferenceEquals(runManager.DebugOnlyGetState(), _runState) ||
            !PhilosophyRunStateService.TryGet(_runState, out PhilosophyRunState? currentSharedState) ||
            !ReferenceEquals(currentSharedState, _sharedState))
        {
            return false;
        }

        _sharedState.SetCurrentZenoRouteState(state, _catalog);
        return true;
    }

    public Task SaveRunAsync()
    {
        return SaveManager.Instance.SaveRun(null);
    }

    public ZenoRouteSaveReadBack ReadCurrentRunSave()
    {
        SaveManager saveManager = SaveManager.Instance;
        RunSaveManager runSaveManager = SaveManagerAccess.GetRunSaveManager(saveManager);
        ISaveStore saveStore = SaveManagerAccess.GetSaveStore(saveManager);
        string? json = saveStore.ReadFile(SaveManagerAccess.GetCurrentRunSavePath(runSaveManager));
        if (json is null)
        {
            return ZenoRouteSaveReadBack.Unavailable;
        }

        PhilosophyRunStateRitsuSaveReadResult ritsuRead = PhilosophyRunStateRitsuSaveReader.Read(json);
        if (ritsuRead.Classification == PhilosophyRunStateRitsuSaveClassification.Current &&
            ritsuRead.EncodedState is not null)
        {
            return new ZenoRouteSaveReadBack(
                true,
                [$"{PhilosophyRunStateMarkerCarrier.CurrentVersionPrefix}{ritsuRead.EncodedState}"]);
        }

        if (ritsuRead.Classification == PhilosophyRunStateRitsuSaveClassification.Invalid)
        {
            return new ZenoRouteSaveReadBack(true, []);
        }

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

    private static class SaveManagerAccess
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_runSaveManager")]
        internal static extern ref RunSaveManager GetRunSaveManager(SaveManager saveManager);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_saveStore")]
        internal static extern ref ISaveStore GetSaveStore(SaveManager saveManager);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CurrentRunSavePath")]
        internal static extern string GetCurrentRunSavePath(RunSaveManager runSaveManager);
    }
}
