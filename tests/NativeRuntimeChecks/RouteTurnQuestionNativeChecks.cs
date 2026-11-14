using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;

// Proves the first act's question actually opens at each entry that needs it and
// that answering it names the line. The graph can name every line and still leave
// a run with no room to answer in, so this check pulls the real native event.
internal static class RouteTurnQuestionNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(DescartesThinkingLens).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Methods)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static void Put(object value, string name, object? item) => value.GetType().GetProperty(name, Methods)!.SetValue(value, item);
    private static void Set(object value, string name, object? item) => value.GetType().GetField(name, Methods)!.SetValue(value, item);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    // entry, relic, the edge the answer takes, the line it must name, and whether
    // the shared figure needs the direction question to name the line.
    // entry, relic, the edge the answer takes, the line it must name, whether the
    // shared figure needs the direction question, and the entry's own rooms that a
    // run may already have played at an earlier question node.
    private static readonly (string Node, Type Relic, string Edge, string Route, bool Direction, string[] Prior)[] Cases =
    [
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_153", "AUTONOMY_AND_HISTORY", false, []),
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_189", "UNCONSCIOUS_AND_NORM", false, []),
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_160", "EMBODIED_PERCEPTION", true, []),
        ("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE", typeof(SocratesQuestionCup), "WESTERN_EDGE_165", "SCIENTIFIC_MODEL", false, []),
        ("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE", typeof(SocratesQuestionCup), "WESTERN_EDGE_069", "FALLIBLE_INQUIRY", true, []),
        ("ROUSSEAU__HISTORY_AND_POWER__CORE", typeof(RousseauUncarvedStone), "WESTERN_EDGE_197", "SOCIAL_KNOWLEDGE", false, ["MillHarm"]),
        ("ROUSSEAU__HISTORY_AND_POWER__CORE", typeof(RousseauUncarvedStone), "WESTERN_EDGE_177", "SOCIAL_CRITIQUE", false, ["MillHarm"]),
        ("ROUSSEAU__HISTORY_AND_POWER__CORE", typeof(RousseauUncarvedStone), "WESTERN_EDGE_180", "GENEALOGY_AND_POWER", false, ["MillHarm"]),
        ("ROUSSEAU__HISTORY_AND_POWER__CORE", typeof(RousseauUncarvedStone), "WESTERN_EDGE_192", "EDUCATION_AND_SITUATION", false, ["MillHarm"]),
        ("ARISTOTLE__LANGUAGE_AND_MEANING__CORE", typeof(AristotleCategoryTablet), "WESTERN_EDGE_186", "STRUCTURE_AND_EXCLUSION", false, []),
        ("PLATO__FREEDOM_AND_INSTITUTIONS__CORE", typeof(PlatoCivicSeal), "WESTERN_EDGE_174", "SYMPATHY_AND_PUBLIC_ACTION", false, []),
    ];

    public static Task Run()
    {
        ModelDb.Inject(T("RouteTurnQuestion"));
        CheckKnowledgeAnswerAndContinuation();
        CheckRouteOnlyDirection();
        CheckAdornoDirection();
        foreach (var item in Cases)
        {
            var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [item.Relic, 0])!;
            var run = (RunState)tuple[1]!;
            object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
            object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [item.Node])!;
            Put(journey, "RouteRules", true);
            Put(journey, "LastFixedAct", 1);
            object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
            object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
            string id = Snake(item.Relic.Name);
            Put(doctrine, "ThinkerId", id.Split('_')[0]);
            Put(doctrine, "DoctrineId", id);
            Put(shared, "CurrentDoctrine", doctrine);
            Put(shared, "WesternJourney", journey);
            object table = T("PhilosophyRunStateRitsuStore").GetField("IsolatedRuns", Methods)!.GetValue(null)!;
            table.GetType().GetMethod("Remove")!.Invoke(table, [run]);
            Call("PhilosophyRunStateService", "ReplaceState", run, shared);
            Call("PhilosophyRunStateService", "MarkResolved", run);
            // A run reaches later question nodes with the entry's own rooms behind
            // it; those rooms keep their own node and are not asked again.
            foreach (string prior in item.Prior)
                ((HashSet<ModelId>)run.VisitedEventIds).Add((ModelId)typeof(ModelDb).GetMethods(Methods)
                    .Single(method => method.Name == "GetId" && method.IsGenericMethodDefinition
                        && method.GetParameters().Length == 0)
                    .MakeGenericMethod(T(prior)).Invoke(null, null)!);
            Set(run, "_currentRooms", new List<AbstractRoom>());
            run.CurrentMapPoint!.PointType = MapPointType.Unknown;

            EventModel chosen = run.Act.PullNextEvent(run);
            Check(chosen.GetType() == T("RouteTurnQuestion"),
                $"{item.Node}: the entry's question must open as RouteTurnQuestion, got {chosen.GetType().Name}.");
            Check(run.VisitedEventIds.Contains(chosen.Id),
                $"{item.Node}: the question's identity must be visited without a second event.");

            // A fork left open is still there after a load: the node it was asked
            // on is the only place the answer can be given, so losing it loses the
            // line. Closing the record is what makes it stop coming back.
            object?[] live = [run, null];
            Call("PhilosophyRunStateService", "TryGet", live);
            object record = Get(live[1]!, "RouteTurnQuestion")!;
            Check(record is not null && !(bool)Get(record, "Closed"),
                $"{item.Node}: presenting the question must leave an open record.");
            // A checkpoint may record the question without its room: the record is
            // then the only thing left, and it has to bring the question back.
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Check(Call("RouteTurnQuestionPatch", "TrySelectExistingNative", run) is not null,
                $"{item.Node}: an open question must resume on its own node.");
            Put(record, "Closed", true);
            Check(Call("RouteTurnQuestionPatch", "TrySelectExistingNative", run) is null,
                $"{item.Node}: an answered question must not come back.");
            Put(record, "Closed", false);

            // A record that cannot be read is no answer: the fork must still be
            // available rather than closed by a shape the run cannot use.
            Put(record, "Version", 0);
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Check(Call("RouteTurnQuestionPatch", "TrySelectNative", run) is not null,
                $"{item.Node}: an unreadable record must not spend the fork.");
            Put(record, "Version", 1);

            // The act's fixed event takes the slot; once it is taken the question
            // is no longer this act's question.
            Put(record, "Closed", true);
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Put(journey, "LastFixedAct", 2);
            Check(Call("RouteTurnQuestionPatch", "TrySelectNative", run) is null,
                $"{item.Node}: a taken fixed slot must close the act's question.");
            Put(journey, "LastFixedAct", 1);
            Put(record, "Closed", false);

            // The fork is asked once per run: an answered record means the run
            // already chose, so the next question node must not ask it again.
            Put(record, "Closed", true);
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Check(Call("RouteTurnQuestionPatch", "TrySelectNative", run) is null,
                $"{item.Node}: an answered fork must not be asked again.");
            Put(record, "Closed", false);

            // Answering is the graph's own step: the edge names the line, and the
            // shared figure's edge is followed by the direction the answer gives.
            Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [journey, item.Edge])!,
                $"{item.Node}: {item.Edge} must be an eligible first-act edge.");
            if (item.Direction)
                Check((bool)graph.GetType().GetMethod("TryNameRoute")!.Invoke(graph, [journey, item.Route])!,
                    $"{item.Node}: the direction must be nameable.");
            Check((string)Get(journey, "RouteId") == item.Route,
                $"{item.Node}: answering with {item.Edge} must name {item.Route}, got {Get(journey, "RouteId")}.");
            Console.WriteLine($"Route turn question: {item.Node} opens the question and {item.Edge} names {item.Route}.");
        }
        return Task.CompletedTask;
    }

    private static void ChooseAndSave(NativeRuntimeFixture fixture, RunState run,
        EventModel canonical, EventModel local, object journey, object direction)
    {
        var acts = run.Acts;
        try
        {
            using var memory = new NativeEventMemorySaveFixture(fixture, run);
            object sync = RuntimeHelpers.GetUninitializedObject(typeof(RunManager)
                .GetProperty("EventSynchronizer")!.PropertyType);
            NativeRuntimeFixture.Set(sync, "_events", new List<EventModel> { local });
            NativeRuntimeFixture.Set(sync, "_playerCollection", run); NativeRuntimeFixture.Set(sync, "_localPlayerId", run.Players[0].NetId);
            NativeRuntimeFixture.Set(sync, "_pendingOptionTasks", new List<Task>());
            NativeRuntimeFixture.Set(RunManager.Instance, "EventSynchronizer", sync);
            NativeRuntimeFixture.Set(run, "_currentRooms", new List<AbstractRoom> { new EventRoom(canonical) });
            typeof(RouteTurnQuestion).GetMethod("SetInitialEventState", Methods)!.Invoke(local, [false]);
            ((Task)typeof(RouteTurnQuestion).GetMethod("Choose", Methods)!
                .Invoke(local, [journey, direction])!).GetAwaiter().GetResult();
            Check(local.IsFinished && memory.SaveAttempts > 0
                && memory.Json.Contains("ROUTE_TURN_QUESTION", StringComparison.Ordinal)
                && !(bool)typeof(RouteTurnQuestion).GetField("_blocked", Methods)!.GetValue(local)!,
                "The real route page completes only after a native memory checkpoint.");
        }
        finally
        {
            NativeRuntimeFixture.Set(run, "Acts", acts); NativeRuntimeFixture.Set(run, "_currentRooms", new List<AbstractRoom>());
        }
    }

    private static void CheckKnowledgeAnswerAndContinuation()
    {
        ModelDb.Inject(typeof(PeirceInquiry));
        ModelDb.Inject(typeof(JamesPragmatism));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesQuestionCup), 0])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "RouteRules", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;
        EventModel selected = run.Act.PullNextEvent(run);
        Check(selected is RouteTurnQuestion,
            "The real knowledge entry must ask the route fork before Sextus, got " + selected.GetType().Name + ".");
        Check(!run.VisitedEventIds.Contains(ModelDb.GetId<SextusOpposedAppearances>()),
            "The route fork must not pre-visit the local Sextus question.");
        EventModel local = selected.ToMutable();
        NativeRuntimeFixture.Set(local, "Owner", run.Players[0]);
        object choices = T("RouteTurnQuestionOptions").GetMethod("For", Methods)!.Invoke(null, [journey, graph])!;
        object peirce = ((System.Collections.IEnumerable)choices).Cast<object>()
            .Single(choice => (string)choice.GetType().GetProperty("Key")!.GetValue(choice)! == "TO_PEIRCE");
        ChooseAndSave((NativeRuntimeFixture)tuple[0]!, run, selected, local, journey, peirce);
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_069")
            && (string)Get(journey, "RouteId") == "FALLIBLE_INQUIRY"
            && ((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(1),
            "Choosing Peirce on the real fork takes 069, names the fallible line, and spends act one's question.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: PeirceInquiry },
            "The same real fork reaches Peirce at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "PeirceInquiry");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "TestByConsequences", "Leave" })
        {
            object action = Enum.Parse(T("PeirceInquiryAction"), name);
            Check((bool)Call("PeirceInquiryFlow", "TryPrepare", state, action)!
                && (bool)Call("PeirceInquiryFlow", "TryCommitPending", state, journey, graph)!,
                "The real Peirce continuation commits " + name + ".");
        }
        Set(run, "_currentRooms", new List<AbstractRoom>());
        EventModel james = run.Act.PullNextEvent(run);
        Check(james is JamesPragmatism,
            "The same real run selects James after Peirce, got " + james.GetType().Name + ".");
    }

    private static void CheckAdornoDirection()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(RousseauUncarvedStone), 0])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ROUSSEAU__HISTORY_AND_POWER__CORE"])!;
        Put(journey, "RouteRules", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;
        EventModel selected = run.Act.PullNextEvent(run);
        Check(selected is RouteTurnQuestion,
            "The real Rousseau entry must ask the route fork, got " + selected.GetType().Name + ".");
        EventModel local = selected.ToMutable();
        NativeRuntimeFixture.Set(local, "Owner", run.Players[0]);
        object choices = T("RouteTurnQuestionOptions").GetMethod("For", Methods)!
            .Invoke(null, [journey, graph])!;
        object adorno = ((System.Collections.IEnumerable)choices).Cast<object>()
            .Single(choice => (string)choice.GetType().GetProperty("Key")!.GetValue(choice)! == "TO_ADORNO");
        ChooseAndSave((NativeRuntimeFixture)tuple[0]!, run, selected, local, journey, adorno);
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_083")
            && (string)Get(journey, "RouteId") == "SOCIAL_CRITIQUE"
            && (string)Get(journey, "CurrentNodeId") == "KARL_MARX__HISTORY_AND_POWER__EARLY"
            && (int)Get(journey, "LastFixedAct") == 2,
            "Choosing the social-critique fixed branch must apply 083, name the line, and stand on Marx.");
    }

    private static void CheckRouteOnlyDirection()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(AristotleCategoryTablet), 0])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ARISTOTLE__LANGUAGE_AND_MEANING__CORE"])!;
        Put(journey, "RouteRules", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;
        EventModel selected = run.Act.PullNextEvent(run);
        Check(selected is RouteTurnQuestion,
            "The real Aristotle entry must ask the route fork, got " + selected.GetType().Name + ".");
        EventModel local = selected.ToMutable();
        NativeRuntimeFixture.Set(local, "Owner", run.Players[0]);
        object choices = T("RouteTurnQuestionOptions").GetMethod("For", Methods)!
            .Invoke(null, [journey, graph])!;
        object ordinary = ((System.Collections.IEnumerable)choices).Cast<object>()
            .Single(choice => (string)choice.GetType().GetProperty("Key")!.GetValue(choice)! == "TO_WITTGENSTEIN");
        ChooseAndSave((NativeRuntimeFixture)tuple[0]!, run, selected, local, journey, ordinary);
        var completed = (HashSet<string>)Get(journey, "CompletedEdgeIds");
        Check((string)Get(journey, "RouteId") == "ORDINARY_SPEECH"
            && ((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(1)
            && !completed.Contains("WESTERN_EDGE_183") && !completed.Contains("WESTERN_EDGE_186")
            && (int)Get(journey, "LastFixedAct") == 1
            && (string)Get(journey, "CurrentNodeId") == "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            "Choosing ordinary language must name the line and spend act one without moving or taking a fixed slot.");
    }

    private static string Snake(string name)
    {
        var text = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append('_');
            text.Append(char.ToUpperInvariant(c));
        }
        return text.ToString();
    }
}
