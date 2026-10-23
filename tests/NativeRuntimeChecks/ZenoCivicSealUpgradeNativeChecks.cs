using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class ZenoCivicSealUpgradeNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => T(type).GetMethods(Methods)
        .Single(m => m.IsStatic && m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!;
    private static object Get(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target)!;
    private static void Put(object target, string name, object? value) => target.GetType().GetProperty(name)!.SetValue(target, value);
    private static object Action(string name) => Enum.Parse(T("ZenoCivicAction"), name);
    private static object Branch(bool conditional) => Enum.Parse(T("ZenoCivicUpgradeBranch"), conditional ? "Conditional" : "Public");
    private static object Reload(object shared) => Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", shared));

    private static object Shared(bool conditional, bool close)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["PLATO__FREEDOM_AND_INSTITUTIONS__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "The native grant fixture consumes the real first-act Question.");
        object state = Call("ZenoCivicChallengeState", "Create", "ZENO:UPGRADE:NATIVE:1");
        string[] actions = ["AcceptInvitation", conditional ? "ProposeConditionalException" : "DemandPublicRule",
            conditional ? "PublishConditionalObjection" : "PublishPublicObjection"];
        foreach (string action in actions.Concat(close ? ["Leave"] : Array.Empty<string>()))
            Check((bool)Call("ZenoCivicChallengeFlow", "TryPrepare", state, Action(action))
                && (bool)Call("ZenoCivicChallengeFlow", "TryCommitPending", state, journey, graph),
                "The native seal fixture records actual civic choices rather than inventing a closed result.");
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey); Put(shared, "ZenoCivicChallenge", state);
        return Reload(shared);
    }

    private static (NativeRuntimeFixture Fixture, RunState Run, object Shared) FixtureFor(bool conditional, bool close)
    {
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(PlatoCivicSeal));
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", 0);
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(fixture.Player, "_runState", run); Set(fixture.Combat, "RunState", run);
        Set(fixture.Player, "Deck", new CardPile(PileType.Deck));
        CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(room, "Id", 87); Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(run, "ActFloor", 4);
        Set(RunManager.Instance, "State", run);
        object shared = Shared(conditional, close);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        return (fixture, run, shared);
    }

    private static bool Grant(PlatoCivicSeal seal, string instance, object branch) =>
        (bool)typeof(PlatoCivicSeal).GetMethod("TryGrantZenoCivicUpgrade", Methods)!.Invoke(seal, [instance, branch])!;

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? original = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorTest = TestMode.IsOn;
        object priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(CombatManager.Instance);
        object? priorCardIds = typeof(NetCombatCardDb).GetField("_idToCard", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(NetCombatCardDb.Instance);
        object? priorCards = typeof(NetCombatCardDb).GetField("_cardToId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(NetCombatCardDb.Instance);
        List<Action> restoreCache = SnapshotStaticState(typeof(ModelIdSerializationCache));
        List<Action> restoreAssemblies = SnapshotStaticState(typeof(AssemblyInfo));
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(PlatoCivicSeal)); ModelDb.Inject(typeof(ZenoCivicChallenge));
            ModelDb.Inject(typeof(StrikeSilent)); ModelDb.Inject(typeof(DefendSilent));
            // Only this isolated headless host's native cache is initialized;
            // its prior collections, scalar sizes and initialized flag return below.
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![typeof(PlatoCivicSeal).Assembly] = new Mod
            {
                path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true },
                assemblies = [typeof(PlatoCivicSeal).Assembly],
            };
            ModelIdSerializationCache.ResetForTest();
            ModelIdSerializationCache.Init();
            foreach (bool conditional in new[] { false, true })
            {
                CheckImmediateCommitAndNativeReadback(conditional);
                CheckConflictingGrantDoesNotClose(conditional);
                foreach (bool attackLaw in new[] { false, true }) await CombatScenario(conditional, attackLaw);
                await LegacyScenario(conditional);
            }
            Console.WriteLine("Zeno seal native checks passed: actual final event commit improves the owned seal, native SavedProperty serialization/RunSaveManager readback and detached restore, mutually exclusive instance-bound grants, real old law costs plus 3 Block/1 draw, both law kinds, no reward for lawmaking/automatic/repeated callbacks, per-turn receipts/reload and malformed isolation, no closed-legacy backfill; no rendered UI/full game replay.");
        }
        finally
        {
            foreach (Action restore in restoreCache) restore();
            foreach (Action restore in restoreAssemblies) restore();
            Set(manager, "State", original); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn);
            Set(NetCombatCardDb.Instance, "_idToCard", priorCardIds); Set(NetCombatCardDb.Instance, "_cardToId", priorCards);
            TestMode.IsOn = priorTest;
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

    private static void CheckConflictingGrantDoesNotClose(bool conditional)
    {
        var (fixture, _, shared) = FixtureFor(conditional, close: false);
        var seal = (PlatoCivicSeal)fixture.Relic;
        Check(Grant(seal, "ANOTHER:REAL:INSTANCE", Branch(conditional)), "Prepare an existing conflicting native receipt.");
        object state = Get(shared, "ZenoCivicChallenge");
        Check((bool)Call("ZenoCivicChallengeFlow", "TryPrepare", state, Action("Leave")), "Prepare the blocked grant close.");
        string before = JsonSerializer.Serialize(state, state.GetType());
        string payload = seal.ZenoCivicUpgradePayload;
        EventModel local = ModelDb.Event<ZenoCivicChallenge>().ToMutable(); Set(local, "Owner", fixture.Player);
        Check(!(bool)typeof(ZenoCivicChallenge).GetMethod("CommitPending", Methods)!.Invoke(local, [shared, state])!
            && JsonSerializer.Serialize(state, state.GetType()) == before && seal.ZenoCivicUpgradePayload == payload,
            "A conflicting grant is refused before closure, retaining the exact pending save and old receipt.");
    }

    private static void CheckImmediateCommitAndNativeReadback(bool conditional)
    {
        var (fixture, _, shared) = FixtureFor(conditional, close: false);
        PlatoCivicSeal seal = (PlatoCivicSeal)fixture.Relic;
        Check(seal.ZenoCivicUpgradePayload == string.Empty && !(bool)Get(Get(shared, "ZenoCivicChallenge"), "Closed"),
            "Publishing a civic objection has not yet improved a seal or closed the event.");
        object state = Get(shared, "ZenoCivicChallenge");
        Check((bool)Call("ZenoCivicChallengeFlow", "TryPrepare", state, Action("Leave")), "Prepare the actual closing grant.");
        ModelDb.Inject(typeof(ZenoCivicChallenge));
        EventModel local = ModelDb.Event<ZenoCivicChallenge>().ToMutable();
        Set(local, "Owner", fixture.Player);
        MethodInfo commit = typeof(ZenoCivicChallenge).GetMethod("CommitPending", Methods)!;
        Check((bool)commit.Invoke(local, [shared, state])!, "The real event's final commit grants the actual mutable seal.");
        string payload = seal.ZenoCivicUpgradePayload;
        Check(payload.Length > 0 && (bool)Get(state, "Closed") && (bool)Get(state, "SealUpgradeGranted")
            && !(bool)commit.Invoke(local, [shared, state])!, "The actual close and grant cannot be replayed.");
        string instance = (string)Get(state, "InstanceId");
        Check(Grant(seal, instance, Branch(conditional)) && seal.ZenoCivicUpgradePayload == payload
            && !Grant(seal, "OTHER:INSTANCE", Branch(conditional)) && !Grant(seal, instance, Branch(!conditional)),
            "Native grant idempotence retains the same receipt and refuses another instance or branch.");
        SerializableRun saved = new()
        {
            SchemaVersion = 20,
            Players = [new SerializablePlayer { NetId = fixture.Player.NetId, Relics = [seal.ToSerializable()] }],
        };
        var store = new InMemoryRunSaveStore(JsonSerializationUtility.ToJson(saved));
        var loader = new RunSaveManager(2, store, new MigrationManager(store), false);
        var read = loader.LoadRunSave();
        Check(read.Success && read.SaveData is not null, $"The actual native run loader reads the serialized civic seal: {read.Status} {read.ErrorMessage}");
        MethodInfo matches = typeof(ZenoCivicChallenge).GetMethod("SavedSealUpgradeMatches", Methods)!;
        bool Matches(SerializableRun candidate, string expected, string id, object branch) =>
            (bool)matches.Invoke(null, [candidate, fixture.Player.NetId, expected, id, branch])!;
        Check(Matches(read.SaveData!, payload, instance, Branch(conditional))
            && !Matches(read.SaveData!, payload, "OTHER:INSTANCE", Branch(conditional))
            && !Matches(read.SaveData!, payload, instance, Branch(!conditional))
            && !Matches(read.SaveData!, string.Empty, instance, Branch(conditional))
            && ReferenceEquals(fixture.Player.Relics.Single(), seal) && ReferenceEquals(seal.Owner, fixture.Player),
            "Production readback confirms the real native saved property without rebinding or replacing the active player's relic.");
        read.SaveData!.Players[0].Relics.Clear();
        Check(!Matches(read.SaveData, payload, instance, Branch(conditional)), "A shared Closed result with no native saved seal cannot claim delivery.");
        PlatoCivicSeal another = (PlatoCivicSeal)ModelDb.Relic<PlatoCivicSeal>().ToMutable(); another.Owner = fixture.Player;
        Check(another.ZenoCivicUpgradePayload == string.Empty, "Independent canonical copies never share a mutable grant/turn ledger.");
    }

    private static void FillDrawPile(NativeRuntimeFixture fixture)
    {
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 8; i++)
        {
            var card = fixture.Combat.CreateCard<DefendSilent>(fixture.Player);
            fixture.PlayerState.DrawPile.AddInternal(card, silent: true);
        }
    }

    private static CardModel Card(NativeRuntimeFixture fixture, bool attack)
    {
        CardModel card = attack ? fixture.AddCard<StrikeSilent>() : fixture.AddCard<DefendSilent>();
        card.EnergyCost.SetThisTurnOrUntilPlayed(2); return card;
    }

    private static async Task CombatScenario(bool conditional, bool attackLaw)
    {
        var (fixture, run, shared) = FixtureFor(conditional, close: true);
        PlatoCivicSeal seal = (PlatoCivicSeal)fixture.Relic;
        string instance = (string)Get(Get(shared, "ZenoCivicChallenge"), "InstanceId");
        Check(Grant(seal, instance, Branch(conditional)), "The verified civic branch is attached to the native relic.");
        FillDrawPile(fixture);
        await fixture.Begin(); fixture.PlayerState.Energy = 40;
        int startBlock = fixture.Player.Creature.Block;
        int drawCount = fixture.PlayerState.DrawPile.Cards.Count;
        CardModel first = Card(fixture, attackLaw);
        string beforeAuto = seal.ZenoCivicUpgradePayload;
        await seal.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = first, IsAutoPlay = true,
            Target = attackLaw ? fixture.Enemy : null, ResultPile = PileType.Discard,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            PlayIndex = 0, PlayCount = 1,
        });
        Check(seal.ZenoCivicUpgradePayload == beforeAuto, "An automatic card cannot establish or spend a new upgrade claim.");
        CardPlay lawmaker = await fixture.PayAndComplete(first, attackLaw ? fixture.Enemy : null);
        Check(lawmaker.Resources.EnergySpent == 2 && fixture.Player.Creature.Block == startBlock
            && fixture.PlayerState.DrawPile.Cards.Count == drawCount && seal.ZenoCivicUpgradePayload == beforeAuto,
            "The first manual lawmaking card receives neither branch's reward.");
        fixture.ReloadRelicPayload();
        await seal.AfterCardPlayed(new ThrowingPlayerChoiceContext(), lawmaker);
        Check(seal.ZenoCivicUpgradePayload == beforeAuto, "Repeating the lawmaker callback after base payload restore cannot turn it into a reward.");
        bool rewardAttack = conditional ? !attackLaw : attackLaw;
        CardPlay rewarded = await fixture.PayAndComplete(Card(fixture, rewardAttack), rewardAttack ? fixture.Enemy : null);
        Check(rewarded.Resources.EnergySpent == (conditional ? 3 : 1)
            && fixture.Player.Creature.Block == startBlock + (conditional ? 0 : 3)
            && fixture.PlayerState.DrawPile.Cards.Count == drawCount - (conditional ? 1 : 0),
            "The true second manual card keeps old native discount/surcharge and grants exactly the chosen 3 Block/1 draw.");
        string claimed = seal.ZenoCivicUpgradePayload;
        typeof(PlatoCivicSeal).GetProperty(nameof(PlatoCivicSeal.ZenoCivicUpgradePayload))!.SetValue(seal, claimed);
        fixture.ReloadRelicPayload();
        await seal.AfterCardPlayed(new ThrowingPlayerChoiceContext(), rewarded);
        await fixture.PayAndComplete(Card(fixture, rewardAttack), rewardAttack ? fixture.Enemy : null);
        Check(seal.ZenoCivicUpgradePayload == claimed && fixture.Player.Creature.Block == startBlock + (conditional ? 0 : 3)
            && fixture.PlayerState.DrawPile.Cards.Count == drawCount - (conditional ? 1 : 0),
            "Saved spent receipt, duplicate callback and later matching cards cannot repeat a turn's reward.");
        await fixture.NextTurn(); fixture.PlayerState.Energy = 40;
        int beforeNextBlock = fixture.Player.Creature.Block;
        await fixture.PayAndComplete(Card(fixture, rewardAttack), rewardAttack ? fixture.Enemy : null);
        Check(fixture.Player.Creature.Block == beforeNextBlock + (conditional ? 0 : 3)
            && fixture.PlayerState.DrawPile.Cards.Count == drawCount - (conditional ? 2 : 0),
            "A genuine later turn may claim the same mutually exclusive branch once again.");
        string valid = seal.ZenoCivicUpgradePayload;
        typeof(PlatoCivicSeal).GetProperty(nameof(PlatoCivicSeal.ZenoCivicUpgradePayload))!.SetValue(seal, "broken native civic payload");
        Check(!Grant(seal, instance, Branch(conditional)), "A malformed native payload cannot be overwritten by regranting history.");
        typeof(PlatoCivicSeal).GetProperty(nameof(PlatoCivicSeal.ZenoCivicUpgradePayload))!.SetValue(seal, valid);
        await fixture.NextTurn(); fixture.PlayerState.Energy = 40;
        int unavailableBlock = fixture.Player.Creature.Block;
        int unavailableDraw = fixture.PlayerState.DrawPile.Cards.Count;
        await fixture.PayAndComplete(Card(fixture, rewardAttack), rewardAttack ? fixture.Enemy : null);
        Check(seal.ZenoCivicUpgradePayload == "broken native civic payload"
            && fixture.Player.Creature.Block == unavailableBlock && fixture.PlayerState.DrawPile.Cards.Count == unavailableDraw,
            "Unknown native payload stays verbatim and frozen, even if a later setter or combat would otherwise reward it.");
        var restoredBad = (PlatoCivicSeal)RelicModel.FromSerializable(seal.ToSerializable());
        Check(restoredBad.ZenoCivicUpgradePayload == "broken native civic payload", "Native reserialization preserves the exact isolated payload.");
        Check(run.CurrentActIndex == 0, "A civic seal payoff does not wait for an act transition.");
    }

    private static async Task LegacyScenario(bool conditional)
    {
        var (fixture, run, shared) = FixtureFor(conditional, close: true);
        object old = Get(shared, "ZenoCivicChallenge");
        JsonObject json = JsonSerializer.SerializeToNode(old, old.GetType())!.AsObject();
        json.Remove("SealUpgradeVersion"); json.Remove("SealUpgradeGranted");
        old = JsonSerializer.Deserialize(json.ToJsonString(), old.GetType())!;
        Put(shared, "ZenoCivicChallenge", old);
        shared = Reload(shared); Call("PhilosophyRunStateService", "Restore", run, shared);
        PlatoCivicSeal seal = (PlatoCivicSeal)fixture.Relic;
        FillDrawPile(fixture); await fixture.Begin(); fixture.PlayerState.Energy = 40;
        await fixture.PayAndComplete(Card(fixture, true), fixture.Enemy);
        int block = fixture.Player.Creature.Block, draws = fixture.PlayerState.DrawPile.Cards.Count;
        await fixture.PayAndComplete(Card(fixture, !conditional), conditional ? null : fixture.Enemy);
        Check(seal.ZenoCivicUpgradePayload == string.Empty && fixture.Player.Creature.Block == block
            && fixture.PlayerState.DrawPile.Cards.Count == draws,
            "Old already-closed civic history does not silently improve the relic or gain a new combat reward.");
    }
}
