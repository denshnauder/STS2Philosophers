using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using STS2RitsuLib.RunData;
using static NativeRuntimeFixture;

// The native writer/loader and real Ritsu callbacks run against the existing
// test-only memory store. Only map-screen opening is stubbed; no player store exists here.
internal sealed class NativeEventMemorySaveFixture : IDisposable
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly ModAssembly = typeof(PhilosophersGaze).Assembly;
    private static readonly Type Registry = typeof(RunSavedDataOptions).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry")!;
    private readonly Harmony _harmony = new("STS2Philosophers.ENC199.NativeMemorySave");
    private readonly List<Action> _restoreCaches;
    private readonly List<Action> _restoreAssemblies;
    private readonly FieldInfo _mockField = typeof(SaveManager).GetField("_mockInstance", Flags)!;
    private readonly object? _priorMock;
    private readonly object _store;
    private readonly RunSaveManager _runSaveManager;
    private static int _saveAttempts, _proceedAttempts;
    internal SaveManager Manager { get; }
    internal int SaveAttempts => _saveAttempts;
    internal int ProceedAttempts => _proceedAttempts;
    internal bool FailWrites { set => _store.GetType().GetField("FailWrites", Flags)!.SetValue(_store, value); }
    internal string Json => ((ISaveStore)_store).ReadFile((string)typeof(RunSaveManager)
        .GetProperty("CurrentRunSavePath", Flags)!.GetValue(_runSaveManager)!)!;

    internal NativeEventMemorySaveFixture(NativeRuntimeFixture fixture, RunState run)
    {
        Check(TestMode.IsOn, "Memory-save fixtures require the native test mode.");
        var snapshot = typeof(ZenoCivicSealUpgradeNativeChecks).GetMethod("SnapshotStaticState", Flags)!;
        _restoreCaches = (List<Action>)snapshot.Invoke(null, [typeof(ModelIdSerializationCache)])!;
        _restoreAssemblies = (List<Action>)snapshot.Invoke(null, [typeof(AssemblyInfo)])!;
        _priorMock = _mockField.GetValue(null);
        _store = Activator.CreateInstance(typeof(PhilosophersGazeSaveRecoveryNativeChecks)
            .GetNestedType("MemoryWrites", Flags)!, nonPublic: true)!;
        Manager = new SaveManager((ISaveStore)_store, false);
        _runSaveManager = new RunSaveManager(2, (ISaveStore)_store, new MigrationManager((ISaveStore)_store), false);
        Set(Manager, "_runSaveManager", _runSaveManager);
        try
        {
            ModAssembly.GetType("STS2Philosophers.PhilosophyRunStateRitsuStore")!
                .GetMethod("Initialize", Flags)!.Invoke(null, null);
            typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Flags)!.Invoke(null, null);
            typeof(PhilosophersGazeSaveRecoveryNativeChecks).GetMethod("InitializeCharacterLocalization", Flags)!.Invoke(null, null);
            Player player = fixture.Player;
            ModelDb.Inject(typeof(Ironclad)); Set(player, "Character", ModelDb.Character<Ironclad>());
            var rng = new MegaCrit.Sts2.Core.Random.PlayerRngSet(199);
            Set(player, "PlayerRng", rng); Set(player, "PlayerOdds", new MegaCrit.Sts2.Core.Odds.PlayerOddsSet(rng));
            Set(player, "RelicGrabBag", new RelicGrabBag()); Set(player, "Deck", new CardPile(PileType.Deck));
            Set(player, "ExtraFields", new ExtraPlayerFields()); Set(player, "UnlockState", new UnlockState([], [], 1));
            foreach (string name in new[] { "DiscoveredCards", "DiscoveredRelics", "DiscoveredPotions", "DiscoveredEnemies" })
                Set(player, name, new List<ModelId>());
            Set(player, "DiscoveredEpochs", new List<string>());
            Set(run, "Acts", Array.Empty<ActModel>());
            Set(run, "Odds", new MegaCrit.Sts2.Core.Odds.RunOddsSet(run.Rng.UnknownMapPoint));
            Set(run, "SharedRelicGrabBag", new RelicGrabBag()); Set(run, "ExtraFields", new ExtraRunFields());
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![ModAssembly] = new Mod { path = "isolated_enc199_memory_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [ModAssembly] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init();
            _mockField.SetValue(null, Manager); _saveAttempts = _proceedAttempts = 0;
            Check(ReferenceEquals(SaveManager.Instance, Manager), "Every native save must use this private memory store.");
            Type ModType(string name) => ModAssembly.GetType("STS2Philosophers." + name)!;
            _harmony.CreateClassProcessor(ModType("RouteEventMapBoundarySaveRoomPatch")).Patch();
            _harmony.CreateClassProcessor(ModType("PhilosophyRunStateToSavePatch")).Patch();
            Type Ritsu(string name) => Registry.Assembly.GetType("STS2RitsuLib.RunData.Patches." + name)!;
            _harmony.Patch(typeof(RunManager).GetMethod(nameof(RunManager.ToSave))!,
                postfix: new HarmonyMethod(Ritsu("RunSavedDataToSavePatch").GetMethod("Postfix", Flags)!));
            var patch = Ritsu("RunSavedDataSaveRunPatch");
            _harmony.Patch(typeof(RunSaveManager).GetMethod(nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])!,
                prefix: new HarmonyMethod(patch.GetMethod("Prefix", Flags)!), postfix: new HarmonyMethod(patch.GetMethod("Postfix", Flags)!));
            var write = new HarmonyMethod(Ritsu("RunSavedDataSaveStoreWritePatch").GetMethod("Prefix", Flags)!);
            _harmony.Patch(_store.GetType().GetMethod(nameof(ISaveStore.WriteFile), [typeof(string), typeof(byte[])])!, prefix: write);
            _harmony.Patch(_store.GetType().GetMethod(nameof(ISaveStore.WriteFileAsync), [typeof(string), typeof(byte[])])!, prefix: write);
            _harmony.Patch(typeof(SaveManager).GetMethod("SaveProgressFile", Flags)!,
                prefix: new HarmonyMethod(typeof(NativeEventMemorySaveFixture).GetMethod(nameof(SkipProgress), Flags)!));
            _harmony.Patch(typeof(SaveManager).GetMethod(nameof(SaveManager.SaveRun), [typeof(AbstractRoom), typeof(bool)])!,
                prefix: new HarmonyMethod(typeof(NativeEventMemorySaveFixture).GetMethod(nameof(CountSave), Flags)!));
            _harmony.Patch(typeof(NEventRoom).GetMethod(nameof(NEventRoom.Proceed))!,
                prefix: new HarmonyMethod(typeof(NativeEventMemorySaveFixture).GetMethod(nameof(CountProceed), Flags)!));
        }
        catch { Dispose(); throw; }
    }

    private static bool SkipProgress() => false;
    private static void CountSave() => _saveAttempts++;
    private static bool CountProceed(ref Task __result)
    {
        _proceedAttempts++; __result = Task.CompletedTask; return false;
    }

    public void Dispose()
    {
        _harmony.UnpatchAll(_harmony.Id); _mockField.SetValue(null, _priorMock);
        foreach (Action restore in _restoreCaches) restore();
        foreach (Action restore in _restoreAssemblies) restore();
    }
}
