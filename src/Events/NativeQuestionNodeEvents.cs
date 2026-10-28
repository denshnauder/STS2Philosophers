using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

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
        int last = -1;
        for (int i = 0; i < history.Count; i++)
            if (history[i].Rooms.Any(room => Equals(room.ModelId, predecessor))
                || history[i].PlayerStats.Any(stats => stats.EventChoices.Any(choice =>
                    choice.Title.LocTable == "events" && choice.Title.LocEntryKey.StartsWith(
                        localizationRoot + ".pages.", StringComparison.Ordinal)))) last = i;
        // Legacy inserted events may have only native choice history, rather
        // than their own room record. Missing history cannot prove a later fight.
        return last >= 0 && history.Skip(last + 1).Any(point => point.Rooms.Any(room =>
            room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Monster
            || !normalOnly && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Elite));
    }

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
            ?? BeauvoirOtherPatch.TrySelectExistingNative(run)
            ?? FichteSelfPositingPatch.TrySelectExistingNative(run)
            ?? LockeCargoIdentityPatch.TrySelectExistingNative(run)
            ?? HumeInductionPatch.TrySelectExistingNative(run)
            ?? KantApperceptionPatch.TrySelectExistingNative(run)
            ?? SchopenhauerWillDenialPatch.TrySelectExistingNative(run)
            ?? JamesForcedBridgePatch.TrySelectExistingNative(run)
            ?? CamusRevoltLimitPatch.TrySelectExistingNative(run)
            ?? CarnapFrameworkChoicePatch.TrySelectExistingNative(run)
            ?? RousseauConsentPatch.TrySelectExistingNative(run)
            ?? KantUniversalizationPatch.TrySelectExistingNative(run);
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
            ?? KantUniversalizationPatch.TrySelectNative(run)
            ?? MillHarmPatch.TrySelectNative(run) ?? FichteSelfPositingPatch.TrySelectNative(run)
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
            ?? MachiavelliFloodPreparationPatch.TrySelectNative(run)
            ?? WollstonecraftAdmissionPatch.TrySelectNative(run)
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
