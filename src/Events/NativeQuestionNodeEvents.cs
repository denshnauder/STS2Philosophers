using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class NativeQuestionNodeEvents
{
    internal static EventModel ModifyNextEvent(WesternPracticeRelic carrier, EventModel nextEvent)
    {
        RunManager manager = RunManager.Instance;
        if (!carrier.CanOfferNativeQuestion || carrier.Owner?.RunState is not RunState run
            || !manager.IsInProgress || !manager.ShouldSave || !ReferenceEquals(manager.DebugOnlyGetState(), run)
            || manager.NetService?.Type != NetGameType.Singleplayer
            || run.IsGameOver || run.Players.Count != 1 || !ReferenceEquals(run.Players[0], carrier.Owner)
            || CombatManager.Instance.IsInProgress || run.CurrentRoomCount != 0
            || run.CurrentMapCoord is null || run.CurrentMapPoint is not { PointType: MapPointType.Unknown } point
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return nextEvent;

        bool fork = MapTravel.GetTravelablePointsFrom(run, point).Take(2).Count() == 2;
        WesternQuestionKind kind = WesternQuestionNodePolicy.Select(shared, run.CurrentActIndex, carrier.Id.Entry, fork);
        if (kind == WesternQuestionKind.None) return nextEvent;
        // Resolve and check the native identity before any question budget is spent.
        EventModel replacement;
        try { replacement = Canonical(kind); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Native question model {kind} is unavailable; preserving the native event: {exception}");
            return nextEvent;
        }
        if (run.VisitedEventIds.Contains(replacement.Id)
            || !WesternQuestionNodePolicy.TryStart(shared, run.CurrentActIndex, carrier.Id.Entry,
                fork, run.Rng.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), kind)) return nextEvent;
        // ActModel.PullNextEvent registers this returned event as visited. Do not
        // advance its event queue, append map history, or consume a second RNG roll.
        return replacement;
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
