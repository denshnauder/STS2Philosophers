using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class EasternQuestionEntryNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type T(string name) => typeof(HeraclitusLyre).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object[] args) => T(type).GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
    private static string Encode(object state) => (string)Call("PhilosophyRunStateCodec", "Encode", state)!;
    private static readonly Type[] Starters =
    [typeof(KongziMuduo), typeof(KongziQingYuPei), typeof(MoziMoSeZhuJian), typeof(MoziShouChengTu), typeof(LaoziWuWeiShuJian), typeof(LaoziShuiYu)];

    public static Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer;
        object priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Flags)!.GetValue(CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            foreach (Type model in new[] { typeof(PhilosophersGaze), typeof(MengziXiongZhang), typeof(XunziShengMo), typeof(QinGuliShouChengXie), typeof(HuishiLiWuChou), typeof(ZhuangziDaHu), typeof(YangzhuQuanShengBi) }) ModelDb.Inject(model);
            foreach (Type starter in Starters)
            {
                var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
                    .Invoke(null, [typeof(HeraclitusLyre), 1])!;
                var fixture = (NativeRuntimeFixture)tuple[0]!;
                var run = (RunState)tuple[1]!;
                object pool = tuple[3]!;
                EventModel original = (EventModel)tuple[4]!;
                ModelDb.Inject(starter);
                RelicModel carrier = ModelDb.GetById<RelicModel>(ModelDb.GetId(starter)).ToMutable(); carrier.Owner = fixture.Player;
                ((List<RelicModel>)fixture.Player.Relics).Clear(); ((List<RelicModel>)fixture.Player.Relics).Add(carrier);
                object shared = Activator.CreateInstance(T("PhilosophyRunState"), true)!;
                Call("PhilosophyRunStateService", "Restore", run, shared);
                // The opening uses this same canonical ID. Native hook timing
                // must permit the separate second-act continuation slot.
                ((HashSet<ModelId>)run.VisitedEventIds).Add(ModelDb.GetId<PhilosophersGaze>());
                string before = Encode(shared), rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
                int queue = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
                run.CurrentMapPoint!.PointType = MapPointType.Monster;
                Check(ReferenceEquals(carrier.ModifyNextEvent(original), original), starter.Name + ": a combat node cannot become the continuation.");
                run.CurrentMapPoint.PointType = MapPointType.Unknown;
                Set(run, "_currentRooms", new List<AbstractRoom> { new MapRoom() });
                Check(ReferenceEquals(carrier.ModifyNextEvent(original), original), starter.Name + ": observing a map room cannot manufacture the continuation.");
                Set(run, "_currentRooms", new List<AbstractRoom>());
                Set(run, "_currentActIndex", 0);
                Check(ReferenceEquals(carrier.ModifyNextEvent(original), original), starter.Name + ": the shared opening ModelId cannot turn a first-act question into act-two continuation.");
                Set(run, "_currentActIndex", 1);
                EventModel chosen = run.Act.PullNextEvent(run);
                Check(chosen is PhilosophersGaze && run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id),
                    starter.Name + ": actual native PullNextEvent calls the Eastern carrier and permits act-two Gaze despite the opening's visited ID.");
                Check((int)shared.GetType().GetProperty("PhilosophersGazeFixedEntryAct", Flags)!.GetValue(shared)! == 2,
                    starter.Name + ": the first Eastern entry claims the act-two fixed slot.");
                shared.GetType().GetProperty("PhilosophersGazeFixedEntryAct", Flags)!.SetValue(shared, 0);
                Check(Encode(shared) == before && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
                    && (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)! == queue,
                    starter.Name + ": native continuation selection consumes no Western slot, new RNG or additional event queue item.");
                EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(new EventRoom(chosen).ToSerializable(), null!)!;
                Check(restored.CanonicalEvent is PhilosophersGaze && Equals(restored.ToSerializable().EventId, chosen.Id),
                    starter.Name + ": its native room checkpoint retains the chosen canonical identity.");
                carrier.GetType().GetMethod("RecordPhilosophersGazeContinuation", Flags)!.Invoke(carrier, null);
                Check(ReferenceEquals(carrier.ModifyNextEvent(original), original),
                    starter.Name + ": the actual durable resolved flag prevents a second continuation.");
            }
            CheckHanFeiHook();
            CheckWuWeiTemporaryDexterity().GetAwaiter().GetResult();
            CheckQinBlockTiming().GetAwaiter().GetResult();
            CheckActTwoFixedEntry();
            Console.WriteLine("Eastern question-entry checks passed: all six real starter relic hooks through native PullNextEvent, act-two shared-ID/visited behavior, map/combat rejection, unchanged Western state/RNG/queue, native room identity and actual resolved-flag prevention; the ancient-boundary fixed entry opens the correct successor page without a question, spends its slot once and survives the codec; Wu Wei applies temporary Dexterity and Qin gains no Block at turn start; Xunzi public Han Fei hook. No rendered UI, full run replay or player-save access.");
            return Task.CompletedTask;
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static void CheckHanFeiHook()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(HeraclitusLyre), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!; var run = (RunState)tuple[1]!;
        EventModel original = (EventModel)tuple[4]!;
        ModelDb.Inject(typeof(XunziShengMo)); ModelDb.Inject(typeof(HanFeiPublicStandard));
        RelicModel carrier = ModelDb.GetById<RelicModel>(ModelDb.GetId<XunziShengMo>()).ToMutable(); carrier.Owner = fixture.Player;
        ((List<RelicModel>)fixture.Player.Relics).Clear(); ((List<RelicModel>)fixture.Player.Relics).Add(carrier);
        object shared = typeof(AllRouteQuestionNodeChecks).GetMethod("BuildShared", Flags)!
            .Invoke(null, ["HanFeiPublicStandard", "", carrier.Id.Entry, run])!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is HanFeiPublicStandard && run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id),
            "Xunzi's actual public relic hook selects Han Fei and native visited registration records only the final model.");
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check(ReferenceEquals(carrier.ModifyNextEvent(original), original), "The visited Han Fei identity prevents repeated selection through Xunzi's actual hook.");
    }

    private static async Task CheckWuWeiTemporaryDexterity()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(HeraclitusLyre), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ModelDb.Inject(typeof(LaoziWuWeiShuJian));
        ModelDb.Inject(typeof(LaoziWuWeiTemporaryDexterityPower));
        ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Powers.DexterityPower));
        var relic = (LaoziWuWeiShuJian)ModelDb.GetById<RelicModel>(ModelDb.GetId<LaoziWuWeiShuJian>()).ToMutable();
        relic.Owner = fixture.Player;
        ((List<RelicModel>)fixture.Player.Relics).Add(relic);
        await fixture.Begin();
        typeof(LaoziWuWeiShuJian).GetProperty("PendingRewardTurn", Flags)!.SetValue(relic, fixture.PlayerState.TurnNumber);
        await relic.AfterPlayerTurnStart(null!, fixture.Player);
        var power = fixture.Player.Creature.Powers.OfType<LaoziWuWeiTemporaryDexterityPower>().SingleOrDefault();
        Check(power is { Amount: 2 } && power.GetType().BaseType == typeof(MegaCrit.Sts2.Core.Models.Powers.TemporaryDexterityPower),
            "Wu Wei's next-turn reward must apply two temporary Dexterity, not permanent Dexterity.");
    }

    private static async Task CheckQinBlockTiming()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(HeraclitusLyre), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ModelDb.Inject(typeof(QinGuliShouChengXie));
        var relic = (QinGuliShouChengXie)ModelDb.GetById<RelicModel>(ModelDb.GetId<QinGuliShouChengXie>()).ToMutable();
        relic.Owner = fixture.Player;
        ((List<RelicModel>)fixture.Player.Relics).Add(relic);
        await fixture.Begin();
        int before = fixture.Player.Creature.Block;
        await relic.AfterPlayerTurnStartLate(null!, fixture.Player);
        Check(fixture.Player.Creature.Block == before,
            "City Defense Machinery must not grant Block at turn start; its Block belongs to the owner's turn end.");
    }

