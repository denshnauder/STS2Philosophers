using System.Globalization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace STS2Philosophers;

internal static class NativeQuestionNodeEvents
{
    internal static bool IsEntry(RunState run)
    {
        RunManager manager = RunManager.Instance;
        return manager.IsInProgress && manager.ShouldSave
            && ReferenceEquals(manager.DebugOnlyGetState(), run)
            && manager.NetService?.Type == NetGameType.Singleplayer
            && !run.IsGameOver && run.Players.Count == 1
            && !CombatManager.Instance.IsInProgress && run.CurrentRoomCount == 0
            && run.CurrentMapCoord is not null
            && run.CurrentMapPoint is { PointType: MapPointType.Unknown }
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) && shared is not null;
    }

    internal static bool HasCompletedCombat(RunState run, bool normalOnly = false) =>
        run.CurrentActIndex >= 0 && run.MapPointHistory.Count > run.CurrentActIndex
        && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
        && shared?.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex)?.CompletedCombatCount > 0
        && run.MapPointHistory[run.CurrentActIndex].Any(point => point.Rooms.Any(room =>
            room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Monster
            || !normalOnly && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Elite));

    internal static bool HasCompletedCombatAfter(RunState run, ModelId predecessor, string localizationRoot,
        bool normalOnly = false)
    {
        if (!HasCompletedCombat(run, normalOnly)) return false;
        var history = run.MapPointHistory[run.CurrentActIndex];
        int last = LastPredecessorIndex(history, predecessor, localizationRoot);
        // Legacy inserted events may have only native choice history, rather
        // than their own room record. Missing history cannot prove a later fight.
        return last >= 0 && history.Skip(last + 1).Any(point => IsCombatPoint(point, normalOnly));
    }

    // Third-act questions may depend on a second-act event (Hegel 025 needs
    // Leibniz 024). The current-act scan cannot see that history, so the
    // predecessor is looked up in earlier acts and any fight after it counts.
    // Wollstonecraft (040) and Machiavelli (046) compete for the same act-two
    // Fixed2 slot on Rousseau's history node. A fixed ?? order hid whichever came
    // second, so the pair is ordered from the persisted run seed: across runs both
    // stay reachable, within one run only one can open the slot.
    private static EventModel? TrySelectRousseauSecondAct(RunState run)
    {
        string seed = Convert.ToString(run.Rng.Seed, CultureInfo.InvariantCulture) ?? string.Empty;
        bool wollstonecraftFirst = seed.Length > 0 && (seed[^1] - '0') % 2 == 0;
        return wollstonecraftFirst
            ? WollstonecraftAdmissionPatch.TrySelectNative(run)
                ?? MachiavelliFloodPreparationPatch.TrySelectNative(run)
            : MachiavelliFloodPreparationPatch.TrySelectNative(run)
                ?? WollstonecraftAdmissionPatch.TrySelectNative(run);
    }

    internal static bool HasCompletedCombatAfterAcrossActs(RunState run, ModelId predecessor,
        string localizationRoot, bool normalOnly = false)
    {
        if (!HasCompletedCombat(run, normalOnly)) return false;
        var acts = new List<IReadOnlyList<CombatHistoryPoint>>();
        for (int act = 0; act <= run.CurrentActIndex && act < run.MapPointHistory.Count; act++)
        {
            acts.Add(run.MapPointHistory[act]
                .Select(point => new CombatHistoryPoint(
                    MatchesPredecessor(point, predecessor, localizationRoot),
                    IsCombatPoint(point, normalOnly)))
                .ToList());
        }
        return CrossActCombatHistory.HasCombatAfterPredecessor(acts, run.CurrentActIndex);
    }

    private static int LastPredecessorIndex(IReadOnlyList<MapPointHistoryEntry> history, ModelId predecessor,
        string localizationRoot)
    {
        int last = -1;
        for (int i = 0; i < history.Count; i++)
            if (MatchesPredecessor(history[i], predecessor, localizationRoot)) last = i;
        return last;
    }

    private static bool MatchesPredecessor(MapPointHistoryEntry point, ModelId predecessor,
        string localizationRoot) =>
        point.Rooms.Any(room => Equals(room.ModelId, predecessor))
        || point.PlayerStats.Any(stats => stats.EventChoices.Any(choice =>
            choice.Title.LocTable == "events" && choice.Title.LocEntryKey.StartsWith(
                localizationRoot + ".pages.", StringComparison.Ordinal)));

    private static bool IsCombatPoint(MapPointHistoryEntry point, bool normalOnly) =>
        point.Rooms.Any(room => room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Monster
            || !normalOnly && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Elite);

    private static bool IsDoctrineCarrier(RelicModel carrier) => carrier is WesternPracticeRelic
        or KongziMuduo or KongziQingYuPei or MengziXiongZhang or XunziShengMo
        or MoziMoSeZhuJian or MoziShouChengTu or LaoziWuWeiShuJian or LaoziShuiYu
        or QinGuliShouChengXie or ZhuangziDaHu or YangzhuQuanShengBi or HuishiLiWuChou;

    internal static EventModel ModifyNextEvent(RelicModel carrier, EventModel nextEvent)
    {
        if (!carrier.IsMutable || !IsDoctrineCarrier(carrier) || carrier.Owner?.RunState is not RunState run
            || !carrier.Owner.Relics.Any(relic => ReferenceEquals(relic, carrier))
            || carrier.Owner.Relics.Count(IsDoctrineCarrier) != 1
            || !IsEntry(run) || !ReferenceEquals(run.Players[0], carrier.Owner)
            || RouteEventMapBoundary.IsRegisteredEvent(nextEvent)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            return nextEvent;

        // A legacy business checkpoint may predate its room. Resume the exact
        // open question on a legal node without a factory, reroll, or new budget.
        EventModel? existing = ProtagorasSharedDeliberationPatch.TrySelectExistingNative(run)
            ?? ZenoCivicChallengePatch.TrySelectExistingNative(run)
            ?? SextusOpposedAppearancesPatch.TrySelectExistingNative(run)
            ?? ParmenidesInquiryPatch.TrySelectExistingNative(run)
            ?? AnaximanderOriginInquiryPatch.TrySelectExistingNative(run)
            ?? EmpedoclesRootsInquiryPatch.TrySelectExistingNative(run)
            ?? AnaxagorasMixtureInquiryPatch.TrySelectExistingNative(run)
            ?? GorgiasSpeechInquiryPatch.TrySelectExistingNative(run)
            ?? PyrrhoReportedCrossingPatch.TrySelectExistingNative(run)
            ?? AnselmUnfinishedProofPatch.TrySelectExistingNative(run)
            ?? OckhamNamesInquiryPatch.TrySelectExistingNative(run)
            ?? LutherSignedCarePatch.TrySelectExistingNative(run)
            ?? MachiavelliFloodPreparationPatch.TrySelectExistingNative(run)
            ?? WollstonecraftAdmissionPatch.TrySelectExistingNative(run)
            ?? MillHarmPatch.TrySelectExistingNative(run)
            ?? MontesquieuBalancePatch.TrySelectExistingNative(run)
            ?? SmithSympathyPatch.TrySelectExistingNative(run)
            ?? SchellingNaturePatch.TrySelectExistingNative(run)
            ?? KierkegaardFaithPatch.TrySelectExistingNative(run)
            ?? MarxAlienationPatch.TrySelectExistingNative(run)
            ?? NietzscheRevaluationPatch.TrySelectExistingNative(run)
            ?? BenthamUtilityPatch.TrySelectExistingNative(run)
            ?? ComtePositivismPatch.TrySelectExistingNative(run)
            ?? PeirceInquiryPatch.TrySelectExistingNative(run)
            ?? DeweyPublicExperimentPatch.TrySelectExistingNative(run)
            ?? HusserlEpochePatch.TrySelectExistingNative(run)
            ?? HeideggerDaseinPatch.TrySelectExistingNative(run)
            ?? SartreExistencePatch.TrySelectExistingNative(run)
            ?? MerleauPontyBodyPatch.TrySelectExistingNative(run)
            ?? BeauvoirOtherPatch.TrySelectExistingNative(run)
            ?? FichteSelfPositingPatch.TrySelectExistingNative(run)
            ?? LockeCargoIdentityPatch.TrySelectExistingNative(run)
            ?? HumeInductionPatch.TrySelectExistingNative(run)
            ?? KantApperceptionPatch.TrySelectExistingNative(run)
            ?? SchopenhauerWillDenialPatch.TrySelectExistingNative(run)
            ?? JamesForcedBridgePatch.TrySelectExistingNative(run)
            ?? CamusRevoltLimitPatch.TrySelectExistingNative(run)
            ?? CarnapFrameworkChoicePatch.TrySelectExistingNative(run)
            ?? AustinSpeechActPatch.TrySelectExistingNative(run)
            ?? PopperFalsificationPatch.TrySelectExistingNative(run)
            ?? RousseauConsentPatch.TrySelectExistingNative(run)
            ?? KantUniversalizationPatch.TrySelectExistingNative(run)
            ?? FoucaultPowerPatch.TrySelectExistingNative(run)
            ?? DeleuzeDifferencePatch.TrySelectExistingNative(run)
            ?? GadamerFusionPatch.TrySelectExistingNative(run)
            ?? RussellAnalysisPatch.TrySelectExistingNative(run)
            ?? WittgensteinGamePatch.TrySelectExistingNative(run)
            ?? SaussureSignPatch.TrySelectExistingNative(run)
            ?? LeviStraussMythPatch.TrySelectExistingNative(run)
            ?? DerridaDeconstructionPatch.TrySelectExistingNative(run)
            ?? HorkheimerReasonPatch.TrySelectExistingNative(run)
            ?? AdornoNegativePatch.TrySelectExistingNative(run)
            ?? HabermasCommunicationPatch.TrySelectExistingNative(run)
            ?? FreudUnconsciousPatch.TrySelectExistingNative(run)
            ?? LacanSignifierPatch.TrySelectExistingNative(run)
            ?? FregeSensePatch.TrySelectExistingNative(run);
        if (existing is not null) return existing;
        if (WesternQuestionNodePolicy.HasOpenQuestion(shared)) return nextEvent;

        // These slots keep their business identity, but share the native node.
        // Gaze has the same ModelId as the opening and uses continuation facts.
        EventModel? special = ActTwoPhilosophersGazePatch.TrySelectNative(run)
            ?? DiogenesStaySwitchEntrySession.TrySelectNative(run)
            ?? ZenoRouteNativeTriggerRegistry.TrySelectNative(run);
        if (special is not null) return special;
        EventModel? james = JamesForcedBridgePatch.TrySelectNative(run);
        if (james is not null) return james;

        bool fork = MapTravel.GetTravelablePointsFrom(run, run.CurrentMapPoint!).Take(2).Count() == 2;
        WesternQuestionKind kind = carrier is WesternPracticeRelic
            ? WesternQuestionNodePolicy.Select(shared, run.CurrentActIndex, carrier.Id.Entry, fork)
            : WesternQuestionKind.None;
        EventModel? Question(WesternQuestionKind expected)
        {
            if (kind != expected) return null;
            EventModel replacement;
            try { replacement = Canonical(kind); }
            catch (Exception exception)
            {
                Log.Error($"[STS2Philosophers] Native question model {kind} is unavailable; preserving the native event: {exception}");
                return null;
            }
            return !run.VisitedEventIds.Contains(replacement.Id)
                && WesternQuestionNodePolicy.TryStart(shared, run.CurrentActIndex, carrier.Id.Entry,
                    fork, run.Rng.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), kind)
                ? replacement : null;
        }

        // Preserve the existing route dispatch priority. Resolve eligibility only
        // until the first event is selected, so one node has one business claim.
        return Question(WesternQuestionKind.Protagoras)
            ?? Question(WesternQuestionKind.Zeno)
            ?? PlatoWholeArrangementPatch.TrySelectNative(run)
            ?? EpicurusSharedMarginPatch.TrySelectNative(run)
            ?? PlotinusCommonGroundPatch.TrySelectNative(run)
            ?? Question(WesternQuestionKind.Sextus)
            ?? DescartesMethodicReconstructionPatch.TrySelectNative(run)
            ?? LockeCargoIdentityPatch.TrySelectNative(run)
            ?? HumeInductionPatch.TrySelectNative(run)
            ?? KantApperceptionPatch.TrySelectNative(run)
            ?? SchopenhauerWillDenialPatch.TrySelectNative(run)
            ?? CamusRevoltLimitPatch.TrySelectNative(run)
            ?? CarnapFrameworkChoicePatch.TrySelectNative(run)
            ?? AustinSpeechActPatch.TrySelectNative(run)
            ?? PopperFalsificationPatch.TrySelectNative(run)
            ?? KantUniversalizationPatch.TrySelectNative(run)
            ?? MillHarmPatch.TrySelectNative(run) ?? MontesquieuBalancePatch.TrySelectNative(run) ?? SmithSympathyPatch.TrySelectNative(run) ?? SchellingNaturePatch.TrySelectNative(run) ?? KierkegaardFaithPatch.TrySelectNative(run) ?? MarxAlienationPatch.TrySelectNative(run) ?? NietzscheRevaluationPatch.TrySelectNative(run) ?? BenthamUtilityPatch.TrySelectNative(run) ?? ComtePositivismPatch.TrySelectNative(run) ?? PeirceInquiryPatch.TrySelectNative(run) ?? DeweyPublicExperimentPatch.TrySelectNative(run) ?? HusserlEpochePatch.TrySelectNative(run) ?? HeideggerDaseinPatch.TrySelectNative(run) ?? SartreExistencePatch.TrySelectNative(run) ?? MerleauPontyBodyPatch.TrySelectNative(run) ?? FichteSelfPositingPatch.TrySelectNative(run)
            ?? BeauvoirOtherPatch.TrySelectNative(run)
            ?? HanFeiPublicStandardPatch.TrySelectNative(run)
            ?? Question(WesternQuestionKind.Parmenides)
            ?? Question(WesternQuestionKind.Anaximander)
            ?? AnaximenesBreathInvitationPatch.TrySelectNative(run)
            ?? AugustinePromisePatch.TrySelectNative(run)
            ?? AristotleCivicInquiryPatch.TrySelectNative(run)
            ?? MachiavelliRealistChallengePatch.TrySelectNative(run)
            ?? AquinasBridgeNoticePatch.TrySelectNative(run)
            ?? DemocritusArrangementPatch.TrySelectNative(run)
            ?? Question(WesternQuestionKind.Empedocles)
            ?? Question(WesternQuestionKind.Anaxagoras)
            ?? Question(WesternQuestionKind.Gorgias)
            ?? SenecaDailyReviewPatch.TrySelectNative(run)
            ?? EpictetusGuidePracticePatch.TrySelectNative(run)
            ?? MarcusAureliusCampPracticePatch.TrySelectNative(run)
            ?? Question(WesternQuestionKind.Pyrrho)
            ?? Question(WesternQuestionKind.Anselm)
            ?? Question(WesternQuestionKind.Ockham)
            ?? TrySelectRousseauSecondAct(run)
            ?? Question(WesternQuestionKind.Luther)
            ?? RousseauConsentPatch.TrySelectNative(run)
            ?? BaconSampleLedgerPatch.TrySelectNative(run)
            ?? CopernicusShiftedCenterPatch.TrySelectNative(run)
            ?? GalileoMeasuredPracticePatch.TrySelectNative(run)
            ?? NewtonTwoSitesPatch.TrySelectNative(run)
            ?? SpinozaCauseNetworkPatch.TrySelectNative(run)
            ?? LeibnizMonadComparisonPatch.TrySelectNative(run)
            ?? HegelSubstanceSubjectPatch.TrySelectNative(run)
            ?? LeibnizIndependentClocksPatch.TrySelectNative(run)
            ?? HobbesAuthorizedSovereigntyPatch.TrySelectNative(run)
            ?? FoucaultPowerPatch.TrySelectNative(run)
            ?? DeleuzeDifferencePatch.TrySelectNative(run)
            ?? GadamerFusionPatch.TrySelectNative(run)
            ?? RussellAnalysisPatch.TrySelectNative(run)
            ?? WittgensteinGamePatch.TrySelectNative(run)
            ?? SaussureSignPatch.TrySelectNative(run)
            ?? LeviStraussMythPatch.TrySelectNative(run)
            ?? DerridaDeconstructionPatch.TrySelectNative(run)
            ?? HorkheimerReasonPatch.TrySelectNative(run)
            ?? AdornoNegativePatch.TrySelectNative(run)
            ?? HabermasCommunicationPatch.TrySelectNative(run)
            ?? FreudUnconsciousPatch.TrySelectNative(run)
            ?? LacanSignifierPatch.TrySelectNative(run)
            ?? FregeSensePatch.TrySelectNative(run)
            ?? nextEvent;
        // ActModel.PullNextEvent registers the returned model as visited. We do
        // not advance its event queue, append history, or consume another RNG roll.
    }

    private static EventModel Canonical(WesternQuestionKind kind) => kind switch
    {
        WesternQuestionKind.Protagoras => ModelDb.Event<ProtagorasSharedDeliberation>(),
        WesternQuestionKind.Parmenides => ModelDb.Event<ParmenidesInquiry>(),
        WesternQuestionKind.Sextus => ModelDb.Event<SextusOpposedAppearances>(),
        WesternQuestionKind.Gorgias => ModelDb.Event<GorgiasSpeechInquiry>(),
        WesternQuestionKind.Zeno => ModelDb.Event<ZenoCivicChallenge>(),
        WesternQuestionKind.Anaximander => ModelDb.Event<AnaximanderOriginInquiry>(),
        WesternQuestionKind.Empedocles => ModelDb.Event<EmpedoclesRootsInquiry>(),
        WesternQuestionKind.Anaxagoras => ModelDb.Event<AnaxagorasMixtureInquiry>(),
        WesternQuestionKind.Pyrrho => ModelDb.Event<PyrrhoReportedCrossing>(),
        WesternQuestionKind.Ockham => ModelDb.Event<OckhamNamesInquiry>(),
        WesternQuestionKind.Luther => ModelDb.Event<LutherSignedCare>(),
        WesternQuestionKind.Anselm => ModelDb.Event<AnselmUnfinishedProof>(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
