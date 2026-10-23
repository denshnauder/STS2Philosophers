using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class GalileoMeasuredPracticeNativeChecks
{
    private static readonly Assembly Mod = typeof(HeraclitusLyre).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => T(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!;
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj)!;
    private static void Put(object obj, string name, object value) => obj.GetType().GetProperty(name)!.SetValue(obj, value);
    private static object Reload(object obj) => Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", obj));
    private static void Commit(string type, object state, string action, params object[] rest)
    {
        var kind = Enum.Parse(T(type + "Action"), action);
        Check((bool)Call(type + "Flow", "TryPrepare", state, kind)
            && (bool)Call(type + "Flow", "TryCommitPending", [state, .. rest]), $"Real Galileo predecessor commits {action}.");
    }
    private static object Shared(string outcome, bool close = true)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["HERACLITUS__BEING_AND_CHANGE__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [journey, "WESTERN_EDGE_001"])!, "The actual Parmenides edge applies.");
        object democritus = Call("DemocritusArrangementState", "Create", "DEMOCRITUS:GALILEO:2");
        foreach (string action in new[] { "AcceptInvitation", "StudyMotion", "LeaveGap", "AdoptAtoms", "Leave" })
            Commit("DemocritusArrangement", democritus, action, journey, graph);
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 2])!, "The real Q2 is reserved.");
        object empedocles = Call("EmpedoclesRootsInquiryState", "Create", "EMPEDOCLES:GALILEO:2");
        foreach (string action in new[] { "AcceptInvitation", "StudyComposition", "CombineDifferent", "RecordComparison", "Leave" })
            Commit("EmpedoclesRootsInquiry", empedocles, action, journey, graph);
        object state = Call("GalileoMeasuredPracticeState", "Create", $"GALILEO_MEASURED_PRACTICE:{new RunRngSet("123456789").Seed}:3",
            Get(democritus, "InstanceId"), Get(empedocles, "InstanceId"));
        string[] path = outcome == "DeclineInvitation" ? [outcome]
            : outcome == "PublishRaw" ? ["AcceptInvitation", "ShortFirst", "TakeRemaining", outcome]
            : ["AcceptInvitation", "LongFirst", "TakeRemaining", "CheckClock", "RepeatCorrected", outcome];
        foreach (string action in path.Concat(close ? ["Leave"] : Array.Empty<string>()))
            Commit("GalileoMeasuredPractice", state, action, journey, democritus, empedocles);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey); Put(shared, "DemocritusArrangement", democritus);
        Put(shared, "EmpedoclesRootsInquiry", empedocles); Put(shared, "GalileoMeasuredPractice", state);
        return Reload(shared);
    }
    private static (NativeRuntimeFixture F, RunState Run) Fixture(object shared)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(HeraclitusLyre));
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player }); Set(run, "_currentActIndex", 2);
        Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 4);
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run); Set(f.Combat, "RunState", run); Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(RunManager.Instance, "State", run);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 12; i++) f.PlayerState.DrawPile.AddInternal(f.Combat.CreateCard<DefendSilent>(f.Player), silent: true);
        return (f, run);
    }
    private static HeraclitusLyre Lyre(NativeRuntimeFixture f) => (HeraclitusLyre)f.Relic;
    private static async Task<CardPlay> Release(NativeRuntimeFixture f, int paid, bool auto = false, bool wrongModel = false, bool lieAboutExhaust = false)
    {
        CardModel tracked = wrongModel ? f.AddCard<DefendSilent>() : f.AddCard<StrikeSilent>();
        tracked.EnergyCost.SetThisTurnOrUntilPlayed(paid);
        await f.Relic.AfterCardDrawn(new ThrowingPlayerChoiceContext(), tracked, fromHandDraw: true);
        HashSet<CardKeyword> keywords = [];
        Check(f.Relic.TryModifyKeywordsInCombat(tracked, keywords) && keywords.Contains(CardKeyword.Retain), "Galileo retains the actual tracked card.");
        CardModel opposing = wrongModel ? f.AddCard<StrikeSilent>() : f.AddCard<DefendSilent>();
        await f.PayAndComplete(opposing, wrongModel ? f.Enemy : null);
        f.SelectAction(tracked, wrongModel ? null : f.Enemy);
        var (energy, stars) = await tracked.SpendResources();
        var resources = new ResourceInfo { EnergySpent = energy, EnergyValue = 99, StarsSpent = stars, StarValue = stars };
        var location = f.Relic.ModifyCardPlayResultLocation(tracked, auto, resources,
            new CardLocation(f.Player, PileType.Discard, CardPilePosition.Bottom));
        Check(location.pileType == (auto ? PileType.Discard : PileType.Exhaust), "The production modifier freezes actual release versus automatic non-release.");
        f.PlayerState.Hand.RemoveInternal(tracked, silent: true); f.PlayerState.PlayPile.AddInternal(tracked, silent: true);
        var play = new CardPlay { Player = f.Player, Card = tracked, IsAutoPlay = auto,
            Target = wrongModel ? null : f.Enemy, ResultPile = lieAboutExhaust ? PileType.Discard : location.pileType,
            Resources = resources, PlayIndex = 0, PlayCount = 1 };
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        f.PlayerState.PlayPile.RemoveInternal(tracked, silent: true);
        f.PlayerState.ExhaustPile.AddInternal(tracked, silent: true); f.ClearAction();
        return play;
    }
    public static async Task Run()
    {
        var manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        object priorExecutor = manager.ActionExecutor;
        object? turn = typeof(CombatManager).GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(CombatManager.Instance);
        bool wasTest = TestMode.IsOn;
        var cacheRestore = SnapshotStaticState(typeof(ModelIdSerializationCache));
        var assemblyRestore = SnapshotStaticState(typeof(AssemblyInfo));
        object? oldIds = typeof(NetCombatCardDb).GetField("_idToCard", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(NetCombatCardDb.Instance);
        object? oldCards = typeof(NetCombatCardDb).GetField("_cardToId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(NetCombatCardDb.Instance);
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(HeraclitusLyre)); ModelDb.Inject(typeof(GalileoMeasuredPractice));
            ModelDb.Inject(typeof(StrikeSilent)); ModelDb.Inject(typeof(DefendSilent));
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init();
            foreach (string outcome in new[] { "AdoptProtocol", "KeepOld", "PublishRaw", "DeclineInvitation" })
                await Scenario(outcome);
            foreach (string failure in new[] { "sameTurn", "differentPaid", "differentModel", "automatic", "notExhaust", "malformed" })
                await Failure(failure);
            var (open, _) = Fixture(Shared("AdoptProtocol", close: false));
            Check(!(bool)typeof(HeraclitusLyre).GetMethod("HasGalileoProtocol", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(open.Relic, null)!, "Adopt without final closure cannot grant an improvement.");
            Console.WriteLine("Galileo native checks passed: actual positive EnergySpent, original Retain and native Exhaust result, later-turn same-model repeat draws exactly 3 once per combat; repeated callback/receipt recovery, wrong payment/model/turn/auto/result, malformed payload and retained old draw 1; no rendered UI or live game acceptance.");
        }
        finally
        {
            foreach (Action restore in cacheRestore.Concat(assemblyRestore)) restore();
            Set(manager, "State", prior); Set(manager, "ActionExecutor", priorExecutor); Set(CombatManager.Instance, "_turnState", turn);
            Set(NetCombatCardDb.Instance, "_idToCard", oldIds); Set(NetCombatCardDb.Instance, "_cardToId", oldCards); TestMode.IsOn = wasTest;
        }
    }
    private static List<Action> SnapshotStaticState(Type type)
    {
        List<Action> restore = [];
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.IsLiteral) continue;
            object? value = field.GetValue(null);
            if (value is IDictionary dictionary)
            {
                List<DictionaryEntry> entries = [];
                IDictionaryEnumerator iterator = dictionary.GetEnumerator();
                while (iterator.MoveNext()) entries.Add(iterator.Entry);
                restore.Add(() => { if (!field.IsInitOnly) field.SetValue(null, value); dictionary.Clear(); foreach (DictionaryEntry entry in entries) dictionary.Add(entry.Key, entry.Value); });
            }
            else if (value is IList list)
            {
                object?[] entries = list.Cast<object?>().ToArray();
                restore.Add(() => { if (!field.IsInitOnly) field.SetValue(null, value); list.Clear(); foreach (object? entry in entries) list.Add(entry); });
            }
            else if (!field.IsInitOnly) restore.Add(() => field.SetValue(null, value));
        }
        return restore;
    }

    private static async Task Scenario(string outcome)
    {
        var (f, run) = Fixture(Shared(outcome));
        await f.Begin(); f.PlayerState.Energy = 40;
        CardPlay sample = await f.PayAndComplete(f.AddCard<StrikeSilent>(), f.Enemy);
        Check(sample.Resources.EnergySpent == 1, "The reference sample uses native payment, not the quoted cost.");
        string partial = Lyre(f).GalileoMeasurementsPayload;
        var detached = (HeraclitusLyre)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check(detached.GalileoMeasurementsPayload == partial, $"The actual native relic saved properties round-trip the partial sample: {outcome} expected={partial} restored={detached.GalileoMeasurementsPayload}.");
        typeof(HeraclitusLyre).GetProperty(nameof(HeraclitusLyre.GalileoMeasurementsPayload))!.SetValue(f.Relic, partial);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), sample);
        await f.NextTurn(); f.PlayerState.Energy = 40;
        int before = f.PlayerState.DrawPile.Cards.Count;
        CardPlay release = await Release(f, 1);
        int expected = outcome == "AdoptProtocol" ? 3 : 1;
        Check(f.PlayerState.DrawPile.Cards.Count == before - expected && release.Resources.EnergySpent == 1,
            $"{outcome} actually draws {expected}; delta {before-f.PlayerState.DrawPile.Cards.Count}, paid {release.Resources.EnergySpent}, seed {run.Rng.Seed}, payload {Lyre(f).GalileoMeasurementsPayload}, sample model {sample.Card.Id}, practice {f.Relic.PracticePayload}.");
        string spent = Lyre(f).GalileoMeasurementsPayload;
        detached = (HeraclitusLyre)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check(detached.GalileoMeasurementsPayload == spent, "The actual native relic saved properties round-trip the claimed receipt.");
        typeof(HeraclitusLyre).GetProperty(nameof(HeraclitusLyre.GalileoMeasurementsPayload))!.SetValue(f.Relic, spent);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), release);
        Check(f.PlayerState.DrawPile.Cards.Count == before - expected, "Repeated callback after saved receipt cannot repeat the draw.");
        if (outcome != "AdoptProtocol") { Check(spent == string.Empty, "Old practice does not create Galileo receipts."); return; }
        await f.Relic.BeforeCombatStart();
        Check(Lyre(f).GalileoMeasurementsPayload == spent, "A recovery combat-start hook preserves the spent receipt.");
        await f.StartTurn(); await f.NextTurn(); f.PlayerState.Energy = 40;
        before = f.PlayerState.DrawPile.Cards.Count;
        await Release(f, 1);
        Check(f.PlayerState.DrawPile.Cards.Count == before, "The improved lyre does not fall back to draw 1 after its once-per-combat claim.");
        Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 5);
        await f.Relic.BeforeCombatStart(); await f.StartTurn(); f.PlayerState.Energy = 40;
        await f.PayAndComplete(f.AddCard<StrikeSilent>(), f.Enemy); await f.NextTurn(); f.PlayerState.Energy = 40;
        before = f.PlayerState.DrawPile.Cards.Count; await Release(f, 1);
        Check(f.PlayerState.DrawPile.Cards.Count == before - 3, "A genuine new combat can perform one fresh repeat.");
    }
    private static async Task Failure(string failure)
    {
        var (f, _) = Fixture(Shared("AdoptProtocol"));
        await f.Begin(); f.PlayerState.Energy = 40;
        await f.PayAndComplete(f.AddCard<StrikeSilent>(), f.Enemy);
        if (failure != "sameTurn") { await f.NextTurn(); f.PlayerState.Energy = 40; }
        if (failure == "malformed") typeof(HeraclitusLyre).GetProperty(nameof(HeraclitusLyre.GalileoMeasurementsPayload))!.SetValue(f.Relic, "unknown measurement payload");
        int before = f.PlayerState.DrawPile.Cards.Count;
        await Release(f, failure == "differentPaid" ? 2 : 1, auto: failure == "automatic", wrongModel: failure == "differentModel", lieAboutExhaust: failure == "notExhaust");
        Check(f.PlayerState.DrawPile.Cards.Count == before, $"{failure} cannot obtain draw 3 or restore old draw 1.");
        if (failure == "malformed") Check(Lyre(f).GalileoMeasurementsPayload == "unknown measurement payload", "Bad native payload is preserved verbatim.");
    }
}
