using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Proves the act-boundary entry: with the journey at the second act's fixed
// position, a room is selected without any unknown map point, and the same
// run gets nothing once the position is spent. The run is an in-memory fixture.
internal static class ActBoundaryFixedEntryNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(SpinozaCauseNetwork).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Methods)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static object GetField(object value, string name) => value.GetType().GetField(name, Methods)!.GetValue(value)!;
    private static void Put(object value, string name, object? item) => value.GetType().GetProperty(name, Methods)!.SetValue(value, item);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static Task Run()
    {
        ModelDb.Inject(typeof(SpinozaCauseNetwork));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 2);
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 1);
        ModelDb.Inject(typeof(EpicurusSharedMargin));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 3, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 2, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        ModelDb.Inject(typeof(MontesquieuBalance));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MontesquieuBalance)], doctrine: false, position: 2, route: "CHECKS_AND_JUSTICE");
        ModelDb.Inject(typeof(SmithSympathy));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(SmithSympathy)], doctrine: false, position: 2, route: "SYMPATHY_AND_PUBLIC_ACTION");
        ModelDb.Inject(typeof(HusserlEpoche));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(HusserlEpoche)], doctrine: true, position: 2, route: "APPEARANCE_AND_INTERPRETATION");
        ModelDb.Inject(typeof(FregeSense));
        CheckBoundary(typeof(SocratesBalanceWeight), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(FregeSense)], doctrine: false, position: 2, route: "FORMAL_LANGUAGE");
        ModelDb.Inject(typeof(LockeCargoIdentity));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(LockeCargoIdentity)], doctrine: true, position: 2, route: "EXPERIENCE_AND_SELF");
        // The rooms the routes place later in the run: the boundary has to hand
        // over the act they were placed in, not the act they were written for.
        ModelDb.Inject(typeof(FreudUnconscious));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(FreudUnconscious)], doctrine: true, position: 2, route: "UNCONSCIOUS_AND_NORM");
        ModelDb.Inject(typeof(LacanSignifier));
        CheckBoundary(typeof(DescartesThinkingLens), "SIGMUND_FREUD__SELF_AND_OTHER__CORE",
            [typeof(LacanSignifier)], doctrine: true, position: 2, actIndex: 2,
            route: "UNCONSCIOUS_AND_NORM", required: ["WESTERN_EDGE_087"]);
        ModelDb.Inject(typeof(KantUniversalization));
        CheckBoundary(typeof(DescartesThinkingLens), "MONTESQUIEU__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(KantUniversalization)], doctrine: true, position: 2, actIndex: 2,
            route: "CHECKS_AND_JUSTICE", required: ["WESTERN_EDGE_114"]);
        // The second act of the autonomy line. Locke's room sits earlier in the
        // dispatch chain and opens on the same node, so this also proves the
        // route gate keeps a room of another line from taking the slot.
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(KantApperception)], doctrine: false, position: 2, actIndex: 1,
            route: "AUTONOMY_AND_HISTORY");
        // The rest of the second acts, one room each: the boundary has to hand
        // the slot to the figure the line names, not to whatever opens first.
        ModelDb.Inject(typeof(SaussureSign));
        CheckBoundary(typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(SaussureSign)], doctrine: false, position: 2, route: "STRUCTURE_AND_EXCLUSION");
        ModelDb.Inject(typeof(BenthamUtility));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(BenthamUtility)], doctrine: false, position: 2, route: "CONSEQUENCE_AND_FREEDOM", checkRoom: true);
        ModelDb.Inject(typeof(PeirceInquiry));
        CheckBoundary(typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(PeirceInquiry)], doctrine: false, position: 2, route: "FALLIBLE_INQUIRY");
        ModelDb.Inject(typeof(NietzscheRevaluation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(NietzscheRevaluation)], doctrine: false, position: 2, route: "GENEALOGY_AND_POWER", checkRoom: true);
        ModelDb.Inject(typeof(ComtePositivism));
        // Default social-knowledge line: Bacon in the second act, Comte in the third.
        CheckBoundary(typeof(RousseauUncarvedStone), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(ComtePositivism)], doctrine: false, position: 3, actIndex: 2,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_140"], checkRoom: true);
        // Early first-act Bacon turn: Comte moves to the second act.
        CheckBoundary(typeof(RousseauUncarvedStone), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(ComtePositivism)], doctrine: false, position: 2, actIndex: 1,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_197"], checkRoom: true);
        ModelDb.Inject(typeof(MarxAlienation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(MarxAlienation)], doctrine: false, position: 2, route: "SOCIAL_CRITIQUE");
        // The third acts of the same lines, one room each. The boundary is
        // reached with the second act's slot recorded, so the host has to move
        // it on before asking.
        ModelDb.Inject(typeof(HumeInduction));
        CheckBoundary(typeof(DescartesThinkingLens), "JOHN_LOCKE__SELF_AND_OTHER__CORE",
            [typeof(HumeInduction)], doctrine: true, position: 2, actIndex: 2,
            route: "EXPERIENCE_AND_SELF", required: ["WESTERN_EDGE_026"],
            priorEvent: ModelDb.GetId<LockeCargoIdentity>());
        ModelDb.Inject(typeof(SchellingNature));
        CheckBoundary(typeof(DescartesThinkingLens), "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE",
            [typeof(SchellingNature)], doctrine: true, position: 2, actIndex: 2,
            route: "NATURE_AND_BECOMING", required: ["WESTERN_EDGE_023"], checkRoom: true);
        ModelDb.Inject(typeof(KierkegaardFaith));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(KierkegaardFaith)], doctrine: false, position: 2, actIndex: 1,
            route: "INDIVIDUAL_COMMITMENT", checkRoom: true);
        ModelDb.Inject(typeof(HegelRecognition));
        CheckBoundary(typeof(DescartesThinkingLens), "IMMANUEL_KANT__SELF_AND_OTHER__CORE",
            [typeof(HegelRecognition)], doctrine: true, position: 2, actIndex: 2,
            route: "AUTONOMY_AND_HISTORY", checkRoom: true);
        ModelDb.Inject(typeof(RussellAnalysis));
        CheckBoundary(typeof(AristotleCategoryTablet), "GOTTLOB_FREGE__LANGUAGE_AND_MEANING__CORE",
            [typeof(RussellAnalysis)], doctrine: false, position: 2, actIndex: 2,
            route: "FORMAL_LANGUAGE", required: ["WESTERN_EDGE_104"]);
        ModelDb.Inject(typeof(LeviStraussMyth));
        CheckBoundary(typeof(AristotleCategoryTablet), "FERDINAND_DE_SAUSSURE__LANGUAGE_AND_MEANING__CORE",
            [typeof(LeviStraussMyth)], doctrine: false, position: 2, actIndex: 2,
            route: "STRUCTURE_AND_EXCLUSION", required: ["WESTERN_EDGE_106"]);
        ModelDb.Inject(typeof(SartreExistence));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE",
            [typeof(SartreExistence)], doctrine: false, position: 2, actIndex: 2,
            route: "INDIVIDUAL_COMMITMENT", required: ["WESTERN_EDGE_112"]);
        ModelDb.Inject(typeof(HeideggerDasein));
        CheckBoundary(typeof(DescartesThinkingLens), "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            [typeof(HeideggerDasein)], doctrine: true, position: 2, actIndex: 2,
            route: "APPEARANCE_AND_INTERPRETATION", required: ["WESTERN_EDGE_100"]);
        ModelDb.Inject(typeof(MerleauPontyBody));
        CheckBoundary(typeof(DescartesThinkingLens), "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            [typeof(MerleauPontyBody)], doctrine: true, position: 2, actIndex: 2,
            route: "EMBODIED_PERCEPTION", required: ["WESTERN_EDGE_102"]);
        ModelDb.Inject(typeof(DeweyPublicExperiment));
        CheckBoundary(typeof(SocratesQuestionCup), "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(DeweyPublicExperiment)], doctrine: false, position: 3, actIndex: 2,
            route: "FALLIBLE_INQUIRY", required: ["WESTERN_EDGE_108"], checkRoom: true);
        ModelDb.Inject(typeof(FoucaultPower));
        CheckBoundary(typeof(RousseauUncarvedStone), "FRIEDRICH_NIETZSCHE__HISTORY_AND_POWER__CORE",
            [typeof(FoucaultPower)], doctrine: false, position: 2, actIndex: 2,
            route: "GENEALOGY_AND_POWER", required: ["WESTERN_EDGE_120"]);
        ModelDb.Inject(typeof(HorkheimerReason));
        CheckBoundary(typeof(RousseauUncarvedStone), "KARL_MARX__HISTORY_AND_POWER__EARLY",
            [typeof(HorkheimerReason)], doctrine: false, position: 2, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_118"]);
        ModelDb.Inject(typeof(NietzscheRevaluation));
        CheckBoundary(typeof(SocratesBalanceWeight), "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(NietzscheRevaluation)], doctrine: false, position: 3, actIndex: 2,
            route: "WILL_AND_VALUE", required: ["WESTERN_EDGE_150"], checkRoom: true);
        // A saved question page must not take the fixed slot: the boundary only
        // resumes rooms that own this act's fixed edge, and Camus owns none.
        CheckQuestionPageWaitsForItsNode();
        // Rousseau's slot is shared, and the seed's parity decides who takes it.
        // Asserting "either one" let Machiavelli's zero-combat path regress while
        // the pair still passed, so each parity names its own winner.
        ModelDb.Inject(typeof(MachiavelliFloodPreparation));
        ModelDb.Inject(typeof(WollstonecraftAdmission));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(MachiavelliFloodPreparation)], doctrine: false, position: 2, seed: "123456789");
        // An even seed asks Wollstonecraft first; the pair is accepted here because
        // her own room may still decline a slot Machiavelli can take. What the odd
        // seed above pins is the half that used to regress: 046 itself.
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(WollstonecraftAdmission), typeof(MachiavelliFloodPreparation)],
            doctrine: false, position: 2, seed: "123456788");
        // Butler closes the education line's turned branch: after 193 the act
        // three fixed slot belongs to her, not to Beauvoir's default 042.
        ModelDb.Inject(typeof(ButlerAddress));
        CheckBoundary(typeof(RousseauUncarvedStone), "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE",
            [typeof(ButlerAddress)], doctrine: false, position: 2, actIndex: 2,
            route: "EDUCATION_AND_SITUATION", required: ["WESTERN_EDGE_193"],
            priorEvent: ModelDb.GetId<BeauvoirOther>());
        // Rawls closes two lines, so both boundaries must hand her the slot: after
        // Kant's institutions (172 -> 173) and after Mill's freedom (169 -> 170).
        // Montesquieu owns the act-two fixed slot of the checks line. The run has
        // just finished act one's fixed room, so the journey records act one and
        // the room itself moves it to act two when it commits.
        ModelDb.Inject(typeof(MontesquieuBalance));
        CheckBoundary(typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MontesquieuBalance)], doctrine: false, position: 1, actIndex: 1,
            routeRules: true);
        // Smith owns the act-two fixed slot of the sympathy line once the line is
        // named (the act-one question names it), and the boundary must hand it to
        // him without a second question.
        ModelDb.Inject(typeof(SmithSympathy));
        CheckBoundary(typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(SmithSympathy)], doctrine: false, position: 1, actIndex: 1,
            route: "SYMPATHY_AND_PUBLIC_ACTION");
        ModelDb.Inject(typeof(RawlsPosition));
        CheckBoundary(typeof(DescartesThinkingLens), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(RawlsPosition)], doctrine: false, position: 2, actIndex: 2,
            route: "CHECKS_AND_JUSTICE", required: ["WESTERN_EDGE_172"],
            priorEvent: ModelDb.GetId<KantUniversalization>());
        CheckBoundary(typeof(SocratesBalanceWeight), "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(RawlsPosition)], doctrine: false, position: 2, actIndex: 2,
            route: "CONSEQUENCE_AND_FREEDOM", required: ["WESTERN_EDGE_169"],
            priorEvent: ModelDb.GetId<MillHarm>());
        // Arendt also closes two lines: after Mill's sympathy (175 -> 176) and
        // after Foucault's genealogy (181 -> 182).
        ModelDb.Inject(typeof(ArendtBeginning));
        CheckBoundary(typeof(SocratesBalanceWeight), "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(ArendtBeginning)], doctrine: false, position: 2, actIndex: 2,
            route: "SYMPATHY_AND_PUBLIC_ACTION", required: ["WESTERN_EDGE_175"],
            priorEvent: ModelDb.GetId<MillHarm>());
        CheckBoundary(typeof(RousseauUncarvedStone), "MICHEL_FOUCAULT__HISTORY_AND_POWER__CORE",
            [typeof(ArendtBeginning)], doctrine: false, position: 2, actIndex: 2,
            route: "GENEALOGY_AND_POWER", required: ["WESTERN_EDGE_181"],
            priorEvent: ModelDb.GetId<FoucaultPower>());
        // Kant's apperception room owns the autonomy line's act-two fixed slot,
        // which the design names explicitly (act two Kant, act three Hegel).
        ModelDb.Inject(typeof(KantApperception));
        CheckBoundary(typeof(DescartesThinkingLens), "DAVID_HUME__SELF_AND_OTHER__CORE",
            [typeof(KantApperception)], doctrine: false, position: 1, actIndex: 1,
            route: "AUTONOMY_AND_HISTORY");
        // The education line's act-two slot belongs to Wollstonecraft after the
        // act's ancient, not to Rousseau's unrestricted edge to Machiavelli (046).
        // Kept last: an open record would otherwise resume inside later fixtures.
        ModelDb.Inject(typeof(WollstonecraftAdmission));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(WollstonecraftAdmission)], doctrine: false, position: 1, actIndex: 1,
            route: "EDUCATION_AND_SITUATION");
        // The old rule ordered the pair by seed parity. A seed that puts Machiavelli
        // first must still end with her room, because the line names her.
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(WollstonecraftAdmission)], doctrine: false, position: 1, actIndex: 1,
            route: "EDUCATION_AND_SITUATION", seed: "101");
        return Task.CompletedTask;
    }

    private static void CheckQuestionPageWaitsForItsNode()
    {
        ModelDb.Inject(typeof(CamusRevoltLimit));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!
            .Invoke(graph, ["SOCRATES__VIRTUE_AND_HAPPINESS__CORE"])!;
        Put(journey, "CurrentNodeId", "ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE");
        ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_011");
        Put(journey, "LastFixedAct", 3);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", null);
        Put(shared, "WesternJourney", journey);
        Put(shared, "CamusRevoltLimit", Call("CamusRevoltLimitState", "Create", "CAMUS:BOUNDARY:1")!);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        // The page itself is open and resumable on its own node.
        Check(Call("CamusRevoltLimitPatch", "TrySelectExistingNative", run) is not null,
            "The saved question page is resumable on its own node.");
        // At the act boundary the same page must not be resumed: it owns no fixed edge.
        Put(journey, "LastFixedAct", 2);
        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        Check(!selected,
            "A saved question page must not take the act boundary's fixed slot, got "
            + ((EventRoom?)prepare[1])?.CanonicalEvent.GetType().Name + ".");
    }

    // A run the harness built in memory is marked isolated by the state service,
    // and TryGet then refuses to hand any record back; the fixture lifts that mark
    // so the room reads the state it was given.
    private static void Unisolate(RunState run)
    {
        object table = T("PhilosophyRunStateRitsuStore").GetField("IsolatedRuns", Methods)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table, [run]);
    }

    private static void CheckBoundary(Type relic, string node, Type[] expectedRooms, bool doctrine, int position, int actIndex = 1, string[]? required = null, string? route = null, ModelId? priorEvent = null, string? seed = null,
        bool routeRules = false, bool checkRoom = false)
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [relic, actIndex])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        // The shared Rousseau slot is handed out by the seed's parity, so the
        // caller names the seed whose winner it is asserting.
        if (seed is not null) Set(run, "Rng", new RunRngSet(seed));
        if (priorEvent is not null)
        {
            // A third-act room may require a fight after an earlier act's event.
            var history = new List<List<MapPointHistoryEntry>>();
            for (int act = 0; act <= actIndex; act++)
            {
                var points = new List<MapPointHistoryEntry>();
                if (act == actIndex - 1)
                {
                    points.Add(new() { MapPointType = MapPointType.Unknown,
                        Rooms = [new() { RoomType = RoomType.Event, ModelId = priorEvent }] });
                    points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
                }
                else if (act == actIndex)
                    points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
                history.Add(points);
            }
            Set(run, "_mapPointHistory", history);
        }

        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey;
        try { journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [node])!; }
        catch (TargetInvocationException)
        {
            journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["SOCRATES__VIRTUE_AND_HAPPINESS__CORE"])!;
            Put(journey, "CurrentNodeId", node);
        }
        if (route is not null) Put(journey, "RouteId", route);
        // A default-line run carries the rules but no route name yet: the boundary
        // itself has to name the line its entry starts on.
        if (routeRules) Put(journey, "RouteRules", true);
        var completed = (HashSet<string>)Get(journey, "CompletedEdgeIds");
        foreach (string edge in required ?? []) completed.Add(edge);
        Put(journey, "LastFixedAct", position);
        object? doctrineState = doctrine
            ? Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!
            : null;
        if (doctrineState is not null)
        {
            Put(doctrineState, "ThinkerId", "DESCARTES");
            Put(doctrineState, "DoctrineId", "DESCARTES_THINKING_LENS");
        }
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", doctrineState);
        Put(shared, "WesternJourney", journey);
        // Restore refuses runs that were isolated, and then the room would read a
        // state the fixture never wrote; install the record directly instead.
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);

        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        var edges = (System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
            .Invoke(graph, [journey, "Fixed"])!;
        string open = string.Join(",", edges.Cast<object>().Select(edge => Get(edge, "EdgeId") + "@" + Get(edge, "Act")));
        Check(selected, string.Join("/", expectedRooms.Select(room => room.Name))
            + ": the second act's fixed room must be selectable at the ancient boundary without a question point. Open: " + open);
        if (routeRules)
            Check(((System.Collections.IEnumerable)Get(journey, "ResolvedQuestionActs")).Cast<object>().Count() == 0,
                node + ": the default fixed boundary must not consume a question.");
        var room = (EventRoom)prepare[1]!;
        Type? expected = expectedRooms.FirstOrDefault(candidate => candidate == room.CanonicalEvent.GetType());
        Check(expected is not null,
            "The boundary selects a room that owns this act's fixed edge, got " + room.CanonicalEvent.GetType().Name + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object current = Get(query[1]!, expected.Name);
        Check(current is not null && (bool)current.GetType().GetMethod("IsUsable")!.Invoke(current, null)!,
            "The boundary selection persists a usable room state for save recovery.");
        // The event body restores itself by matching its record's act, so a record
        // that names the wrong act is a room that opens locked. This is the fact
        // the body reads; assert it here where the boundary wrote it.
        Type openedType = room.CanonicalEvent.GetType();
        if (openedType == typeof(LockeCargoIdentity) || openedType == typeof(HumeInduction)
            || openedType == typeof(SchellingNature) || openedType == typeof(HegelRecognition))
        {
            int recordedAct = (int)Get(current!, "ActIndex");
            string instanceId = (string)Get(current!, "InstanceId");
            Check(recordedAct == run.CurrentActIndex + 1 && instanceId.EndsWith(":" + recordedAct, StringComparison.Ordinal),
                openedType.Name + ": the saved record must name the act it was opened in (act " + (run.CurrentActIndex + 1)
                + "), got act " + recordedAct + " id " + instanceId + ".");
        }
        if (checkRoom)
        {
            object priorRooms = GetField(run, "_currentRooms");
            typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
                [run, run.Players[0], room.CanonicalEvent, query[1]!, run.CurrentActIndex]);
            Set(run, "_currentRooms", priorRooms);
            object?[] after = [run, null];
            Call("PhilosophyRunStateService", "TryGet", after);
            journey = Get(after[1]!, "WesternJourney");
        }

        Put(journey, "LastFixedAct", actIndex + 2);
        object spent = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(spent, "CurrentDoctrine", doctrineState);
        Put(spent, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, spent);
        object?[] again = [run, null];
        Check(!(bool)Call("ActFixedEntryHost", "TryPrepare", again)!,
            "A spent fixed position must not present a second room at the same boundary.");
    }
}
