using System.Reflection;
using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Exercises the real mutable PlatoCivicSeal and native CardPlay callbacks. Only
// the hand/deck bootstrap and the explicit pile moves are fixture operations;
// the seal's turn record, return command and saved property are production code.
internal static class PlatoCivicSealNativeChecks
{
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) =>
        T(type).GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, args);
    private static object? Get(object value, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo? property = value.GetType().GetProperty(name, flags);
        if (property is not null) return property.GetValue(value);
        return value.GetType().GetField(name, flags)!.GetValue(value);
    }

    private static async Task<NativeRuntimeFixture> Setup()
    {
        Set(RunManager.Instance, "State", null);
        var fixture = new NativeRuntimeFixture(typeof(PlatoCivicSeal));
        LocalContext.NetId = fixture.Player.NetId;
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player });
        Set(run, "_currentActIndex", 0);
        Set(run, "Rng", new RunRngSet("ENC206_PLATO_SEAL"));
        Set(run, "ActFloor", 1);
        Set(run, "_allCards", new List<CardModel>());
        Set(run, "Modifiers", Array.Empty<ModifierModel>());
        Set(run, "RunModels", Array.Empty<RunModel>());
        Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(run, "SharedRelicGrabBag", new RelicGrabBag());
        Set(run, "_currentRooms", new List<AbstractRoom>());
        var history = new MapPointHistoryEntry
        {
            MapPointType = MapPointType.Ancient,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<PhilosophersGaze>() }],
            PlayerStats = [new() { PlayerId = fixture.Player.NetId }],
        };
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new() { history } });
        Set(fixture.Player, "Deck", new CardPile(PileType.Deck));
        Set(fixture.Player, "RelicGrabBag", new RelicGrabBag());
        Set(fixture.Player, "_runState", run);
        Set(fixture.Combat, "RunState", run);
        Set(RunManager.Instance, "State", run);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        Set(RunManager.Instance, "ShouldSave", false);
        Set(fixture.Player.Creature, "CombatId", (uint)1);
        await fixture.Begin();
        fixture.PlayerState.Energy = 40;
        return fixture;
    }

    private static CardModel Add(NativeRuntimeFixture fixture, bool attack)
    {
        ModelDb.Inject(attack ? typeof(MegaCrit.Sts2.Core.Models.Cards.StrikeSilent)
            : typeof(MegaCrit.Sts2.Core.Models.Cards.DefendSilent));
        CardModel card = attack
            ? fixture.Combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.StrikeSilent>(fixture.Player)
            : fixture.Combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.DefendSilent>(fixture.Player);
        fixture.PlayerState.Hand.AddInternal(card, silent: true);
        card.EnergyCost.SetThisTurnOrUntilPlayed(2);
        return card;
    }

    private static async Task<CardPlay> Play(NativeRuntimeFixture fixture, CardModel card)
    {
        CardPlay play = await fixture.PayAndComplete(card,
            card.Type == CardType.Attack ? fixture.Enemy : null);
        return play;
    }

    private static void MoveToDiscard(NativeRuntimeFixture fixture, CardModel card)
    {
        fixture.PlayerState.PlayPile.RemoveInternal(card, silent: true);
        fixture.PlayerState.DiscardPile.AddInternal(card, silent: true);
    }

    private static void MoveToExhaust(NativeRuntimeFixture fixture, CardModel card)
    {
        fixture.PlayerState.PlayPile.RemoveInternal(card, silent: true);
        fixture.PlayerState.ExhaustPile.AddInternal(card, silent: true);
    }

    private static bool InHand(NativeRuntimeFixture fixture, CardModel card) =>
        fixture.PlayerState.Hand.Cards.Any(held => ReferenceEquals(held, card));

    private static async Task AttackThenSkill()
    {
        var fixture = await Setup();
        CardModel attack = Add(fixture, attack: true);
        CardModel skill = Add(fixture, attack: false);
        await Play(fixture, attack);
        MoveToDiscard(fixture, attack);
        await Play(fixture, skill);
        Check(InHand(fixture, attack),
            "Attack then Skill must return the exact first Attack from the discard pile.");
        Check(!InHand(fixture, skill),
            "The later Skill must not be returned as well.");
        int handCount = fixture.PlayerState.Hand.Cards.Count;
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(),
            new CardPlay { Player = fixture.Player, Card = skill, IsAutoPlay = false,
                Target = null, ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
                Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 } });
        Check(fixture.PlayerState.Hand.Cards.Count == handCount,
            "A repeated callback after the pair resolved must not return a second card.");
    }

    private static async Task SkillThenAttack()
    {
        var fixture = await Setup();
        CardModel skill = Add(fixture, attack: false);
        CardModel attack = Add(fixture, attack: true);
        await Play(fixture, skill);
        MoveToDiscard(fixture, skill);
        await Play(fixture, attack);
        Check(InHand(fixture, skill) && !InHand(fixture, attack),
            "Skill then Attack must return the earlier Skill, not the later Attack.");
    }

    private static async Task SameTypeThenOther()
    {
        var fixture = await Setup();
        CardModel firstAttack = Add(fixture, attack: true);
        CardModel secondAttack = Add(fixture, attack: true);
        CardModel skill = Add(fixture, attack: false);
        await Play(fixture, firstAttack);
        await Play(fixture, secondAttack);
        MoveToDiscard(fixture, firstAttack);
        MoveToDiscard(fixture, secondAttack);
        await Play(fixture, skill);
        Check(InHand(fixture, firstAttack) && !InHand(fixture, secondAttack),
            "Two Attacks then a Skill must return only the first Attack.");
    }

    private static async Task OneTypeAndNewTurn()
    {
        var fixture = await Setup();
        CardModel attack = Add(fixture, attack: true);
        await Play(fixture, attack);
        MoveToDiscard(fixture, attack);
        Check(!InHand(fixture, attack) && fixture.PlayerState.Hand.Cards.Count == 0,
            "A turn with only one card type must not return the lone card.");
        await fixture.NextTurn();
        fixture.PlayerState.Energy = 40;
        CardModel skill = Add(fixture, attack: false);
        CardModel nextAttack = Add(fixture, attack: true);
        await Play(fixture, skill);
        MoveToDiscard(fixture, skill);
        await Play(fixture, nextAttack);
        Check(InHand(fixture, skill),
            "A new turn must start a fresh pair and return its earlier Skill.");
    }

    private static bool SkipMarkSeen() => false;

    private static async Task EventPages()
    {
        NativeRuntimeFixture fixture = await Setup();
        ((List<RelicModel>)fixture.Player.Relics).Clear();
        ModelDb.Inject(typeof(PhilosophersGaze));
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, null);
        typeof(PhilosophersGazeSaveRecoveryNativeChecks).GetMethod("InitializeCharacterLocalization",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, null);
        object shared = EventShared("ENC206_EVENT_PAGES", fixture);

        EventRoom room = new(ModelDb.Event<PhilosophersGaze>());
        EventModel local = room.CanonicalEvent.ToMutable();
        Set(local, "Owner", fixture.Player);
        RoomList(fixture).Clear();
        RoomList(fixture).Add(room);
        object synchronizer = RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        Set(synchronizer, "_events", new List<EventModel> { local });
        Set(synchronizer, "_playerCollection", fixture.Player.RunState!);
        Set(synchronizer, "_localPlayerId", fixture.Player.NetId);
        Set(synchronizer, "_pendingOptionTasks", new List<Task>());
        Set(RunManager.Instance, "EventSynchronizer", synchronizer);
        Action<EventModel>? onStart = (Action<EventModel>?)typeof(EventRoom)
            .GetProperty("OnStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(room);
        onStart?.Invoke(local);
        StartInitial(local);
        await local.AfterEventStarted();
        shared = CurrentState(fixture);

        Check(Keys(local).Contains("WESTERN.options.PLATO"),
            "The act-one roll call must offer Plato as a real invitation; keys=" + string.Join(",", Keys(local)));
        await Page(local, "ShowWesternThinker", "PLATO");
        Check(Keys(local).SequenceEqual(new[]
            {
                "PLATO_A.options.PLATO_B", "PLATO_A.options.PLATO_C",
                "PLATO_A.options.PLATO_D",
            }),
            "Plato's first page must offer exactly the shield, the iron tally and the brazier.");

        await Page(local, "AdvanceEastPage", PageId("PLATO_A"), OptionId("PlatoB"));
        Check(Keys(local).SequenceEqual(new[]
            {
                "PLATO_B.options.PLATO_SEAL_B", "PLATO_B.options.PLATO_DECLINE",
            }),
            "The shield page must offer the seal and a refusal, nothing else; keys="
            + string.Join(",", Keys(local)));
        await Page(local, "AdvanceEastPage", PageId("PLATO_B"), OptionId("PlatoSealB"));
        Check(fixture.Player.Relics.OfType<PlatoCivicSeal>().Count() == 1,
            "The shield accept must grant exactly one city seal.");
        Check(RouteId(shared) == "CHECKS_AND_JUSTICE",
            "The shield accept must open the checks-and-justice line toward Montesquieu.");

        NativeRuntimeFixture c = await Setup();
        ((List<RelicModel>)c.Player.Relics).Clear();
        object cShared = EventShared("ENC206_EVENT_PAGES_C", c);
        EventRoom cRoom = new(ModelDb.Event<PhilosophersGaze>());
        EventModel cLocal = cRoom.CanonicalEvent.ToMutable();
        Set(cLocal, "Owner", c.Player);
        RoomList(c).Add(cRoom);
        object cSynchronizer = RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        Set(cSynchronizer, "_events", new List<EventModel> { cLocal });
        Set(cSynchronizer, "_playerCollection", c.Player.RunState!);
        Set(cSynchronizer, "_localPlayerId", c.Player.NetId);
        Set(cSynchronizer, "_pendingOptionTasks", new List<Task>());
        Set(RunManager.Instance, "EventSynchronizer", cSynchronizer);
        Action<EventModel>? cOnStart = (Action<EventModel>?)typeof(EventRoom)
            .GetProperty("OnStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(cRoom);
        cOnStart?.Invoke(cLocal);
        StartInitial(cLocal);
        await cLocal.AfterEventStarted();
        cShared = CurrentState(c);
        await Page(cLocal, "ShowWesternThinker", "PLATO");
        await Page(cLocal, "AdvanceEastPage", PageId("PLATO_A"), OptionId("PlatoC"));
        Check(Keys(cLocal).SequenceEqual(new[]
            {
                "PLATO_C.options.PLATO_SEAL_C", "PLATO_C.options.PLATO_DECLINE",
            }),
            "The iron tally page must offer the seal and a refusal, nothing else.");
        await Page(cLocal, "AdvanceEastPage", PageId("PLATO_C"), OptionId("PlatoSealC"));
        Check(c.Player.Relics.OfType<PlatoCivicSeal>().Count() == 1
              && RouteId(cShared) == "SYMPATHY_AND_PUBLIC_ACTION",
            "The iron tally accept must grant one seal and open the sympathy line toward Adam Smith.");

        foreach ((string page, string exit, string label) in new[]
                 {
                     ("PLATO_B", "PlatoDecline", "shield refusal"),
                     ("PLATO_C", "PlatoDecline", "tally refusal"),
                     ("PLATO_D", "PlatoLeave", "brazier leave"),
                 })
        {
            NativeRuntimeFixture d = await Setup();
            ((List<RelicModel>)d.Player.Relics).Clear();
            object dShared = EventShared("ENC206_EVENT_PAGES_D", d);
            EventRoom dRoom = new(ModelDb.Event<PhilosophersGaze>());
            EventModel dLocal = dRoom.CanonicalEvent.ToMutable();
            Set(dLocal, "Owner", d.Player);
            RoomList(d).Add(dRoom);
            object dSynchronizer = RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
            Set(dSynchronizer, "_events", new List<EventModel> { dLocal });
            Set(dSynchronizer, "_playerCollection", d.Player.RunState!);
            Set(dSynchronizer, "_localPlayerId", d.Player.NetId);
            Set(dSynchronizer, "_pendingOptionTasks", new List<Task>());
            Set(RunManager.Instance, "EventSynchronizer", dSynchronizer);
            Action<EventModel>? dOnStart = (Action<EventModel>?)typeof(EventRoom)
                .GetProperty("OnStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(dRoom);
            dOnStart?.Invoke(dLocal);
            StartInitial(dLocal);
            await dLocal.AfterEventStarted();
            dShared = CurrentState(d);
            await Page(dLocal, "ShowWesternThinker", "PLATO");
            if (page == "PLATO_D")
                await Page(dLocal, "AdvanceEastPage", PageId("PLATO_A"), OptionId("PlatoD"));
            else
                await Page(dLocal, "AdvanceEastPage", PageId("PLATO_A"),
                    page == "PLATO_B" ? OptionId("PlatoB") : OptionId("PlatoC"));
            await Page(dLocal, "AdvanceEastPage", PageId(page), OptionId(exit));
            Check(dLocal.IsFinished && d.Player.Relics.OfType<PlatoCivicSeal>().Count() == 0
                  && Get(dShared, "WesternJourney") is null,
                $"The {label} must finish without a seal or a Western route.");
        }
    }

    private static void StartInitial(EventModel local)
    {
        object options = typeof(EventModel)
            .GetMethod("GenerateInitialOptionsWrapper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(local, null)!;
        object description = typeof(PhilosophersGaze)
            .GetMethod("PageDescription", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(local, [PageId("Initial")])!;
        typeof(EventModel).GetMethod("SetEventState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(local, [description, options]);
    }

    private static object CurrentState(NativeRuntimeFixture fixture)
    {
        object?[] args = [fixture.Player.RunState!, null];
        Call("PhilosophyRunStateService", "TryGet", args);
        return args[1] ?? throw new InvalidOperationException("The event fixture has no live philosophy state.");
    }

    private static object EventShared(string key, NativeRuntimeFixture fixture)
    {
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object candidates = Activator.CreateInstance(T("GeneratedCandidates"), nonPublic: true)!;
        Set(candidates, "GenerationKey", key);
        Set(candidates, "CandidateIds", new List<string> { "PLATO", "KONGZI", "MOZI" });
        ((System.Collections.IDictionary)Get(shared, "GeneratedCandidates")!)[
            "ACT_ONE_THINKERS"] = candidates;
        Call("PhilosophyRunStateService", "Restore", fixture.Player.RunState!, shared);
        return shared;
    }

    private static System.Collections.IList RoomList(NativeRuntimeFixture fixture) =>
        (System.Collections.IList)Get(fixture.Player.RunState!, "_currentRooms")!;

    private static object PageId(string name) => Enum.Parse(T("PhilosophersGazePage"), name);
    private static object OptionId(string name) => Enum.Parse(T("PhilosophersGazeOption"), name);
    private static string? RouteId(object shared) => Get(shared, "WesternJourney") is { } journey
        ? (string?)Get(journey, "RouteId") : null;

    private static async Task Page(EventModel local, string method, params object?[] args)
    {
        Task task = (Task)typeof(PhilosophersGaze)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(local, args)!;
        await task;
    }

    private static string[] Keys(EventModel local) => local.CurrentOptions
        .Select(option => option.HistoryName.LocEntryKey
            .Replace("PHILOSOPHERS_GAZE.pages.", "")
            .Replace(".title", ""))
        .ToArray();

    private static async Task AutoPlayIgnored()
    {
        var fixture = await Setup();
        CardModel autoAttack = Add(fixture, attack: true);
        fixture.PlayerState.Hand.RemoveInternal(autoAttack, silent: true);
        fixture.PlayerState.PlayPile.AddInternal(autoAttack, silent: true);
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = autoAttack, IsAutoPlay = true,
            Target = fixture.Enemy, ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        });
        CardModel skill = Add(fixture, attack: false);
        CardModel manualAttack = Add(fixture, attack: true);
        await Play(fixture, skill);
        MoveToDiscard(fixture, skill);
        await Play(fixture, manualAttack);
        Check(InHand(fixture, skill) && !InHand(fixture, autoAttack),
            "An automatic Attack must not become the recorded first Attack; the manual Skill is still returned.");
    }

    private static async Task ExhaustedFirstCard()
    {
        var fixture = await Setup();
        CardModel attack = Add(fixture, attack: true);
        CardModel skill = Add(fixture, attack: false);
        await Play(fixture, attack);
        MoveToExhaust(fixture, attack);
        await Play(fixture, skill);
        Check(!InHand(fixture, attack) && fixture.PlayerState.ExhaustPile.Cards.Contains(attack),
            "A first card that left the discard pile for Exhaust must not be returned.");
    }

    private static async Task SaveRoundTripNoDuplicate()
    {
        var fixture = await Setup();
        var seal = (PlatoCivicSeal)fixture.Relic;
        CardModel attack = Add(fixture, attack: true);
        CardModel skill = Add(fixture, attack: false);
        await Play(fixture, attack);
        MoveToDiscard(fixture, attack);
        await Play(fixture, skill);
        Check(InHand(fixture, attack), "The pair must return the first card before the save check.");
        string payload = seal.PlatoCivicTurnPayload;
        Check(payload.Length > 0, "A resolved pair must leave a saved turn payload.");
        typeof(PlatoCivicSeal).GetProperty(nameof(PlatoCivicSeal.PlatoCivicTurnPayload))!.SetValue(seal, payload);
        int handCount = fixture.PlayerState.Hand.Cards.Count;
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(),
            new CardPlay { Player = fixture.Player, Card = skill, IsAutoPlay = false,
                Target = null, ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
                Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 } });
        Check(fixture.PlayerState.Hand.Cards.Count == handCount,
            "Restoring the same saved payload must not duplicate the once-per-turn return.");
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? priorRun = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        object? priorExecutor = manager.ActionExecutor;
        object? priorNetwork = manager.NetService;
        bool priorSave = manager.ShouldSave;
        bool priorTest = TestMode.IsOn;
        List<Exception> failures = [];
        var eventHarmony = new Harmony("STS2Philosophers.ENC206.EventFixture");
        MethodInfo markSeen = typeof(SaveManager).GetMethod(nameof(SaveManager.MarkRelicAsSeen),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(RelicModel)])!;
        eventHarmony.Patch(markSeen, prefix: new HarmonyMethod(typeof(PlatoCivicSealNativeChecks)
            .GetMethod(nameof(SkipMarkSeen), BindingFlags.Static | BindingFlags.NonPublic)!));
        async Task Scenario(string name, Func<Task> run)
        {
            try { await run(); Console.WriteLine($"ENC206 PASS {name}"); }
            catch (Exception error) { failures.Add(new InvalidOperationException(name, error)); Console.WriteLine($"ENC206 FAIL {name}: {error.Message}"); }
        }
        try
        {
            TestMode.IsOn = true;
            await Scenario("attack then skill", AttackThenSkill);
            await Scenario("skill then attack", SkillThenAttack);
            await Scenario("same type then other", SameTypeThenOther);
            await Scenario("one type and new turn", OneTypeAndNewTurn);
            await Scenario("automatic play ignored", AutoPlayIgnored);
            await Scenario("exhausted first card", ExhaustedFirstCard);
            await Scenario("save round trip no duplicate", SaveRoundTripNoDuplicate);
            await Scenario("event pages and grants", EventPages);
            if (failures.Count != 0) throw new AggregateException("ENC206 native seal failures", failures);
            Console.WriteLine("ENC206 native checks passed: A/B/C/D page flow, both seal-granting routes and all three no-reward exits, exact first-card return, both play orders, same-type runs, one-type turns, automatic plays, Exhaust removal, new-turn reset, duplicate callbacks and saved-payload idempotency.");
        }
        finally
        {
            Set(manager, "State", priorRun);
            Set(manager, "ActionExecutor", priorExecutor);
            Set(manager, "NetService", priorNetwork);
            Set(manager, "ShouldSave", priorSave);
            TestMode.IsOn = priorTest;
            eventHarmony.Unpatch(markSeen, HarmonyPatchType.Prefix, eventHarmony.Id);
        }
    }
}
