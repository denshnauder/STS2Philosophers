using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Executes native Discard -> Sly AutoPlay -> OnPlayWrapper -> damage and relic hooks.
// Only hand-selection UI is replaced by a scripted player choice. Native effects,
// costs, payment, resource receipts and hook ordering are not patched.
internal static class SlyFullDiscardNativeChecks
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Practice(NativeRuntimeFixture fixture) =>
        typeof(WesternPracticeRelic).GetField("_practice", Fields)!.GetValue(fixture.Relic)!;
    private static string Law(NativeRuntimeFixture fixture) =>
        Practice(fixture).GetType().GetProperty("LawKind")!.GetValue(Practice(fixture))!.ToString()!;
    private static bool Used(NativeRuntimeFixture fixture, string name) =>
        (bool)Practice(fixture).GetType().GetProperty(name)!.GetValue(Practice(fixture))!;

    private static async Task<NativeRuntimeFixture> Setup()
    {
        Set(RunManager.Instance, "State", null);
        var fixture = new NativeRuntimeFixture(typeof(PlatoCivicSeal));
        LocalContext.NetId = fixture.Player.NetId;
        ModelDb.Inject(typeof(Silent));
        Set(fixture.Player, "Character", ModelDb.Character<Silent>());
        Set(fixture.Player, "ExtraFields", new ExtraPlayerFields());
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player });
        Set(run, "_currentActIndex", 0);
        Set(run, "Rng", new RunRngSet("198CHECK"));
        Set(run, "ActFloor", 1);
        Set(run, "_allCards", new List<CardModel>());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "RunModels", Array.Empty<RunModel>());
        Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        ModelDb.Inject(typeof(MultiplayerScalingModel));
        // Current RunState has RunModels instead of a separate scaling field.
        Set(fixture.Player, "Deck", new CardPile(PileType.Deck));
        Set(fixture.Player, "_runState", run);
        Set(fixture.Combat, "RunState", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Set(RunManager.Instance, "State", run);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        Set(RunManager.Instance, "ShouldSave", false);
        Set(fixture.Player.Creature, "CombatId", (uint)1);
        await fixture.Begin();
        fixture.PlayerState.Energy = 40;
        return fixture;
    }

    private static T Add<T>(NativeRuntimeFixture fixture, bool sly = false) where T : CardModel
    {
        ModelDb.Inject(typeof(T));
        T card = fixture.Combat.CreateCard<T>(fixture.Player);
        fixture.PlayerState.Hand.AddInternal(card, silent: true);
        card.EnergyCost.SetThisTurnOrUntilPlayed(2);
        if (sly) card.GiveSingleTurnSly();
        return card;
    }

    private static async Task Play(NativeRuntimeFixture fixture, CardModel card, int expected)
    {
        var target = card.Type == CardType.Attack ? fixture.Enemy : null;
        fixture.SelectAction(card, target);
        int shown = card.EnergyCost.GetWithModifiers(CostModifiers.All);
        Check(shown == expected, $"{card.Id} shown {shown}, expected {expected}.");
        int before = fixture.PlayerState.Energy;
        var (energy, stars) = await card.SpendResources();
        Check(energy == expected && before - fixture.PlayerState.Energy == expected,
            "The displayed cost and real native payment must agree.");
        await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), target, false,
            new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars },
            true).WaitAsync(TimeSpan.FromSeconds(10));
        fixture.ClearAction();
        Console.WriteLine($"ENC198 actual play {card.Id}: shown/paid={energy}, law={Law(fixture)}.");
    }

    private static async Task Opening()
    {
        var fixture = await Setup();
        var child = Add<StrikeSilent>(fixture, sly: true);
        int hp = fixture.Enemy.CurrentHp;
        await CardCmd.Discard(new ThrowingPlayerChoiceContext(), child).WaitAsync(TimeSpan.FromSeconds(10));
        Check(fixture.Enemy.CurrentHp == hp - 6 && child.Pile?.Type == PileType.Discard
            && fixture.PlayerState.Energy == 40, "The actual Sly attack must deal damage without paying energy.");
        Check(Law(fixture) == "Other" && !Used(fixture, "LawDiscountUsed") && !Used(fixture, "LawSurchargeUsed"),
            "Opening Sly cannot establish a manual law or consume its opportunities.");
        await Play(fixture, Add<StrikeSilent>(fixture), 2);
        Check(Law(fixture) == "Attack", "The first real manual attack establishes Attack.");
        await Play(fixture, Add<StrikeSilent>(fixture), 1);
        await Play(fixture, Add<DefendSilent>(fixture), 3);
        await Play(fixture, Add<DefendSilent>(fixture), 2);
        Console.WriteLine("ENC198 FULL PASS opening Sly damage, no law, first manual attack and once-only prices.");
    }

    private static async Task ManualSkill()
    {
        var fixture = await Setup();
        var child = Add<StrikeSilent>(fixture, sly: true);
        var parent = Add<Survivor>(fixture);
        int hp = fixture.Enemy.CurrentHp;
        await Play(fixture, parent, 2);
        Check(fixture.Enemy.CurrentHp == hp - 6 && child.Pile?.Type == PileType.Discard,
            "Native Survivor must actually discard and auto-play the Sly attack.");
        Check(Law(fixture) == "Skill", "A manual discard skill legally establishes Skill.");
        await Play(fixture, Add<StrikeSilent>(fixture), 3);
        await Play(fixture, Add<StrikeSilent>(fixture), 2);
        await Play(fixture, Add<DefendSilent>(fixture), 1);
        Console.WriteLine("ENC198 FULL PASS Survivor discard, actual Sly, Skill law and lawful Attack surcharge once.");
    }

    private static async Task Nested(bool surcharge)
    {
        var fixture = await Setup();
        await Play(fixture, surcharge ? Add<DefendSilent>(fixture) : Add<StrikeSilent>(fixture), 2);
        var child = Add<StrikeSilent>(fixture, sly: true);
        var parent = Add<DaggerThrow>(fixture);
        int hp = fixture.Enemy.CurrentHp;
        await Play(fixture, parent, surcharge ? 3 : 1);
        Check(fixture.Enemy.CurrentHp == hp - 15 && child.Pile?.Type == PileType.Discard,
            "Native DaggerThrow and its Sly child must both deal their actual damage.");
        Check(Used(fixture, surcharge ? "LawSurchargeUsed" : "LawDiscountUsed"),
            "Paid parent opportunity lost after real Sly");
        await Play(fixture, Add<StrikeSilent>(fixture), 2);
        Console.WriteLine($"ENC198 FULL PASS DaggerThrow, real nested Sly, parent {(surcharge ? "surcharge" : "discount")} consumed once.");
    }

    public static bool SelectForTest(Player player, Func<CardModel, bool>? filter,
        ref Task<IEnumerable<CardModel>> __result)
    {
        CardModel[] cards = player.PlayerCombatState!.Hand.Cards
            .Where(card => card.IsSlyThisTurn && (filter is null || filter(card))).ToArray();
        Check(cards.Length == 1, "Choose the one real Sly card from the actual eligible hand.");
        __result = Task.FromResult<IEnumerable<CardModel>>(cards);
        return false;
    }

    public static async Task Run()
    {
        var manager = RunManager.Instance;
        var priorRun = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        var priorExecutor = manager.ActionExecutor;
        var priorNetwork = manager.NetService;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        var priorLocal = LocalContext.NetId;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Fields)!.GetValue(CombatManager.Instance);
        object? priorIds = typeof(NetCombatCardDb).GetField("_idToCard", Fields)!.GetValue(NetCombatCardDb.Instance);
        object? priorCards = typeof(NetCombatCardDb).GetField("_cardToId", Fields)!.GetValue(NetCombatCardDb.Instance);
        var harmony = new Harmony("sts2philosophers.tests.enc198.full.input");
        List<Exception> failures = [];
        try
        {
            TestMode.IsOn = true;
            MethodInfo selection = typeof(CardSelectCmd).GetMethods()
                .Single(method => method.Name == "FromHand" && method.GetParameters().Length == 5);
            harmony.Patch(selection, prefix: new HarmonyMethod(typeof(SlyFullDiscardNativeChecks).GetMethod(nameof(SelectForTest))));
            foreach (var (name, work) in new (string, Func<Task>)[]
            {
                ("opening", Opening), ("manual skill", ManualSkill),
                ("nested discount", () => Nested(false)), ("nested surcharge", () => Nested(true)),
            })
            {
                try { await work(); }
                catch (Exception error)
                {
                    Console.WriteLine($"ENC198 FULL FAIL {name}: {error.Message}");
                    failures.Add(new InvalidOperationException(name, error));
                }
            }
            if (failures.Count > 0) throw new AggregateException(failures);
            Console.WriteLine("ENC198 full native discard checks passed: actual Sly/card effects, shown and paid costs; scripted discard selection, no rendered UI or player saves.");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Set(manager, "State", priorRun);
            Set(manager, "ActionExecutor", priorExecutor);
            Set(manager, "NetService", priorNetwork);
            Set(manager, "ShouldSave", priorSave);
            Set(CombatManager.Instance, "_turnState", priorTurn);
            Set(NetCombatCardDb.Instance, "_idToCard", priorIds);
            Set(NetCombatCardDb.Instance, "_cardToId", priorCards);
            LocalContext.NetId = priorLocal;
            TestMode.IsOn = priorTest;
        }
    }
}
