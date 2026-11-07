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

    public static Task Run()
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
        ModelDb.Inject(typeof(MillHarm));
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
            route: "INDIVIDUAL_COMMITMENT", required: ["WESTERN_EDGE_163"], checkRoom: true);
        CheckBoundary(typeof(DescartesThinkingLens), "MAURICE_MERLEAU_PONTY__SELF_AND_OTHER__CORE",
            [typeof(BeauvoirOther)], doctrine: false, position: 2, actIndex: 2,
            route: "EMBODIED_PERCEPTION", required: ["WESTERN_EDGE_195"], checkRoom: true);
        ModelDb.Inject(typeof(HabermasCommunication));
        CheckBoundary(typeof(DescartesThinkingLens), "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE",
            [typeof(HabermasCommunication)], doctrine: false, position: 2, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_084"], checkRoom: true);
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
        // Early first-act Bacon turn: Comte moves to the second act.
        CheckBoundary(typeof(RousseauUncarvedStone), "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE",
            [typeof(ComtePositivism)], doctrine: false, position: 2, actIndex: 1,
            route: "SOCIAL_KNOWLEDGE", required: ["WESTERN_EDGE_197"], checkRoom: true);
        ModelDb.Inject(typeof(MarxAlienation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE",
            [typeof(MarxAlienation)], doctrine: false, position: 2, route: "SOCIAL_CRITIQUE", checkRoom: true);
        CheckWittgensteinTwoActs();
        CheckJamesIndependentQuestion();
        CheckBerkeleyIndependentQuestion();
        CheckEarlySaussureContinuation();
        CheckEarlyNietzscheContinuation();
        CheckBenjaminIndependentQuestion();
        CheckLegacyNestedOptionsAreHidden();
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
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_177"], checkRoom: true);
        ModelDb.Inject(typeof(AdornoNegative));
        CheckBoundary(typeof(RousseauUncarvedStone), "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE",
            [typeof(AdornoNegative)], doctrine: false, position: 2, actIndex: 2,
            route: "SOCIAL_CRITIQUE", required: ["WESTERN_EDGE_178"], checkRoom: true);
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

    private static void CheckEarlySaussureContinuation()
    {
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
            "The Saussure turn opens Levi-Strauss at the real second-act boundary.");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        object state = Get(shared, "LeviStraussMyth");
        journey = Get(shared, "WesternJourney");
        Check((int)Get(state, "ActIndex") == 2 && (string)Get(state, "EdgeId") == "WESTERN_EDGE_187",
            "The early continuation uses its actual second-act edge and identity.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
            [run, run.Players[0], ((EventRoom)prepare[1]!).CanonicalEvent, shared, 1]);
        query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        shared = query[1]!;
        state = Get(shared, "LeviStraussMyth");
        journey = Get(shared, "WesternJourney");
        foreach (string name in new[] { "StructuralReading", "Leave" })
        {
            object action = Enum.Parse(T("LeviStraussMythAction"), name);
            Check((bool)Call("LeviStraussMythFlow", "TryPrepare", state, action)!
                && (bool)Call("LeviStraussMythFlow", "TryCommitPending", state, journey, graph)!,
                "The early continuation commits " + name + ".");
        }
        Check(((HashSet<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_187")
            && !((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Contains(2),
            "The continuation completes 187 without consuming a second-act question.");
        Set(run, "_currentActIndex", 2);
        prepare = [run, null];
        Check((bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!
            && prepare[1] is EventRoom { CanonicalEvent: DerridaDeconstruction },
            "The same turned run continues to Derrida at the third-act boundary.");
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