private static void CheckActTwoFixedEntry()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(HeraclitusLyre), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        ModelDb.Inject(typeof(PhilosophersGaze));
        ModelDb.Inject(typeof(KongziMuduo));
        RelicModel carrier = ModelDb.GetById<RelicModel>(ModelDb.GetId<KongziMuduo>()).ToMutable();
        carrier.Owner = fixture.Player;
        ((List<RelicModel>)fixture.Player.Relics).Clear();
        ((List<RelicModel>)fixture.Player.Relics).Add(carrier);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), true)!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);

        object?[] first = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", first)!,
            "A Kongzi root relic must open the act-two fixed entry after the ancient without a Western journey.");
        var room = (EventRoom)first[1]!;
        Check(room.CanonicalEvent is PhilosophersGaze,
            "The act-two Eastern fixed entry must open PhilosophersGaze, got " + room.CanonicalEvent.GetType().Name + ".");
        Check((int)shared.GetType().GetProperty("PhilosophersGazeFixedEntryAct", Flags)!.GetValue(shared)! == 2,
            "The Eastern fixed entry must record that act two's continuation slot was spent.");
        object decoded = Call("PhilosophyRunStateCodec", "Decode", Encode(shared))!;
        Check((int)decoded.GetType().GetProperty("PhilosophersGazeFixedEntryAct", Flags)!.GetValue(decoded)! == 2,
            "The spent act-two fixed slot must survive a save/load codec round-trip.");

        EventModel local = room.CanonicalEvent.ToMutable();
        Set(local, "Owner", fixture.Player);
        typeof(PhilosophersGaze).GetMethod("SetInitialEventState", Flags)!.Invoke(local, [false]);
        Check(local.InitialDescription.LocEntryKey.EndsWith(".XUNZI_INTRO.description", StringComparison.Ordinal)
              && local.CurrentOptions.Count == 2
              && local.CurrentOptions.All(option => option.HistoryName.LocEntryKey.Contains("XUNZI_INTRO")),
            "The fixed entry must resume directly at Xunzi's introduction, not the old continuation menu.");

        object?[] second = [run, null];
        Check(!(bool)Call("ActFixedEntryHost", "TryPrepare", second)!,
            "The same act-two boundary must not present the Eastern continuation a second time.");
    }

}
