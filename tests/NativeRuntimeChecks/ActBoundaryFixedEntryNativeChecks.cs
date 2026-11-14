using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Events;
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

    public static async Task Run()
    {
        ModelDb.Inject(typeof(SpinozaCauseNetwork));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 2);
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 1);
        CheckBoundary(typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 2,
            route: "NATURE_AND_BECOMING", checkRoom: true, thinker: "HERACLITUS", doctrineId: "HERACLITUS_LYRE");
        ModelDb.Inject(typeof(EpicurusSharedMargin));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 3, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 2, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        ModelDb.Inject(typeof(MontesquieuBalance));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MontesquieuBalance)], doctrine: false, position: 2, route: "CHECKS_AND_JUSTICE", checkRoom: true);
        ModelDb.Inject(typeof(SmithSympathy));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(SmithSympathy)], doctrine: false, position: 2, route: "SYMPATHY_AND_PUBLIC_ACTION", checkRoom: true);
        ModelDb.Inject(typeof(HusserlEpoche));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(HusserlEpoche)], doctrine: true, position: 2, route: "APPEARANCE_AND_INTERPRETATION", checkRoom: true);
        ModelDb.Inject(typeof(FregeSense));
        CheckBoundary(typeof(SocratesBalanceWeight), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(FregeSense)], doctrine: false, position: 2, route: "FORMAL_LANGUAGE");
        CheckBoundary(typeof(SocratesBalanceWeight), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(FregeSense)], doctrine: false, position: 1, actIndex: 1,
            route: "FORMAL_LANGUAGE", checkRoom: true);
        ModelDb.Inject(typeof(LockeCargoIdentity));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(LockeCargoIdentity)], doctrine: true, position: 2, route: "EXPERIENCE_AND_SELF");
        // The rooms the routes place later in the run: the boundary has to hand
        // over the act they were placed in, not the act they were written for.
        ModelDb.Inject(typeof(FreudUnconscious));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(FreudUnconscious)], doctrine: true, position: 2, route: "UNCONSCIOUS_AND_NORM", checkRoom: true);
        ModelDb.Inject(typeof(LacanSignifier));
        CheckBoundary(typeof(DescartesThinkingLens), "SIGMUND_FREUD__SELF_AND_OTHER__CORE",
            [typeof(LacanSignifier)], doctrine: true, position: 3, actIndex: 2,
            route: "UNCONSCIOUS_AND_NORM", required: ["WESTERN_EDGE_087"], checkRoom: true);
        ModelDb.Inject(typeof(KantUniversalization));
        CheckBoundary(typeof(DescartesThinkingLens), "MONTESQUIEU__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(KantUniversalization)], doctrine: true, position: 2, actIndex: 2,
            route: "CHECKS_AND_JUSTICE", required: ["WESTERN_EDGE_114"]);
        // The second act of the autonomy line. Locke's room sits earlier in the
        // dispatch chain and opens on the same node, so this also proves the
        // route gate keeps a room of another line from taking the slot.
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(KantApperception)], doctrine: false, position: 2, actIndex: 1,
            route: "AUTONOMY_AND_HISTORY", checkRoom: true);
        // The rest of the second acts, one room each: the boundary has to hand
        // the slot to the figure the line names, not to whatever opens first.
        ModelDb.Inject(typeof(SaussureSign));
        CheckBoundary(typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(SaussureSign)], doctrine: false, position: 2, route: "STRUCTURE_AND_EXCLUSION", checkRoom: true);
        ModelDb.Inject(typeof(BenthamUtility));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(BenthamUtility)], doctrine: false, position: 2, route: "CONSEQUENCE_AND_FREEDOM", checkRoom: true);
        ModelDb.Inject(typeof(MillHarm));
        CheckBoundary(typeof(SocratesBalanceWeight), "BENTHAM__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 3, actIndex: 2,
            route: "CONSEQUENCE_AND_FREEDOM", checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object mill = Get(shared, "MillHarm");
                Check((string)Get(mill, "EdgeId") == "WESTERN_EDGE_131" && (bool)Get(mill, "FixedMode"),
                    "Mill's fixed room must take 131 from Bentham, not the question edge 041.");
            });
        ModelDb.Inject(typeof(PeirceInquiry));
        CheckBoundary(typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(PeirceInquiry)], doctrine: false, position: 2, route: "FALLIBLE_INQUIRY", checkRoom: true);
        ModelDb.Inject(typeof(GadamerFusion));
        CheckBoundary(typeof(DescartesThinkingLens), "MARTIN_HEIDEGGER__SELF_AND_OTHER__CORE",
            [typeof(GadamerFusion)], doctrine: false, position: 2, actIndex: 2,
            route: "APPEARANCE_AND_INTERPRETATION", required: ["WESTERN_EDGE_164"], checkRoom: true);
        ModelDb.Inject(typeof(DeleuzeDifference));
        CheckBoundary(typeof(DescartesThinkingLens), "FRIEDRICH_WILHELM_JOSEPH_SCHELLING__BEING_AND_CHANGE__CORE",
            [typeof(DeleuzeDifference)], doctrine: false, position: 2, actIndex: 2,
            route: "NATURE_AND_BECOMING", required: ["WESTERN_EDGE_162"], checkRoom: true);
        ModelDb.Inject(typeof(BenthamUtility));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(BenthamUtility)], doctrine: false, position: 1, actIndex: 1,
            route: "CONSEQUENCE_AND_FREEDOM", checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object bentham = Get(shared, "BenthamUtility");
                Check((int)Get(bentham, "ActIndex") == 2
                    && (string)Get(bentham, "EdgeId") == "WESTERN_EDGE_130",
                    "The consequence line's default second-act Bentham room uses 130.");
            });
        ModelDb.Inject(typeof(MillHarm));
        CheckBoundary(typeof(SocratesBalanceWeight), "BENTHAM__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "CONSEQUENCE_AND_FREEDOM", required: ["WESTERN_EDGE_130"], checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object mill = Get(shared, "MillHarm");
                Check((int)Get(mill, "ActIndex") == 3
                    && (string)Get(mill, "EdgeId") == "WESTERN_EDGE_131",
                    "The consequence line's third-act Mill room uses 131, not the early 066.");
            });
        CheckBoundary(typeof(DescartesThinkingLens), "BENTHAM__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "CONSEQUENCE_AND_FREEDOM", checkRoom: true);
        CheckBoundary(typeof(DescartesThinkingLens), "ADAM_SMITH__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "SYMPATHY_AND_PUBLIC_ACTION", checkRoom: true);
        ModelDb.Inject(typeof(BeauvoirOther));
        CheckBoundary(typeof(DescartesThinkingLens), "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(BeauvoirOther)], doctrine: false, position: 2, actIndex: 2,
            route: "EDUCATION_AND_SITUATION", checkRoom: true);
        CheckBoundary(typeof(DescartesThinkingLens), "JEAN_PAUL_SARTRE__SELF_AND_OTHER__CORE",
            [typeof(BeauvoirOther)], doctrine: false, position: 2, actIndex: 2,
            route: "INDIVIDUAL_COMMITMENT", required: ["WESTERN_EDGE_163"], checkRoom: true,
            afterSelect: (run, shared, ev) =>
            {
                var beauvoir = (BeauvoirOther)ev.ToMutable();
                typeof(EventModel).GetProperty("Owner")!.SetValue(beauvoir, run.Players[0]);
                typeof(BeauvoirOther).GetMethod("SetInitialEventState",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(beauvoir, [false]);
                string key = beauvoir.Description.LocEntryKey;
                Check(key.EndsWith("BEAUVOIR_OTHER.pages.INVITATION_SARTRE.description", StringComparison.Ordinal),
                    "The Sartre source must not tell the player she is keeping Wollstonecraft: " + key);
            });
        CheckBoundary(typeof(DescartesThinkingLens), "MAURICE_MERLEAU_PONTY__SELF_AND_OTHER__CORE",
            [typeof(BeauvoirOther)], doctrine: false, position: 2, actIndex: 2,
            route: "EMBODIED_PERCEPTION", required: ["WESTERN_EDGE_195"], checkRoom: true);
        ModelDb.Inject(typeof(HabermasCommunication));
        CheckBoundary(typeof(DescartesThinkingLens), "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE",
            [typeof(HabermasCommunication)], doctrine: false, position: 2, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_177", "WESTERN_EDGE_084"], checkRoom: true);
        ModelDb.Inject(typeof(WittgensteinGame));
        CheckBoundary(typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(WittgensteinGame)], doctrine: false, position: 1, actIndex: 1,
            route: "ORDINARY_SPEECH", checkRoom: true);
        CheckBoundary(typeof(AristotleCategoryTablet), "LUDWIG_WITTGENSTEIN__LANGUAGE_AND_MEANING__CORE",
            [typeof(WittgensteinGame)], doctrine: false, position: 2, actIndex: 2,
            route: "ORDINARY_SPEECH", required: ["WESTERN_EDGE_152"], checkRoom: true);
        CheckBoundary(typeof(AristotleCategoryTablet), "BERTRAND_RUSSELL__LANGUAGE_AND_MEANING__CORE",
            [typeof(WittgensteinGame)], doctrine: false, position: 2, actIndex: 2,
            route: "FORMAL_LANGUAGE", required: ["WESTERN_EDGE_184"], checkRoom: true);
        ModelDb.Inject(typeof(SchopenhauerWillDenial));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE",
            [typeof(SchopenhauerWillDenial)], doctrine: false, position: 1, actIndex: 1,
            route: "WILL_AND_VALUE", checkRoom: true, afterSelect: CheckSchopenhauerFixedCommit);
        ModelDb.Inject(typeof(NietzscheRevaluation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(NietzscheRevaluation)], doctrine: false, position: 2, route: "GENEALOGY_AND_POWER", checkRoom: true);
        ModelDb.Inject(typeof(ComtePositivism));
        // Default social-knowledge line: Bacon in the second act, Comte in the third.
        CheckBoundary(typeof(RousseauUncarvedStone), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(ComtePositivism)], doctrine: false, position: 3, actIndex: 2,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_140"], checkRoom: true);
        // Early first-act Bacon turn: Bacon's own room opens in the second act.
        CheckBoundary(typeof(RousseauUncarvedStone), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(BaconSampleLedger)], doctrine: false, position: 2, actIndex: 1,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_197"], checkRoom: true);
        ModelDb.Inject(typeof(NewtonTwoSites));
        CheckBoundary(typeof(SocratesQuestionCup), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(NewtonTwoSites)], doctrine: false, position: 1, actIndex: 1,
            route: "SCIENTIFIC_MODEL", required: ["WESTERN_EDGE_165"], checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object newton = Get(shared, "NewtonTwoSites");
                Check((bool)Get(newton, "EarlyBacon") && (int)Get(newton, "ActIndex") == 2
                    && ((string)Get(newton, "InstanceId")).EndsWith(":2", StringComparison.Ordinal),
                    "The early Bacon turn creates the act-two Newton record with 166, not the old 049 path.");
                object journey = Get(shared, "WesternJourney");
                object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
                foreach (string name in new[] { "Accept", "MeasureDrop", "RecordError", "RevealOther", "ConditionRelation", "Leave" })
                {
                    object action = Enum.Parse(T("NewtonTwoSitesAction"), name);
                    Check((bool)Call("NewtonTwoSitesFlow", "TryPrepare", newton, action)!
                        && (bool)Call("NewtonTwoSitesFlow", "TryCommitPending", newton, journey, null!, null!, graph)!,
                        "The early Newton event commits " + name + ".");
                }
                Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_166")
                    && (string)Get(journey, "CurrentNodeId") == "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE"
                    && (int)Get(journey, "LastFixedAct") == 3,
                    "The early Newton event commits the act-two 166 edge and lands on Newton.");
            });
        ModelDb.Inject(typeof(MarxAlienation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(MarxAlienation)], doctrine: false, position: 2, route: "SOCIAL_CRITIQUE", checkRoom: true);
        CheckWittgensteinTwoActs();
        CheckJamesIndependentQuestion();
        CheckBerkeleyIndependentQuestion();
        CheckEarlySaussureContinuation();
        CheckEarlySmithContinuation();
        CheckEarlyPeirceContinuation();
        CheckEarlyBenthamContinuation();
        await CheckMontesquieuKantRawls();
        CheckEarlyKierkegaardScene();
        CheckEarlySartreContinuation();
        CheckEarlyMerleauContinuation();
        await CheckFirstActTurnScenes();
        await CheckHegelEntryPersistence();
        CheckMarxAfterEarlyHegel();
        CheckEarlyDeweyContinuation();
        CheckEarlyNietzscheContinuation();
        CheckEarlyHabermasContinuation();
        CheckBenjaminIndependentQuestion();
        CheckLegacyNestedOptionsAreHidden();
        // The third acts of the same lines, one room each. The boundary is
        // reached with the second act's slot recorded, so the host has to move
        // it on before asking.
        ModelDb.Inject(typeof(FichteSelfPositing));
        CheckBoundary(typeof(DescartesThinkingLens), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(FichteSelfPositing)], doctrine: false, position: 3, actIndex: 2,
            route: "AUTONOMY_AND_HISTORY", required: ["WESTERN_EDGE_115"], checkRoom: true);
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
            [typeof(KierkegaardFaith)], doctrine: false, position: 1, actIndex: 1,
            route: "INDIVIDUAL_COMMITMENT", checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object kierkegaard = Get(shared, "KierkegaardFaith");
                Check((int)Get(kierkegaard, "ActIndex") == 2
                    && (string)Get(kierkegaard, "EdgeId") == "WESTERN_EDGE_112",
                    "The individual line's default second-act Kierkegaard room uses 112.");
            });
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
            route: "FORMAL_LANGUAGE", required: ["WESTERN_EDGE_104"], checkRoom: true);
        ModelDb.Inject(typeof(LeviStraussMyth));
        CheckBoundary(typeof(AristotleCategoryTablet), "FERDINAND_DE_SAUSSURE__LANGUAGE_AND_MEANING__CORE",
            [typeof(LeviStraussMyth)], doctrine: false, position: 2, actIndex: 2,
            route: "STRUCTURE_AND_EXCLUSION", required: ["WESTERN_EDGE_106"], checkRoom: true);
        ModelDb.Inject(typeof(DerridaDeconstruction));
        CheckBoundary(typeof(SocratesBalanceWeight), "CLAUDE_LEVI_STRAUSS__LANGUAGE_AND_MEANING__CORE",
            [typeof(DerridaDeconstruction)], doctrine: false, position: 3, actIndex: 2,
            route: "STRUCTURE_AND_EXCLUSION", required: ["WESTERN_EDGE_187"], checkRoom: true);
        ModelDb.Inject(typeof(SartreExistence));
        CheckBoundary(typeof(SocratesBalanceWeight), "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE",
            [typeof(SartreExistence)], doctrine: false, position: 3, actIndex: 2,
            route: "INDIVIDUAL_COMMITMENT", required: ["WESTERN_EDGE_112"], checkRoom: true);
        CheckBoundary(typeof(SocratesBalanceWeight), "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE",
            [typeof(SartreExistence)], doctrine: false, position: 2, actIndex: 1,
            route: "INDIVIDUAL_COMMITMENT", required: ["WESTERN_EDGE_158"], checkRoom: true);
        ModelDb.Inject(typeof(HeideggerDasein));
        CheckBoundary(typeof(DescartesThinkingLens), "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            [typeof(HeideggerDasein)], doctrine: true, position: 3, actIndex: 2,
            route: "APPEARANCE_AND_INTERPRETATION", required: ["WESTERN_EDGE_100"], checkRoom: true);
        CheckBoundary(typeof(DescartesThinkingLens), "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            [typeof(HeideggerDasein)], doctrine: true, position: 2, actIndex: 1,
            route: "APPEARANCE_AND_INTERPRETATION", required: ["WESTERN_EDGE_160"], checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object heidegger = Get(shared, "HeideggerDasein");
                Check((string)Get(heidegger, "EdgeId") == "WESTERN_EDGE_164",
                    "The appearance route must take 164, not the legacy 073.");
            });
        ModelDb.Inject(typeof(MerleauPontyBody));
        CheckBoundary(typeof(DescartesThinkingLens), "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            [typeof(MerleauPontyBody)], doctrine: true, position: 2, actIndex: 2,
            route: "EMBODIED_PERCEPTION", required: ["WESTERN_EDGE_102"], checkRoom: true);
        ModelDb.Inject(typeof(DeweyPublicExperiment));
        CheckBoundary(typeof(SocratesQuestionCup), "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(DeweyPublicExperiment)], doctrine: false, position: 3, actIndex: 2,
            route: "FALLIBLE_INQUIRY", required: ["WESTERN_EDGE_108"], checkRoom: true);
        // Kuhn closes the two turned knowledge lines. Newton's route must have
        // 166 before its third-act fixed handover, and Comte's route 198.
        ModelDb.Inject(typeof(KuhnParadigm));
        CheckBoundary(typeof(SocratesQuestionCup), "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(KuhnParadigm)], doctrine: false, position: 3, actIndex: 2,
            route: "SCIENTIFIC_MODEL", required: ["WESTERN_EDGE_166"], checkRoom: true);
        CheckBoundary(typeof(RousseauUncarvedStone), "COMTE__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(KuhnParadigm)], doctrine: false, position: 3, actIndex: 2,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_198"], checkRoom: true);
        ModelDb.Inject(typeof(NietzscheRevaluation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(NietzscheRevaluation)], doctrine: false, position: 1, actIndex: 1,
            route: "GENEALOGY_AND_POWER", checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object nietzsche = Get(shared, "NietzscheRevaluation");
                Check((int)Get(nietzsche, "ActIndex") == 2
                    && (string)Get(nietzsche, "EdgeId") == "WESTERN_EDGE_120"
                    && (string)Get(nietzsche, "RouteSourceNodeId") == "ROUSSEAU__HISTORY_AND_POWER__CORE",
                    "The genealogy line's own second-act Nietzsche room uses 120 from Rousseau.");
            });
        ModelDb.Inject(typeof(FoucaultPower));
        CheckBoundary(typeof(RousseauUncarvedStone), "FRIEDRICH_NIETZSCHE__HISTORY_AND_POWER__CORE",
            [typeof(FoucaultPower)], doctrine: false, position: 2, actIndex: 2,
            route: "GENEALOGY_AND_POWER", required: ["WESTERN_EDGE_120"], checkRoom: true);
        ModelDb.Inject(typeof(HorkheimerReason));
        CheckBoundary(typeof(RousseauUncarvedStone), "KARL_MARX__HISTORY_AND_POWER__EARLY",
            [typeof(HorkheimerReason)], doctrine: false, position: 3, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_118"], checkRoom: true);
        CheckBoundary(typeof(RousseauUncarvedStone), "KARL_MARX__HISTORY_AND_POWER__EARLY",
            [typeof(HorkheimerReason)], doctrine: false, position: 2, actIndex: 1,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_083"], checkRoom: true);
        CheckBoundary(typeof(RousseauUncarvedStone), "KARL_MARX__HISTORY_AND_POWER__EARLY",
            [typeof(HorkheimerReason)], doctrine: false, position: 1, actIndex: 1,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_177"], checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object horkheimer = Get(shared, "HorkheimerReason");
                Check((string)Get(horkheimer, "EdgeId") == "WESTERN_EDGE_084",
                    "The player's first-act Marx turn must take 084, not the seed branch's 178.");
            });
        ModelDb.Inject(typeof(AdornoNegative));
        CheckBoundary(typeof(RousseauUncarvedStone), "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE",
            [typeof(AdornoNegative)], doctrine: false, position: 2, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_083", "WESTERN_EDGE_178"], checkRoom: true);
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
        // The fortune-and-virtue line is the reachable Machiavelli entry. Its
        // own act-two fixed edge is 046; the education line still keeps 040.
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(MachiavelliFloodPreparation)], doctrine: false, position: 1, actIndex: 1,
            route: "FORTUNE_AND_VIRTUE", routeRules: true, checkRoom: true);
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
        // The same third-act room also closes the unconscious line after Lacan.
        ModelDb.Inject(typeof(LacanSignifier));
        CheckBoundary(typeof(DescartesThinkingLens), "JACQUES_LACAN__SELF_AND_OTHER__CORE",
            [typeof(ButlerAddress)], doctrine: true, position: 2, actIndex: 2,
            route: "UNCONSCIOUS_AND_NORM", required: ["WESTERN_EDGE_190"],
            priorEvent: ModelDb.GetId<LacanSignifier>());
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
            route: "SYMPATHY_AND_PUBLIC_ACTION", checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object smith = Get(shared, "SmithSympathy");
                Check((int)Get(smith, "ActIndex") == 2
                    && (string)Get(smith, "EdgeId") == "WESTERN_EDGE_116",
                    "The sympathy line's default second-act Smith room uses 116.");
            });
        ModelDb.Inject(typeof(MillHarm));
        CheckBoundary(typeof(PlatoCivicSeal), "ADAM_SMITH__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "SYMPATHY_AND_PUBLIC_ACTION", required: ["WESTERN_EDGE_116"], checkRoom: true,
            afterSelect: (_, shared, _) =>
            {
                object mill = Get(shared, "MillHarm");
                Check((int)Get(mill, "ActIndex") == 3
                    && (string)Get(mill, "EdgeId") == "WESTERN_EDGE_117",
                    "The sympathy line's third-act Mill room uses 117, not the early 055.");
            });
        ModelDb.Inject(typeof(RawlsPosition));
        CheckBoundary(typeof(DescartesThinkingLens), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(RawlsPosition)], doctrine: false, position: 2, actIndex: 2,
            route: "CHECKS_AND_JUSTICE", required: ["WESTERN_EDGE_172"],
            priorEvent: ModelDb.GetId<KantUniversalization>());
        CheckBoundary(typeof(SocratesBalanceWeight), "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "CONSEQUENCE_AND_FREEDOM", required: ["WESTERN_EDGE_169"],
            priorEvent: ModelDb.GetId<MillHarm>(), afterSelect: (_, shared, _) =>
            {
                object mill = Get(shared, "MillHarm");
                Check((string)Get(mill, "EdgeId") == "WESTERN_EDGE_170"
                    && (string)Get(mill, "TargetNodeId") == "RAWLS__FREEDOM_AND_INSTITUTIONS__CORE",
                    "The early Mill room takes 170 and targets Rawls.");
            });
        // Arendt also closes two lines: after Mill's sympathy (175 -> 176) and
        // after Foucault's genealogy (181 -> 182).
        CheckBoundary(typeof(SocratesBalanceWeight), "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MillHarm)], doctrine: false, position: 2, actIndex: 2,
            route: "SYMPATHY_AND_PUBLIC_ACTION", required: ["WESTERN_EDGE_175"],
            priorEvent: ModelDb.GetId<MillHarm>(), afterSelect: (_, shared, _) =>
            {
                object mill = Get(shared, "MillHarm");
                Check((string)Get(mill, "EdgeId") == "WESTERN_EDGE_176"
                    && (string)Get(mill, "TargetNodeId") == "HANNAH_ARENDT__FREEDOM_AND_INSTITUTIONS__CORE",
                    "The sympathy Mill room takes 176 and targets Arendt.");
            });
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
        return;
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

    private static EventModel ChooseVirtueDirection(RunState run, NativeRuntimeFixture fixture,
        string node, string key) => ChooseDirection(run, fixture, node, key);

    private static EventModel ChooseDirection(RunState run, NativeRuntimeFixture fixture,
        string node, string key)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [node])!;
        Put(journey, "RouteRules", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;
        EventModel selected = run.Act.PullNextEvent(run);
        Check(selected is RouteTurnQuestion,
            "The real virtue entry must ask the route fork first, got " + selected.GetType().Name + ".");
        var room = new EventRoom(selected);
        EventModel local = room.CanonicalEvent.ToMutable();
        NativeRuntimeFixture.Set(local, "Owner", fixture.Player);
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        object? originalActs = Get(run, "Acts");
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        object choices = T("RouteTurnQuestionOptions").GetMethod("For", Methods)!.Invoke(null, [journey, graph])!;
        object direction = ((System.Collections.IEnumerable)choices).Cast<object>()
            .Single(choice => (string)choice.GetType().GetProperty("Key")!.GetValue(choice)! == key);
        Task task = (Task)typeof(RouteTurnQuestion).GetMethod("Choose", Methods)!
            .Invoke(local, [journey, direction])!;
        task.GetAwaiter().GetResult();
        Check(saves.SaveAttempts > 0,
            "Answering the real route page must persist through the native save store.");
        Set(run, "Acts", originalActs);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        return selected;
    }

    private static void ChooseDirectionDirect(RunState run, NativeRuntimeFixture fixture,
        string node, string key)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [node])!;
        Put(journey, "RouteRules", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "PLATO");
        Put(doctrine, "DoctrineId", "PLATO_WHOLE_ARRANGEMENT");
        Put(shared, "CurrentDoctrine", doctrine);
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;
        EventModel selected = ModelDb.Event<RouteTurnQuestion>();
        NativeRuntimeFixture.Set(selected, "Owner", null);
        var room = new EventRoom(selected);
        EventModel local = room.CanonicalEvent.ToMutable();
        NativeRuntimeFixture.Set(local, "Owner", fixture.Player);
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        object? originalActs = Get(run, "Acts");
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        object choices = T("RouteTurnQuestionOptions").GetMethod("For", Methods)!.Invoke(null, [journey, graph])!;
        object direction = ((System.Collections.IEnumerable)choices).Cast<object>()
            .Single(choice => (string)choice.GetType().GetProperty("Key")!.GetValue(choice)! == key);
        Task task = (Task)typeof(RouteTurnQuestion).GetMethod("Choose", Methods)!
            .Invoke(local, [journey, direction])!;
        task.GetAwaiter().GetResult();
        Check(saves.SaveAttempts > 0,
            "The real route page must persist its answer through the native save store.");
        Set(run, "Acts", originalActs);
        Set(run, "_currentRooms", new List<AbstractRoom>());
    }

    private static void CheckEarlyMerleauContinuation()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(MerleauPontyBody));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "DESCARTES__SELF_AND_OTHER__CORE", "TO_HUSSERL_EMBODIED");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_160")
            && (string)Get(journey, "RouteId") == "EMBODIED_PERCEPTION",
            "The real embodied direction takes 160 and names the embodied line.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: MerleauPontyBody },
            "The real embodied direction opens Merleau at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "MerleauPontyBody");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_195",
            "The early Merleau room uses 195 at act two.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "MerleauPontyBody");
        journey = Get(shared, "WesternJourney");
        object action = Enum.Parse(T("MerleauPontyBodyAction"), "BodyAsSubject");
        Check((bool)Call("MerleauPontyBodyFlow", "TryPrepare", state, action)!
            && (bool)Call("MerleauPontyBodyFlow", "TryCommitPending", state, journey,
                Call("WesternRouteGraph", "LoadEmbedded")!),
            "The early Merleau room commits its body standard.");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_195")
            && (string)Get(journey, "CurrentNodeId") == "MAURICE_MERLEAU_PONTY__SELF_AND_OTHER__CORE",
            "The early Merleau choice reaches Merleau on 195.");
    }

    private static void CheckEarlyDeweyContinuation()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(PeirceInquiry));
        ModelDb.Inject(typeof(DeweyPublicExperiment));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesQuestionCup), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE", "TO_PEIRCE");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_069")
            && (string)Get(journey, "RouteId") == "FALLIBLE_INQUIRY",
            "The real Peirce direction takes 069 and names the fallible line.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: PeirceInquiry },
            "The real Peirce direction opens Peirce at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "PeirceInquiry");
        journey = Get(shared, "WesternJourney");
        Check((string)Get(state, "EdgeId") == "WESTERN_EDGE_070",
            "The early Peirce room uses 070.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "PeirceInquiry");
        journey = Get(shared, "WesternJourney");
        object action = Enum.Parse(T("PeirceInquiryAction"), "TestByConsequences");
        Check((bool)Call("PeirceInquiryFlow", "TryPrepare", state, action)!
            && (bool)Call("PeirceInquiryFlow", "TryCommitPending", state, journey,
                Call("WesternRouteGraph", "LoadEmbedded")!),
            "The early Peirce room commits its inquiry standard.");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_070")
            && (string)Get(journey, "CurrentNodeId") == "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE",
            "The early Peirce choice reaches Peirce on 070.");
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: DeweyPublicExperiment },
            "The real Peirce path opens Dewey at the third-act boundary, got "
            + (prepare[1] is EventRoom deweyRoom ? deweyRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "DeweyPublicExperiment");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 3 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_109",
            "The early Dewey room uses 109 at act three.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 2]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "DeweyPublicExperiment");
        journey = Get(shared, "WesternJourney");
        action = Enum.Parse(T("DeweyPublicExperimentAction"), "PublicExperiment");
        Check((bool)Call("DeweyPublicExperimentFlow", "TryPrepare", state, action)!
            && (bool)Call("DeweyPublicExperimentFlow", "TryCommitPending", state, journey,
                Call("WesternRouteGraph", "LoadEmbedded")!),
            "The early Dewey room commits its public-experiment standard.");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_109")
            && (string)Get(journey, "CurrentNodeId") == "JOHN_DEWEY__KNOWLEDGE_AND_DOUBT__CORE",
            "The early Dewey choice reaches Dewey on 109.");
    }

    private static void CheckEarlyBenthamContinuation()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(MillHarm));
        ModelDb.Inject(typeof(RawlsPosition));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", "TO_BENTHAM");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        object shared = live[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_168")
            && (string)Get(journey, "RouteId") == "CONSEQUENCE_AND_FREEDOM",
            "The real Bentham direction takes 168 and names the consequence line.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: MillHarm },
            "The early Bentham turn opens Mill at the real second-act boundary, got "
            + (prepare[1] is EventRoom er ? er.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "MillHarm");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_066"
            && (string)Get(state, "RoomSourceNodeId") == "BENTHAM__FREEDOM_AND_INSTITUTIONS__CORE",
            "The early Bentham continuation uses Mill's actual act-two edge 066.");
        EventRoom millRoom = (EventRoom)prepare[1]!;
        var millLocal = (MillHarm)millRoom.CanonicalEvent.ToMutable();
        typeof(EventModel).GetProperty("Owner")!.SetValue(millLocal, fixture.Player);
        typeof(MillHarm).GetMethod("SetInitialEventState",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(millLocal, [false]);
        typeof(MillHarm).GetMethod("ShowConfirmedState",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(millLocal, [state]);
        Check(millLocal.Description.LocEntryKey.EndsWith(
                "MILL_HARM.pages.EARLY_BENTHAM.description", StringComparison.Ordinal),
            "Mill's early page must name the act-two handover, got " + millLocal.Description.LocEntryKey
            + " and state " + (Get(shared, "MillHarm") is null ? "missing" : "present") + ".");
        Check(millLocal.CurrentOptions.Any(option => option.HistoryName.LocEntryKey.EndsWith(
                ".options.KEEP_BENTHAM_EARLY.title", StringComparison.Ordinal)),
            "Mill's early keep button must name the act-two handover, got "
            + string.Join(",", millLocal.CurrentOptions.Select(option => option.HistoryName.LocEntryKey)) + ".");
        foreach (string name in new[] { "ApplyHarmTest", "ProtectConduct", "Leave" })
        {
            object action = Enum.Parse(T("MillHarmAction"), name);
            Check((bool)Call("MillHarmFlow", "TryPrepare", state, action)!
                && (bool)Call("MillHarmFlow", "TryCommitPending", state, journey, graph)!,
                "The early Bentham continuation commits Mill's " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_066"),
            "Mill's early room completes 066 before the third act.");
        var history = new List<List<MapPointHistoryEntry>>();
        for (int act = 0; act <= 2; act++)
        {
            var points = new List<MapPointHistoryEntry>();
            if (act == 1)
            {
                points.Add(new() { MapPointType = MapPointType.Unknown,
                    Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<MillHarm>() }] });
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            }
            else if (act == 2)
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            history.Add(points);
        }
        Set(run, "_mapPointHistory", history);
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: RawlsPosition },
            "Mill's early handover opens Rawls on 170 in the third act, got "
            + (prepare[1] is EventRoom rawlsRoom ? rawlsRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object rawls = Get(shared, "RawlsPosition");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(rawls, "ActIndex") == 3
            && (string)Get(rawls, "RequiredEdgeId") == "WESTERN_EDGE_066",
            "Rawls's closing record follows Mill's early 066 handover.");
        foreach (string name in new[] { "DraftMinimum", "Execute", "Leave" })
        {
            object value = Enum.Parse(T("RawlsPositionAction"), name);
            Check((bool)Call("RawlsPositionFlow", "TryPrepare", rawls, value)!
                && (bool)Call("RawlsPositionFlow", "TryCommitPending", rawls, journey, graph)!,
                "Rawls's closing scene commits " + name + ".");
        }
    }

    private static async Task CheckMontesquieuKantRawls()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(RouteTurnQuestion));
        ModelDb.Inject(typeof(KantUniversalization));
        ModelDb.Inject(typeof(RawlsPosition));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(PlatoCivicSeal), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseDirectionDirect(run, fixture, "PLATO__FREEDOM_AND_INSTITUTIONS__CORE", "TO_MONTESQUIEU");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        object shared = live[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_171")
            && (string)Get(journey, "RouteId") == "CHECKS_AND_JUSTICE",
            "The real Montesquieu direction takes 171 and names the checks-and-justice line.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: KantUniversalization },
            "Montesquieu's turn opens Kant on 172 at the real second-act boundary, got "
            + (prepare[1] is EventRoom er ? er.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object kant = Get(shared, "KantUniversalization");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(kant, "ActIndex") == 2 && (string)Get(kant, "EdgeId") == "WESTERN_EDGE_172",
            "The real checks line opens Kant on its act-two edge 172.");
        EventRoom kantRoom = (EventRoom)prepare[1]!;
        Set(run, "_currentRooms", new List<AbstractRoom> { kantRoom });
        using (var saves = new NativeEventMemorySaveFixture(fixture, run))
        {
            EventModel local = kantRoom.CanonicalEvent.ToMutable();
            Set(local, "Owner", fixture.Player);
            object synchronizer = RuntimeHelpers.GetUninitializedObject(
                typeof(RunManager).GetProperty("EventSynchronizer")!.PropertyType);
            Set(synchronizer, "_events", new List<EventModel> { local });
            Set(synchronizer, "_playerCollection", run);
            Set(synchronizer, "_localPlayerId", fixture.Player.NetId);
            Set(synchronizer, "_pendingOptionTasks", new List<Task>());
            Set(RunManager.Instance, "EventSynchronizer", synchronizer);
            var configure = (Action<EventModel>?)typeof(EventRoom).GetProperty("OnStart", Methods)!.GetValue(kantRoom);
            configure?.Invoke(local);
            await local.AfterEventStarted();
            Check(ReferenceEquals(kantRoom.LocalMutableEvent, local),
                "Kant's local event is the room's mutable event.");
            Check((bool)GetField(local, "_ready") && !(bool)GetField(local, "_blocked"),
                "Kant's real second-act AfterEventStarted must become ready; ready="
                + GetField(local, "_ready") + " blocked=" + GetField(local, "_blocked") + ".");
            Check(local.Description.LocEntryKey.EndsWith(
                    "KANT_UNIVERSALIZATION.pages.INVITATION_EARLY.description", StringComparison.Ordinal)
                && local.CurrentOptions.Any(option => option.HistoryName?.LocEntryKey?.EndsWith(
                    ".options.KEEP_ROUSSEAU_EARLY.title", StringComparison.Ordinal) == true),
                "Kant's early page and decline button must name the act-two handover, not act three; options="
                + string.Join(",", local.CurrentOptions.Select(option => option.HistoryName?.LocEntryKey ?? "<null>")) + ".");
            Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption)
                .GetProperty("OnChosen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(option);
            foreach (string action in new[] { "STATE_MAXIM", "UNIVERSAL_RULE", "LEAVE" })
            {
                EventOption? option = local.CurrentOptions.SingleOrDefault(candidate =>
                    candidate.HistoryName.LocEntryKey.EndsWith(".options." + action + ".title", StringComparison.Ordinal));
                Check(option is not null && Callback(option) is not null,
                    "Kant's real button " + action + " is visible and live.");
                await Callback(option)!();
                Check(!(bool)GetField(local, "_blocked"),
                    "Kant's real button " + action + " must not freeze the room.");
            }
        }
        Set(run, "_currentRooms", new List<AbstractRoom>());
        var history = new List<List<MapPointHistoryEntry>>();
        for (int act = 0; act <= 2; act++)
        {
            var points = new List<MapPointHistoryEntry>();
            if (act == 1)
            {
                points.Add(new() { MapPointType = MapPointType.Unknown,
                    Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<KantUniversalization>() }] });
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            }
            else if (act == 2)
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            history.Add(points);
        }
        Set(run, "_mapPointHistory", history);
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: RawlsPosition },
            "Kant's real 173 handover opens Rawls in the third act, got "
            + (prepare[1] is EventRoom rawlsRoom ? rawlsRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object rawls = Get(shared, "RawlsPosition");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(rawls, "ActIndex") == 3 && (string)Get(rawls, "RequiredEdgeId") == "WESTERN_EDGE_172",
            "Rawls's real closing record follows Kant's 172 handover.");
        foreach (string name in new[] { "DraftMinimum", "Execute", "Leave" })
        {
            object value = Enum.Parse(T("RawlsPositionAction"), name);
            Check((bool)Call("RawlsPositionFlow", "TryPrepare", rawls, value)!
                && (bool)Call("RawlsPositionFlow", "TryCommitPending", rawls, journey, graph)!,
                "Rawls's closing scene commits " + name + ".");
        }
    }

    private static void CheckEarlyKierkegaardScene()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(KierkegaardFaith));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", "TO_KIERKEGAARD");
        object? direct = Call("KierkegaardFaithPatch", "TrySelectNative", run);
        EventModel selected = run.Act.PullNextEvent(run);
        Check(selected is KierkegaardFaith,
            "The real Kierkegaard direction must show Kierkegaard's own scene, got "
            + selected.GetType().Name + "; direct=" + (direct?.GetType().Name ?? "null")
            + ", entry=" + (bool)Call("NativeQuestionNodeEvents", "IsEntry", run)
            + ", act=" + run.CurrentActIndex + ", rooms=" + run.CurrentRoomCount + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "KierkegaardFaith");
        object journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 1 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_158",
            "The 158 scene must record its first-act identity without spending a second fixed slot.");
        object action = Enum.Parse(T("KierkegaardFaithAction"), "FaithLeap");
        Check((bool)Call("KierkegaardFaithFlow", "TryPrepare", state, action)!
            && (bool)Call("KierkegaardFaithFlow", "TryCommitPending", state, journey,
                Call("WesternRouteGraph", "LoadEmbedded")!),
            "The 158 scene commits the leap without moving off Kierkegaard.");
        Check((bool)Get(state, "EdgeCommitted")
            && ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_158")
            && (string)Get(journey, "CurrentNodeId") == "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE"
            && (int)Get(journey, "LastFixedAct") == 1,
            "The 158 scene leaves the second-act Sartre room untouched.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] next = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", next)!
            && next[1] is EventRoom { CanonicalEvent: SartreExistence },
            "The completed Kierkegaard scene must still leave Sartre at the second-act boundary.");
    }

    private static void CheckEarlySartreContinuation()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(SartreExistence));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", "TO_KIERKEGAARD");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_158")
            && (string)Get(journey, "RouteId") == "INDIVIDUAL_COMMITMENT",
            "The real Kierkegaard direction takes 158 and names the individual line.");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: SartreExistence },
            "The real Kierkegaard direction opens Sartre at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "SartreExistence");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_163",
            $"The early Sartre room uses the individual line's 163 at act two, got {Get(state, "ActIndex")}/{Get(state, "EdgeId")}.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "SartreExistence");
        journey = Get(shared, "WesternJourney");
        object action = Enum.Parse(T("SartreExistenceAction"), "ExistencePrecedesEssence");
        Check((bool)Call("SartreExistenceFlow", "TryPrepare", state, action)!
            && (bool)Call("SartreExistenceFlow", "TryCommitPending", state, journey,
                Call("WesternRouteGraph", "LoadEmbedded")!),
            "The early Sartre room commits its existence standard.");
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_163"),
            "The early Sartre choice reaches Sartre on the individual line's 163.");
    }

    private static void CheckEarlyPeirceContinuation()
    {
        ModelDb.Inject(typeof(PeirceInquiry));
        ModelDb.Inject(typeof(JamesPragmatism));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesQuestionCup), 1])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 1);
        Put(journey, "Act", 1);
        Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph,
            [journey, "WESTERN_EDGE_069"])!, "The real first-act fallible-inquiry turn applies.");
        Check((bool)graph.GetType().GetMethod("TryNameRoute")!.Invoke(graph,
            [journey, "FALLIBLE_INQUIRY"])!, "The fallible-inquiry turn names its line.");
        Put(journey, "TurnedAct", 1);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: PeirceInquiry },
            "The fallible-inquiry turn opens Peirce at the real second-act boundary.");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "PeirceInquiry");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_070",
            "The early Peirce room uses 070 and its actual second-act identity.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "PeirceInquiry");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "TestByConsequences", "Leave" })
        {
            object action = Enum.Parse(T("PeirceInquiryAction"), name);
            Check((bool)Call("PeirceInquiryFlow", "TryPrepare", state, action)!
                && (bool)Call("PeirceInquiryFlow", "TryCommitPending", state, journey, graph)!,
                "The early Peirce continuation commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_070")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "Peirce completes 070 without consuming a second-act question.");
        Set(run, "_currentRooms", new List<AbstractRoom>());
        EventModel james = run.Act.PullNextEvent(run);
        Check(james is JamesPragmatism,
            "The same run's real native question selects independent James after Peirce, got "
            + james.GetType().Name + ".");
    }

    private static void CheckEarlySmithContinuation()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(MillHarm));
        ModelDb.Inject(typeof(ArendtBeginning));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(PlatoCivicSeal), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseDirection(run, fixture, "PLATO__FREEDOM_AND_INSTITUTIONS__CORE", "TO_SMITH");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object journey = Get(live[1]!, "WesternJourney");
        object shared = live[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_174")
            && (string)Get(journey, "RouteId") == "SYMPATHY_AND_PUBLIC_ACTION",
            "The real Smith direction takes 174 and names the sympathy line.");
        // The early turn already stands on Smith. The second act opens Mill
        // directly on 055; Smith's own room is the default act-two path, not
        // a second fixed slot after the early turn.
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: MillHarm },
            "The early Smith turn opens Mill at the real second-act boundary, got "
            + (prepare[1] is EventRoom er ? er.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "MillHarm");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_055"
            && (string)Get(state, "RoomSourceNodeId") == "ADAM_SMITH__FREEDOM_AND_INSTITUTIONS__CORE",
            "The early Smith continuation uses Mill's actual act-two edge 055.");
        foreach (string name in new[] { "ApplyHarmTest", "ProtectConduct", "Leave" })
        {
            object action = Enum.Parse(T("MillHarmAction"), name);
            Check((bool)Call("MillHarmFlow", "TryPrepare", state, action)!
                && (bool)Call("MillHarmFlow", "TryCommitPending", state, journey, graph)!,
                "The early Smith continuation commits Mill's " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_055"),
            "Mill's early room completes 055 before the third act.");
        var history = new List<List<MapPointHistoryEntry>>();
        for (int act = 0; act <= 2; act++)
        {
            var points = new List<MapPointHistoryEntry>();
            if (act == 1)
            {
                points.Add(new() { MapPointType = MapPointType.Unknown,
                    Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<MillHarm>() }] });
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            }
            else if (act == 2)
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            history.Add(points);
        }
        Set(run, "_mapPointHistory", history);
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: ArendtBeginning },
            "Mill's early handover opens Arendt on 056 in the third act, got "
            + (prepare[1] is EventRoom arendtRoom ? arendtRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object arendt = Get(shared, "ArendtBeginning");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(arendt, "ActIndex") == 3
            && (string)Get(arendt, "RequiredEdgeId") == "WESTERN_EDGE_055",
            "Arendt's closing record follows Mill's early 055 handover.");
        foreach (string name in new[] { "ObserveWaypoint", "InviteProposal", "Leave" })
        {
            object value = Enum.Parse(T("ArendtBeginningAction"), name);
            Check((bool)Call("ArendtBeginningFlow", "TryPrepare", arendt, value)!
                && (bool)Call("ArendtBeginningFlow", "TryCommitPending", arendt, journey, graph)!,
                "Arendt's closing scene commits " + name + ".");
        }
    }

    // A route-turn question moves the protagonist onto the question node in act
    // one. That figure's own room must open on the next native pull before the
    // successor line starts, otherwise the turn is only a renamed default.
    private static async Task CheckFirstActTurnScenes()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        CheckFirstActSpinoza();
        CheckFirstActFreud();
        CheckFirstActHusserl();
        await CheckBaconDiderotContinuation();
    }

    private static void CheckMarxAfterEarlyHegel()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(MarxAlienation));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 2])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Put(journey, "CurrentNodeId", "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE");
        Put(journey, "RouteId", "AUTONOMY_AND_HISTORY");
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 2);
        Put(journey, "Act", 3);
        ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_153");
        ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_154");
        var history = new List<List<MapPointHistoryEntry>>();
        for (int act = 0; act <= 2; act++)
        {
            var points = new List<MapPointHistoryEntry>();
            if (act == 1)
            {
                points.Add(new() { MapPointType = MapPointType.Unknown,
                    Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<HegelRecognition>() }] });
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            }
            else if (act == 2)
                points.Add(new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] });
            history.Add(points);
        }
        Set(run, "_mapPointHistory", history);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: MarxAlienation },
            "The early Kant/Hegel line must open Marx on 155 at the third-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] live = [run, null];
        Call("PhilosophyRunStateService", "TryGet", live);
        object state = Get(live[1]!, "MarxAlienation")!;
        Check((int)Get(state, "ActIndex") == 3 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_155"
            && (string)Get(state, "RouteSourceNodeId") == "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE",
            "Marx's early handover must record act three, edge 155, and Hegel as its real source.");
    }

    private static async Task CheckHegelEntryPersistence()
    {
        typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
        ModelDb.Inject(typeof(HegelRecognition));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Put(journey, "CurrentNodeId", "IMMANUEL_KANT__SELF_AND_OTHER__CORE");
        Put(journey, "RouteId", "AUTONOMY_AND_HISTORY");
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 2);
        Put(journey, "Act", 3);
        ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_153");
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: HegelRecognition },
            "The real Hegel boundary prepares its own room, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        EventRoom hegelRoom = (EventRoom)prepare[1]!;
        Set(run, "_currentRooms", new List<AbstractRoom> { hegelRoom });
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        EventModel local = hegelRoom.CanonicalEvent.ToMutable();
        Set(local, "Owner", fixture.Player);
        object synchronizer = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(RunManager).GetProperty("EventSynchronizer")!.PropertyType);
        Set(synchronizer, "_events", new List<EventModel> { local });
        Set(synchronizer, "_playerCollection", run);
        Set(synchronizer, "_localPlayerId", fixture.Player.NetId);
        Set(synchronizer, "_pendingOptionTasks", new List<Task>());
        Set(RunManager.Instance, "EventSynchronizer", synchronizer);
        var configure = (Action<EventModel>?)typeof(EventRoom).GetProperty("OnStart", Methods)!.GetValue(hegelRoom);
        configure?.Invoke(local);
        await local.AfterEventStarted();
        Check(!(bool)GetField(local, "_blocked"),
            "HegelRecognition's real AfterEventStarted does not freeze on a registered room save.");
        var loaded = saves.Manager.LoadRunSave();
        Check(loaded.Success && loaded.SaveData!.PreFinishedRoom is { } saved
            && Equals(saved.EventId, ModelDb.GetId<HegelRecognition>()),
            "HegelRecognition's real entry save is readable as its exact pre-finished room.");
    }

    private static void CheckFirstActSpinoza()
    {
        ModelDb.Inject(typeof(SpinozaCauseNetwork));
        ModelDb.Inject(typeof(SchellingNature));
        ModelDb.Inject(typeof(DeleuzeDifference));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(HeraclitusLyre), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "HERACLITUS__BEING_AND_CHANGE__CORE", "TO_SPINOZA");
        // The first-act turn skips Spinoza's own room: the second-act fixed
        // slot is Schelling on 162, and Deleuze closes on 157 in act three.
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: SchellingNature },
            "The turned Spinoza line opens Schelling at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "SchellingNature");
        object journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2
            && (string)Get(state, "EdgeId") == "WESTERN_EDGE_162"
            && (string)Get(state, "RouteSourceNodeId") == "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE"
            && (int)Get(journey, "LastFixedAct") == 2,
            "The turned nature line places Schelling's own room on 162, got act="
            + Get(state, "ActIndex") + " edge=" + Get(state, "EdgeId")
            + " source=" + Get(state, "RouteSourceNodeId") + ".");
        foreach (string action in new[] { "NatureAsSpirit", "Leave" })
        {
            object value = Enum.Parse(T("SchellingNatureAction"), action);
            Check((bool)Call("SchellingNatureFlow", "TryPrepare", state, value)!
                && (bool)Call("SchellingNatureFlow", "TryCommitPending", state, journey,
                    Call("WesternRouteGraph", "LoadEmbedded")!),
                "The turned Schelling room commits " + action + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_162")
            && (string)Get(journey, "CurrentNodeId") == "FRIEDRICH_WILHELM_JOSEPH_SCHELLING__BEING_AND_CHANGE__CORE"
            && (int)Get(journey, "LastFixedAct") == 3,
            "The turned Schelling answer reaches Schelling on 162.");
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] third = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", third)!
            && third[1] is EventRoom { CanonicalEvent: DeleuzeDifference },
            "The turned nature line opens Deleuze at the third-act boundary, got "
            + (third[1] is EventRoom thirdRoom ? thirdRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object deleuze = Get(shared, "DeleuzeDifference");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(deleuze, "ActIndex") == 3
            && (string)Get(deleuze, "EdgeId") == "WESTERN_EDGE_157"
            && (string)Get(journey, "CurrentNodeId") == "FRIEDRICH_WILHELM_JOSEPH_SCHELLING__BEING_AND_CHANGE__CORE",
            "The turned nature line gives Deleuze its 157 resolution after Schelling.");
        foreach (string action in new[] { "DifferenceRepetition", "Leave" })
        {
            object value = Enum.Parse(T("DeleuzeDifferenceAction"), action);
            Check((bool)Call("DeleuzeDifferenceFlow", "TryPrepare", deleuze, value)!
                && (bool)Call("DeleuzeDifferenceFlow", "TryCommitPending", deleuze, journey,
                    Call("WesternRouteGraph", "LoadEmbedded")!),
                "The turned Deleuze room commits " + action + ".");
        }
    }

    private static void CheckFirstActFreud()
    {
        ModelDb.Inject(typeof(FreudUnconscious));
        ModelDb.Inject(typeof(LacanSignifier));
        ModelDb.Inject(typeof(ButlerAddress));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "DESCARTES__SELF_AND_OTHER__CORE", "TO_FREUD");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: LacanSignifier },
            "The real Freud direction must open Lacan, not Freud's own room, at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "LacanSignifier");
        object journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2
            && (string)Get(state, "EdgeId") == "WESTERN_EDGE_190"
            && (string)Get(state, "RequiredEdge") == "WESTERN_EDGE_189"
            && (string)Get(state, "TargetNode") == "JACQUES_LACAN__SELF_AND_OTHER__CORE",
            "The turned unconscious line opens Lacan on 190, got act="
            + Get(state, "ActIndex") + " edge=" + Get(state, "EdgeId")
            + " required=" + Get(state, "RequiredEdge") + ".");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "LacanSignifier");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "BindKeeper", "Clarify", "Leave" })
        {
            object action = Enum.Parse(T("LacanSignifierAction"), name);
            Check((bool)Call("LacanSignifierFlow", "TryPrepare", state, action)!
                && (bool)Call("LacanSignifierFlow", "TryCommitPending", state, journey,
                    Call("WesternRouteGraph", "LoadEmbedded")!),
                "The turned Lacan room commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_190")
            && (string)Get(journey, "CurrentNodeId") == "JACQUES_LACAN__SELF_AND_OTHER__CORE"
            && (int)Get(journey, "LastFixedAct") == 3,
            "The turned Freud answer reaches Lacan on 190.");
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        // Butler opens after Lacan's closed room and a later fight, not merely
        // because the route is named.
        var behaviors = (System.Collections.IDictionary)Get(shared, "ActBehaviorStates")!;
        if (!behaviors.Contains(2)) behaviors[2] = Activator.CreateInstance(T("ActBehaviorState"), nonPublic: true)!;
        Put(behaviors[2]!, "ActIndex", 2); Put(behaviors[2]!, "CompletedCombatCount", 1);
        var acts = (System.Collections.IList)Get(run, "MapPointHistory")!;
        while (acts.Count <= 2) acts.Add(new List<MapPointHistoryEntry>());
        var actHistory = (System.Collections.IList)acts[2]!;
        actHistory.Add(new MapPointHistoryEntry
        {
            MapPointType = MapPointType.Unknown,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<LacanSignifier>() }],
            PlayerStats = [new() { PlayerId = run.Players[0].NetId }],
        });
        actHistory.Add(new MapPointHistoryEntry
        {
            MapPointType = MapPointType.Monster,
            Rooms = [new() { RoomType = RoomType.Monster }],
            PlayerStats = [new() { PlayerId = run.Players[0].NetId }],
        });
        object?[] third = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", third)!
            && third[1] is EventRoom { CanonicalEvent: ButlerAddress },
            "The turned unconscious line opens Butler at the third-act boundary, got "
            + (third[1] is EventRoom thirdRoom ? thirdRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object butler = Get(shared, "ButlerAddress");
        Check((int)Get(butler, "ActIndex") == 3,
            "Butler's turned room records the third act.");
    }

    private static void CheckFirstActHusserl()
    {
        ModelDb.Inject(typeof(HusserlEpoche));
        ModelDb.Inject(typeof(HeideggerDasein));
        ModelDb.Inject(typeof(GadamerFusion));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "DESCARTES__SELF_AND_OTHER__CORE", "TO_HUSSERL_APPEARANCE");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: HeideggerDasein },
            "The turned appearance line must open Heidegger, not Husserl's own room, at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "HeideggerDasein");
        object journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_164"
            && (string)Get(state, "RouteSourceNodeId") == "EDMUND_HUSSERL__SELF_AND_OTHER__CORE",
            "The turned Husserl line opens Heidegger on 164.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "HeideggerDasein");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "BeingInSituation", "Leave" })
        {
            object action = Enum.Parse(T("HeideggerDaseinAction"), name);
            Check((bool)Call("HeideggerDaseinFlow", "TryPrepare", state, action)!
                && (bool)Call("HeideggerDaseinFlow", "TryCommitPending", state, journey,
                    Call("WesternRouteGraph", "LoadEmbedded")!),
                "The turned Heidegger room commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_164")
            && (string)Get(journey, "CurrentNodeId") == "MARTIN_HEIDEGGER__SELF_AND_OTHER__CORE"
            && (int)Get(journey, "LastFixedAct") == 3,
            "The turned Husserl answer reaches Heidegger on 164.");
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] third = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", third)!
            && third[1] is EventRoom { CanonicalEvent: GadamerFusion },
            "The turned appearance line opens Gadamer at the third-act boundary, got "
            + (third[1] is EventRoom thirdRoom ? thirdRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object gadamer = Get(shared, "GadamerFusion");
        Check((int)Get(gadamer, "ActIndex") == 3,
            "Gadamer's turned room records the third act.");
    }

    private static async Task CheckBaconDiderotContinuation()
    {
        ModelDb.Inject(typeof(BaconSampleLedger));
        ModelDb.Inject(typeof(DiderotPublicPractice));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(RousseauUncarvedStone), 0])!;
        var run = (RunState)tuple[1]!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        ChooseVirtueDirection(run, fixture, "ROUSSEAU__HISTORY_AND_POWER__CORE", "TO_BACON");
        Set(run, "_currentActIndex", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: BaconSampleLedger },
            "The real Bacon turn opens Bacon at the second-act boundary, got "
            + (prepare[1] is EventRoom room ? room.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object shared = query[1]!;
        object state = Get(shared, "BaconSampleLedger");
        object journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (int)Get(journey, "LastFixedAct") == 2,
            "The Bacon room owns the second-act fixed slot.");
        // The actual second-act room must accept its own activity identity and
        // complete the awaited entry save before any button is usable.
        EventRoom baconRoom = (EventRoom)prepare[1]!;
        Set(run, "_currentRooms", new List<AbstractRoom> { baconRoom });
        object? originalActs = Get(run, "Acts");
        object? originalOdds = Get(run, "Odds");
        using (var saves = new NativeEventMemorySaveFixture(fixture, run))
        {
            EventModel local = baconRoom.CanonicalEvent.ToMutable();
            Set(local, "Owner", fixture.Player);
            object synchronizer = RuntimeHelpers.GetUninitializedObject(
                typeof(RunManager).GetProperty("EventSynchronizer")!.PropertyType);
            Set(synchronizer, "_events", new List<EventModel> { local });
            Set(synchronizer, "_playerCollection", run);
            Set(synchronizer, "_localPlayerId", fixture.Player.NetId);
            Set(synchronizer, "_pendingOptionTasks", new List<Task>());
            Set(RunManager.Instance, "EventSynchronizer", synchronizer);
            var configure = (Action<EventModel>?)typeof(EventRoom).GetProperty("OnStart", Methods)!.GetValue(baconRoom);
            configure?.Invoke(local);
            await local.AfterEventStarted();
            Check((bool)GetField(local, "_ready") && !(bool)GetField(local, "_blocked")
                && saves.SaveAttempts > 0,
                "Bacon's real second-act AfterEventStarted must accept its room and persist before ready.");
            var loaded = saves.Manager.LoadRunSave();
            Check(loaded.Success && loaded.SaveData!.PreFinishedRoom is { } savedRoom
                && Equals(savedRoom.EventId, ModelDb.GetId<BaconSampleLedger>()),
                "Bacon's real second-act entry save is readable as its exact pre-finished room.");
            Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption)
                .GetProperty("OnChosen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(option);
            foreach (string action in new[] { "ACCEPT_INVITATION", "CHECK_TRIBE", "TAKE_COUNTER_SAMPLE", "PUBLISH_LIMITED", "LEAVE" })
            {
                string available = string.Join(",", local.CurrentOptions.Select(candidate =>
                    candidate.HistoryName.LocEntryKey));
                EventOption? option = local.CurrentOptions.SingleOrDefault(candidate =>
                    candidate.HistoryName.LocEntryKey.EndsWith(".options." + action + ".title", StringComparison.Ordinal));
                Check(option is not null, "Bacon's real button " + action + " is visible; options=" + available);
                Check(Callback(option) is not null, "Bacon's real button " + action + " has a callback.");
                await Callback(option)!();
                Check(!(bool)GetField(local, "_blocked"),
                    "Bacon's real button " + action + " must not freeze the room.");
            }
        }
        Put(run, "Acts", originalActs);
        Put(run, "Odds", originalOdds);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check((bool)Get(state, "EdgeCommitted") && (int)Get(journey, "LastFixedAct") == 2
            && (string)Get(journey, "CurrentNodeId") == "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            "Bacon's completed report keeps him on the social-knowledge node for Diderot.");
        EventModel practice = run.Act.PullNextEvent(run);
        Check(practice is DiderotPublicPractice,
            "The completed Bacon room opens Diderot's public practice, got " + practice.GetType().Name + ".");
    }

    private static void CheckEarlyNietzscheContinuation()
    {
        ModelDb.Inject(typeof(FoucaultPower));
        ModelDb.Inject(typeof(BenjaminHistory));
        ModelDb.Inject(typeof(ArendtBeginning));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(RousseauUncarvedStone), 1])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ROUSSEAU__HISTORY_AND_POWER__CORE"])!;
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 1);
        Put(journey, "Act", 1);
        Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph,
            [journey, "WESTERN_EDGE_180"])!, "The real first-act Nietzsche turn applies.");
        Put(journey, "TurnedAct", 1); // Written by RouteTurnQuestion.Choose.
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        shared.GetType().GetMethod("RecordCurrentDoctrine")!.Invoke(shared,
            ["ROUSSEAU", "ROUSSEAU_UNCARVED_STONE", new[] { "WESTERN", "HISTORY_AND_POWER" }]);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: FoucaultPower },
            "The Nietzsche turn opens Foucault at the actual second-act boundary.");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "FoucaultPower");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_181",
            "Early Foucault uses 181 instead of the legacy 063 or default 121.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "FoucaultPower");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "PowerKnowledge", "Leave" })
        {
            object action = Enum.Parse(T("FoucaultPowerAction"), name);
            Check((bool)Call("FoucaultPowerFlow", "TryPrepare", state, action)!
                && (bool)Call("FoucaultPowerFlow", "TryCommitPending", state, journey, graph)!,
                "Early Foucault commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_181")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "Early Foucault completes its fixed edge without spending a question.");
        Set(run, "_currentRooms", new List<AbstractRoom>());
        EventModel question = run.Act.PullNextEvent(run);
        Check(question is BenjaminHistory,
            "The actual native question after early Foucault selects independent Benjamin.");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        Check(!((HashSet<int>)Get(Get(shared, "WesternJourney"), "ResolvedQuestionActs")).Contains(2),
            "Presenting Benjamin does not consume the main question.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], question, shared, 1]);
    }

    private static void CheckEarlyHabermasContinuation()
    {
        ModelDb.Inject(typeof(HorkheimerReason));
        ModelDb.Inject(typeof(HabermasCommunication));
        ModelDb.Inject(typeof(AdornoNegative));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(RousseauUncarvedStone), 1])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ROUSSEAU__HISTORY_AND_POWER__CORE"])!;
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 1);
        Put(journey, "Act", 1);
        Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph,
            [journey, "WESTERN_EDGE_177"])!, "The real first-act Marx turn applies.");
        Put(journey, "TurnedAct", 1); // Written by RouteTurnQuestion.Choose.
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: HorkheimerReason },
            "The Marx turn opens Horkheimer at the actual second-act boundary.");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "HorkheimerReason");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_084",
            "Early Horkheimer takes 084, the handover Habermas requires.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "HorkheimerReason");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "InstrumentalReason", "Leave" })
        {
            object action = Enum.Parse(T("HorkheimerReasonAction"), name);
            Check((bool)Call("HorkheimerReasonFlow", "TryPrepare", state, action)!
                && (bool)Call("HorkheimerReasonFlow", "TryCommitPending", state, journey, graph)!,
                "Early Horkheimer commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_084")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2)
            && (string)Get(journey, "CurrentNodeId") == "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE",
            "Early Horkheimer completes 084 without spending a question.");
        Set(run, "_currentActIndex", 2);
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: HabermasCommunication },
            "The same run continues to Habermas's fixed room at the third-act boundary.");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object habermas = Get(shared, "HabermasCommunication");
        Check((bool)Get(habermas, "FixedMode") && (string)Get(habermas, "EdgeId") == "WESTERN_EDGE_200",
            "The fixed room takes 200, not the old 086 question edge.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 2]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        habermas = Get(shared, "HabermasCommunication");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "CommunicativeAction", "Leave" })
        {
            object action = Enum.Parse(T("HabermasCommunicationAction"), name);
            Check((bool)Call("HabermasCommunicationFlow", "TryPrepare", habermas, action)!
                && (bool)Call("HabermasCommunicationFlow", "TryCommitPending", habermas, journey, graph)!,
                "Early Habermas commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_200")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(3)
            && (string)Get(journey, "CurrentNodeId") == "JURGEN_HABERMAS__HISTORY_AND_POWER__CORE",
            "Habermas closes the third act on 200 without spending a question.");
    }

    private static void CheckEarlySaussureContinuation()
    {
        ModelDb.Inject(typeof(SaussureSign));
        ModelDb.Inject(typeof(LeviStraussMyth));
        ModelDb.Inject(typeof(DerridaDeconstruction));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(AristotleCategoryTablet), 1])!;
        var run = (RunState)tuple[1]!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ARISTOTLE__LANGUAGE_AND_MEANING__CORE"])!;
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 1);
        Put(journey, "Act", 1);
        Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph,
            [journey, "WESTERN_EDGE_186"])!, "The real first-act Saussure turn applies.");
        // RouteTurnQuestion.Choose records this after committing the edge.
        Put(journey, "TurnedAct", 1);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: LeviStraussMyth },
            "The Saussure turn must open Levi-Strauss, not Saussure's own room, at the real second-act boundary; got "
            + (prepare[1] is EventRoom secondRoom ? secondRoom.CanonicalEvent.GetType().Name : "none") + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "LeviStraussMyth");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_187"
            && (string)Get(state, "RouteSourceNodeId") == "FERDINAND_DE_SAUSSURE__LANGUAGE_AND_MEANING__CORE",
            "The turned structure line opens Levi-Strauss on its actual second-act edge 187.");
        foreach (string name in new[] { "StructuralReading", "Leave" })
        {
            object action = Enum.Parse(T("LeviStraussMythAction"), name);
            Check((bool)Call("LeviStraussMythFlow", "TryPrepare", state, action)!
                && (bool)Call("LeviStraussMythFlow", "TryCommitPending", state, journey, graph)!,
                "The turned Levi-Strauss room commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_187")
            && (string)Get(journey, "CurrentNodeId") == "CLAUDE_LEVI_STRAUSS__LANGUAGE_AND_MEANING__CORE"
            && (int)Get(journey, "LastFixedAct") == 3,
            "The turned Saussure answer reaches Levi-Strauss on 187.");
        Set(run, "_currentActIndex", 2);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: DerridaDeconstruction },
            "The turned structure line opens Derrida at the third-act boundary, got "
            + (prepare[1] is EventRoom thirdRoom ? thirdRoom.CanonicalEvent.GetType().Name : "none") + ".");
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "DerridaDeconstruction");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 3 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_188",
            "The third-act Derrida room uses 188.");
        foreach (string name in new[] { "Differance", "Leave" })
        {
            object action = Enum.Parse(T("DerridaDeconstructionAction"), name);
            Check((bool)Call("DerridaDeconstructionFlow", "TryPrepare", state, action)!
                && (bool)Call("DerridaDeconstructionFlow", "TryCommitPending", state, journey, graph)!,
                "The third-act Derrida room commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_188")
            && (string)Get(journey, "CurrentNodeId") == "JACQUES_DERRIDA__LANGUAGE_AND_MEANING__CORE",
            "The third-act Derrida answer reaches Derrida on 188.");
    }

    private static void CheckBenjaminIndependentQuestion()
    {
        ModelDb.Inject(typeof(FoucaultPower));
        ModelDb.Inject(typeof(BenjaminHistory));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesQuestionCup), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "CurrentNodeId", "MICHEL_FOUCAULT__HISTORY_AND_POWER__CORE");
        Put(journey, "LastFixedAct", 3);
        object foucault = Call("FoucaultPowerState", "CreateAt",
            "FOUCAULT_POWER:TEST:2", 2, "WESTERN_EDGE_063")!;
        Put(foucault, "Closed", true);
        Put(foucault, "EdgeCommitted", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Put(shared, "FoucaultPower", foucault);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object CurrentShared()
        {
            object?[] query = [run, null];
            Call("PhilosophyRunStateService", "TryGet", query);
            return query[1]!;
        }

        object wrongSaved = Call("BenjaminHistoryState", "CreateAt", "BENJAMIN_HISTORY:TEST:1", 1)!;
        Put(CurrentShared(), "BenjaminHistory", wrongSaved);
        Check(Call("BenjaminHistoryPatch", "TrySelectNative", run) is null,
            "BenjaminHistoryPatch rejects an open record from the wrong act.");
        Put(CurrentShared(), "BenjaminHistory", null);
        Set(run, "_currentActIndex", 2);
        Check(Call("BenjaminHistoryPatch", "TrySelectNative", run) is null,
            "BenjaminHistoryPatch rejects a wrong-act question node.");
        Set(run, "_currentActIndex", 1);
        object? selected = Call("BenjaminHistoryPatch", "TrySelectNative", run);
        Check(selected is BenjaminHistory,
            "Benjamin's independent perspective opens after Foucault's closed fixed room.");
        object state = Get(CurrentShared(), "BenjaminHistory");
        Check(state is not null && Convert.ToInt32(Get(state, "ActIndex")) == 2
            && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "Benjamin's independent perspective persists a usable second-act record.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, fixture.Player, selected!, CurrentShared(), 1]);
        state = Get(CurrentShared(), "BenjaminHistory");
        Put(state, "Closed", true);
        Check((bool)Get(state, "Closed")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "Benjamin's independent perspective closes without spending the main question.");
    }

    private static void CheckBerkeleyIndependentQuestion()
    {
        ModelDb.Inject(typeof(LockeCargoIdentity));
        ModelDb.Inject(typeof(BerkeleyPerceivedMark));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "CurrentNodeId", "JOHN_LOCKE__SELF_AND_OTHER__CORE");
        Put(journey, "LastFixedAct", 3);
        object locke = Call("LockeCargoIdentityState", "CreateAt",
            "LOCKE_CARGO_IDENTITY:TEST:2", 2)!;
        Put(locke, "Closed", true);
        Put(locke, "EdgeCommitted", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Put(shared, "LockeCargoIdentity", locke);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object CurrentShared()
        {
            object?[] query = [run, null];
            Call("PhilosophyRunStateService", "TryGet", query);
            return query[1]!;
        }

        object wrongSaved = Call("BerkeleyPerceivedMarkState", "CreateAt", "BERKELEY_PERCEIVED_MARK:TEST:1", 1)!;
        Put(CurrentShared(), "BerkeleyPerceivedMark", wrongSaved);
        Check(Call("BerkeleyPerceivedMarkPatch", "TrySelectNative", run) is null,
            "BerkeleyPerceivedMarkPatch rejects an open record from the wrong act.");
        Put(CurrentShared(), "BerkeleyPerceivedMark", null);
        Set(run, "_currentActIndex", 2);
        Check(Call("BerkeleyPerceivedMarkPatch", "TrySelectNative", run) is null,
            "BerkeleyPerceivedMarkPatch rejects a wrong-act question node.");
        Set(run, "_currentActIndex", 1);
        object? selected = Call("BerkeleyPerceivedMarkPatch", "TrySelectNative", run);
        Check(selected is BerkeleyPerceivedMark,
            "Berkeley's independent question opens after Locke's closed fixed room.");
        object state = Get(CurrentShared(), "BerkeleyPerceivedMark");
        Check(state is not null && Convert.ToInt32(Get(state, "ActIndex")) == 2
            && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "Berkeley's independent question persists a usable second-act record.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, fixture.Player, selected!, CurrentShared(), 1]);
        foreach (string action in new[] { "CheckTouch", "RejectMaterialSubstance", "Leave" })
        {
            object parsed = Enum.Parse(T("BerkeleyMarkAction"), action);
            Check((bool)Call("BerkeleyPerceivedMarkFlow", "TryPrepare", state, parsed)!
                && (bool)Call("BerkeleyPerceivedMarkFlow", "TryCommitPending", state)!,
                "Berkeley's independent question commits " + action + ".");
        }
        Check((bool)Get(state, "Closed")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "Berkeley's independent question closes without spending the main question.");
        CheckBerkeleyNestedSegmentBlocksIndependent();
    }

    private static void CheckBerkeleyNestedSegmentBlocksIndependent()
    {
        ModelDb.Inject(typeof(LockeCargoIdentity));
        ModelDb.Inject(typeof(BerkeleyPerceivedMark));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 1])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "CurrentNodeId", "JOHN_LOCKE__SELF_AND_OTHER__CORE");
        Put(journey, "LastFixedAct", 3);
        object locke = Call("LockeCargoIdentityState", "CreateAt",
            "LOCKE_CARGO_IDENTITY:TEST:2", 2)!;
        Put(locke, "Closed", true);
        Put(locke, "EdgeCommitted", true);
        // The nested segment is completed through the production flow, not faked.
        object nested = Get(locke, "Berkeley")!;
        foreach (string action in new[] { "CheckTouch", "RejectMaterialSubstance", "Leave" })
        {
            object parsed = Enum.Parse(T("BerkeleyMarkAction"), action);
            Check((bool)Call("BerkeleyPerceivedMarkFlow", "TryPrepare", nested, parsed)!
                && (bool)Call("BerkeleyPerceivedMarkFlow", "TryCommitPending", nested)!,
                "The nested Berkeley segment commits " + action + ".");
        }
        Check((bool)Get(nested, "Closed"),
            "The nested Berkeley segment is genuinely closed before the negative case.");
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Put(shared, "LockeCargoIdentity", locke);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object CurrentShared()
        {
            object?[] query = [run, null];
            Call("PhilosophyRunStateService", "TryGet", query);
            return query[1]!;
        }

        Check(Call("BerkeleyPerceivedMarkPatch", "TrySelectNative", run) is null,
            "A completed nested Berkeley segment must not open a second independent encounter.");
        Check(Get(CurrentShared(), "BerkeleyPerceivedMark") is null,
            "The negative case must not persist an independent Berkeley record.");
        // An already-open independent record still resumes.
        object open = Call("BerkeleyPerceivedMarkState", "CreateAt", "BERKELEY_PERCEIVED_MARK:TEST:2", 2)!;
        Put(CurrentShared(), "BerkeleyPerceivedMark", open);
        Check(Call("BerkeleyPerceivedMarkPatch", "TrySelectNative", run) is BerkeleyPerceivedMark,
            "An existing independent Berkeley record still resumes.");
    }

    private static void CheckLegacyNestedOptionsAreHidden()
    {
        ModelDb.Inject(typeof(PeirceInquiry));
        ModelDb.Inject(typeof(FoucaultPower));
        EventModel peirceModel = ModelDb.Event<PeirceInquiry>().ToMutable();
        object peirce = Call("PeirceInquiryState", "CreateAt",
            "PEIRCE_INQUIRY:TEST:2", 2, "WESTERN_EDGE_070")!;
        Put(peirce, "Stage", Enum.Parse(T("PeirceInquiryStage"), "ResultTest"));
        MethodInfo peirceGenerate = typeof(PeirceInquiry).GetMethod("GenerateOptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        IReadOnlyList<EventOption> peirceOptions = (IReadOnlyList<EventOption>)peirceGenerate.Invoke(peirceModel, [peirce])!;
        Check(peirceOptions.All(option => !OptionKey(option).Contains("ASK_JAMES")),
            "A new Peirce run must not offer the old nested AskJames page.");
        object james = Call("JamesPragmatismState", "CreateAt", "JAMES_PRAGMATISM:TEST:2", 2)!;
        Put(james, "Closed", true);
        Put(peirce, "James", james);
        peirceOptions = (IReadOnlyList<EventOption>)peirceGenerate.Invoke(peirceModel, [peirce])!;
        Check(peirceOptions.All(option => !OptionKey(option).Contains("ASK_JAMES")),
            "A closed nested James page must not be offered again.");

        EventModel foucaultModel = ModelDb.Event<FoucaultPower>().ToMutable();
        object fresh = Call("FoucaultPowerState", "CreateAt",
            "FOUCAULT_POWER:TEST:6", 1, "WESTERN_EDGE_063")!;
        Put(fresh, "Stage", Enum.Parse(T("FoucaultPowerStage"), "ResultPower"));
        Put(fresh, "Standard", Enum.Parse(T("FoucaultPowerStandard"), "Power"));
        Put(fresh, "EdgeCommitted", true);
        Put(fresh, "ConfirmedActions", EnumList("FoucaultPowerAction", "PowerKnowledge"));
        MethodInfo foucaultGenerate = typeof(FoucaultPower).GetMethod("GenerateOptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        IReadOnlyList<EventOption> freshOptions = (IReadOnlyList<EventOption>)foucaultGenerate.Invoke(foucaultModel, [fresh])!;
        Check(freshOptions.All(option => !OptionKey(option).Contains("BENJAMIN_HISTORY")),
            "A new Foucault run must not offer the old nested Benjamin page.");

        object legacy = Call("FoucaultPowerState", "CreateAt",
            "FOUCAULT_POWER:TEST:7", 1, "WESTERN_EDGE_063")!;
        Put(legacy, "Version", 1);
        legacy.GetType().GetMethod("NormalizeAfterLoad", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(legacy, null);
        Put(legacy, "Stage", Enum.Parse(T("FoucaultPowerStage"), "ResultPower"));
        Put(legacy, "Standard", Enum.Parse(T("FoucaultPowerStandard"), "Power"));
        Put(legacy, "EdgeCommitted", true);
        Put(legacy, "ConfirmedActions", EnumList("FoucaultPowerAction", "PowerKnowledge"));
        IReadOnlyList<EventOption> legacyOptions = (IReadOnlyList<EventOption>)foucaultGenerate.Invoke(foucaultModel, [legacy])!;
        Check(legacyOptions.Any(option => OptionKey(option).Contains("BENJAMIN_HISTORY")),
            "An old Foucault save must keep the nested Benjamin page.");
    }

    private static object EnumList(string type, params string[] names)
    {
        var list = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(T(type)))!;
        foreach (string name in names) list.Add(Enum.Parse(T(type), name));
        return list;
    }

    private static string OptionKey(EventOption option) =>
        (string)Get(Get(option, "HistoryName"), "LocEntryKey");

    private static void CheckJamesIndependentQuestion()
    {
        ModelDb.Inject(typeof(PeirceInquiry));
        ModelDb.Inject(typeof(JamesPragmatism));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(SocratesQuestionCup), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Put(journey, "RouteId", "FALLIBLE_INQUIRY");
        Put(journey, "CurrentNodeId", "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE");
        Put(journey, "LastFixedAct", 3);
        object peirce = Call("PeirceInquiryState", "CreateAt",
            "PEIRCE_INQUIRY:TEST:2", 2, "WESTERN_EDGE_098")!;
        Put(peirce, "Closed", true);
        Put(peirce, "EdgeCommitted", true);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Put(shared, "PeirceInquiry", peirce);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object CurrentShared()
        {
            object?[] query = [run, null];
            Call("PhilosophyRunStateService", "TryGet", query);
            return query[1]!;
        }

        object wrongSaved = Call("JamesPragmatismState", "CreateAt", "JAMES_PRAGMATISM:TEST:1", 1)!;
        Put(CurrentShared(), "JamesPragmatism", wrongSaved);
        Check(Call("JamesPragmatismPatch", "TrySelectNative", run) is null,
            "JamesPragmatismPatch rejects an open record from the wrong act.");
        Put(CurrentShared(), "JamesPragmatism", null);
        Set(run, "_currentActIndex", 2);
        Check(Call("JamesPragmatismPatch", "TrySelectNative", run) is null,
            "JamesPragmatismPatch rejects a wrong-act question node.");
        Set(run, "_currentActIndex", 1);
        object? selected = Call("JamesPragmatismPatch", "TrySelectNative", run);
        Check(selected is JamesPragmatism,
            "James's independent question opens after Peirce's closed fixed room.");
        object state = Get(CurrentShared(), "JamesPragmatism");
        Check(state is not null && Convert.ToInt32(Get(state, "ActIndex")) == 2
            && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "James's independent question persists a usable second-act record.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, fixture.Player, selected!, CurrentShared(), 1]);
        foreach (string action in new[] { "TestPublicly", "Leave" })
        {
            object parsed = Enum.Parse(T("JamesPragmatismAction"), action);
            Check((bool)Call("JamesPragmatismFlow", "TryPrepare", state, parsed)!
                && (bool)Call("JamesPragmatismFlow", "TryCommitPending", state)!,
                "James's independent question commits " + action + ".");
        }
        Check((bool)Get(state, "Closed")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "James's independent question closes without spending the main question.");
    }

    private static void CheckWittgensteinTwoActs()
    {
        ModelDb.Inject(typeof(WittgensteinGame));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(AristotleCategoryTablet), 1])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["ARISTOTLE__LANGUAGE_AND_MEANING__CORE"])!;
        Put(journey, "RouteId", "ORDINARY_SPEECH");
        Put(journey, "LastFixedAct", 2);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);

        object Shared()
        {
            object?[] query = [run, null];
            Call("PhilosophyRunStateService", "TryGet", query);
            return query[1]!;
        }

        object?[] prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!,
            "Wittgenstein's early second-act room opens on the ordinary-speech line.");
        var earlyRoom = (EventRoom)prepare[1]!;
        Check(earlyRoom.CanonicalEvent is WittgensteinGame,
            "The early boundary selects Wittgenstein, not another room.");
        object early = Get(Shared(), "WittgensteinGame");
        Check(Convert.ToInt32(Get(early, "ActIndex")) == 2 && (bool)Get(early, "FixedMode"),
            "The early room records act two and fixed mode.");
        foreach (string action in new[] { "LanguageGame", "Leave" })
        {
            object parsed = Enum.Parse(T("WittgensteinGameAction"), action);
            Check((bool)Call("WittgensteinGameFlow", "TryPrepare", early, parsed)!
                && (bool)Call("WittgensteinGameFlow", "TryCommitPending", early, journey, graph)!,
                "The early Wittgenstein room commits " + action + ".");
        }
        Check((bool)Get(early, "Closed")
            && ((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains(
                "WESTERN_EDGE_152")
            && (string)Get(journey, "CurrentNodeId") == "LUDWIG_WITTGENSTEIN__LANGUAGE_AND_MEANING__CORE"
            && Convert.ToInt32(Get(journey, "LastFixedAct")) == 3,
            "The early room closes after adopting 152 and keeps its completed edge.");
        ((HashSet<ModelId>)run.VisitedEventIds).Add(earlyRoom.CanonicalEvent.Id);

        Set(run, "_currentActIndex", 2);
        object?[] late = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", late)!,
            "The same run opens the late Wittgenstein room after the early room closed.");
        var lateRoom = (EventRoom)late[1]!;
        Check(lateRoom.CanonicalEvent is WittgensteinGame,
            "The late boundary still selects Wittgenstein.");
        object lateState = Get(Shared(), "WittgensteinGame");
        Check(!ReferenceEquals(lateState, early)
            && Convert.ToInt32(Get(lateState, "ActIndex")) == 3
            && (bool)Get(lateState, "FixedMode")
            && (string)Get(lateState, "EdgeId") == string.Empty,
            "The late phase gets a new state instead of reusing the closed early record.");
        foreach (string action in new[] { "LanguageGame", "Leave" })
        {
            object parsed = Enum.Parse(T("WittgensteinGameAction"), action);
            Check((bool)Call("WittgensteinGameFlow", "TryPrepare", lateState, parsed)!
                && (bool)Call("WittgensteinGameFlow", "TryCommitPending", lateState, journey, graph)!,
                "The late Wittgenstein room commits " + action + ".");
        }
        Check((bool)Get(lateState, "Closed")
            && Convert.ToInt32(Get(journey, "LastFixedAct")) == 3
            && (string)Get(journey, "CurrentNodeId") == "LUDWIG_WITTGENSTEIN__LANGUAGE_AND_MEANING__CORE"
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(3),
            "The late room completes without spending the third-act question.");
    }

    private static void CheckSchopenhauerFixedCommit(RunState run, object shared, EventModel selected)
    {
        object state = Get(shared, "SchopenhauerWillDenial");
        object action = Enum.Parse(T("SchopenhauerWillDenialAction"), "BuyExtra");
        Check((bool)Call("SchopenhauerWillDenialFlow", "TryPrepare", state, action)!,
            "Schopenhauer's fixed room can prepare its visible option without Kant.");
        Check((bool)selected.GetType().GetMethod("CommitPending", Methods)!
                .Invoke(null, [shared, state])!,
            "Schopenhauer's event body commits a fixed-room option without a Kant record.");
        Check(Enum.GetName(T("SchopenhauerWillDenialAction"), Get(state, "PendingAction")) == "None",
            "Schopenhauer's fixed-room commit clears its pending choice.");
    }

    private static void CheckBoundary(Type relic, string node, Type[] expectedRooms, bool doctrine, int position, int actIndex = 1, string[]? required = null, string? route = null, ModelId? priorEvent = null, string? seed = null,
        bool routeRules = false, bool checkRoom = false, string thinker = "DESCARTES",
        string doctrineId = "DESCARTES_THINKING_LENS",
        Action<RunState, object, EventModel>? afterSelect = null)
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
            Put(doctrineState, "ThinkerId", thinker);
            Put(doctrineState, "DoctrineId", doctrineId);
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
        if (room.CanonicalEvent is LockeCargoIdentity)
        {
            object?[] lockeQuery = [run, null];
            Call("PhilosophyRunStateService", "TryGet", lockeQuery);
            object lockeState = Get(lockeQuery[1]!, "LockeCargoIdentity");
            Check(Get(lockeState, "Berkeley") is null,
                "A freshly created Locke record must not carry a nested Berkeley sub-record.");
        }
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object current = Get(query[1]!, expected!.Name);
        Check(current is not null && (bool)current.GetType().GetMethod("IsUsable")!.Invoke(current, null)!,
            "The boundary selection persists a usable room state for save recovery.");
        // The event body restores itself by matching its record's act, so a record
        // that names the wrong act is a room that opens locked. This is the fact
        // the body reads; assert it here where the boundary wrote it.
        Type openedType = room.CanonicalEvent.GetType();
        if (openedType == typeof(LockeCargoIdentity) || openedType == typeof(HumeInduction)
            || openedType == typeof(SchellingNature) || openedType == typeof(HegelRecognition)
            || openedType == typeof(MarxAlienation) || openedType == typeof(KantApperception)
            || openedType == typeof(LeviStraussMyth) || openedType == typeof(SaussureSign)
            || openedType == typeof(DerridaDeconstruction) || openedType == typeof(SmithSympathy)
            || openedType == typeof(MontesquieuBalance))
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

        afterSelect?.Invoke(run, query[1]!, room.CanonicalEvent);
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
