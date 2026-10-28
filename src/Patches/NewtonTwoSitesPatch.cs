using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// ENC128 native question entry.
internal static class NewtonTwoSitesPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run))
        {
            return null;
        }

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
        {
            return null;
        }

        WesternJourneyState? journey = shared.WesternJourney;
        NewtonTwoSitesState? saved = shared.NewtonTwoSites;
        SextusOpposedAppearancesState? sextus = shared.SextusOpposedAppearances;
        DescartesMethodicReconstructionState? descartes = shared.DescartesMethodicReconstruction;

        IReadOnlyList<Player> players = run.Players;
        Player? player = players.Count == 1 ? players[0] : null;

        EventModel? canonical = null;
        try
        {
            canonical = ModelDb.Event<NewtonTwoSites>();
        }
        catch (Exception ex)
        {
            Log.Error($"[ENC128] NewtonTwoSites canonical event unavailable: {ex.Message}");
        }

        if (canonical is not null && run.VisitedEventIds.Contains(ModelDb.GetId<NewtonTwoSites>()))
        {
            return null;
        }

        string id = $"NEWTON_TWO_SITES:{run.Rng.Seed}:3";

        bool Consistent(NewtonTwoSitesState state) =>
            state.InstanceId == id
            && journey is not null
            && sextus is not null
            && descartes is not null
            && NewtonTwoSitesFlow.IsRouteConsistent(state, journey, sextus, descartes);

        NewtonTwoSitesTriggerContext context = new(
            RunInProgress: RunManager.Instance.IsInProgress,
            AtNativeQuestionEntry: NativeQuestionNodeEvents.IsEntry(run),
            IsSingleplayer: players.Count == 1,
            CurrentActIndex: run.CurrentActIndex,
            HasRequiredRelic: player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is SocratesQuestionCup { CanOfferNativeQuestion: true },
            HasValidKnowledgePredecessors: sextus is { Closed: true } && sextus.IsUsable()
                && descartes is { Closed: true } && descartes.IsUsable()
                && journey is not null
                && journey.ResolvedQuestionActs.Contains(1)
                && journey.DeclinedFixedActs.Contains(2),
            HasClosedRebuiltDescartes: descartes is { Closed: true, MethodCompleted: true, LimitedJudgmentRebuilt: true } && descartes.IsUsable(),
            HasPyrrhoInstance: shared.PyrrhoReportedCrossing is not null,
            HasAnselmInstance: shared.AnselmUnfinishedProof is not null,
            HasOpenRouteQuestion: WesternQuestionNodePolicy.HasOpenQuestion(shared),
            CurrentNodeId: journey?.CurrentNodeId,
            LastFixedAct: journey?.LastFixedAct ?? 0,
            FixedActThreeResolved: (journey?.LastFixedAct >= 3) || journey?.DeclinedFixedActs.Contains(3) == true,
            RetainedEnding: journey?.RetainedEnding == true,
            HasEligibleFixedEdge: journey is not null
                && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == NewtonTwoSitesState.CompletionEdgeId),
            HasCompletedCombatAfterDescartes: HasSecondActDescartesAndLaterCombat(run),
            HasSavedInstance: saved is not null,
            SavedInstanceUsable: saved?.IsUsable() == true,
            SavedInstanceClosed: saved?.Closed == true,
            SavedInstanceConsistent: saved is not null && Consistent(saved),
            ModelAvailable: canonical is not null);

        if (!NewtonTwoSitesTriggerPolicy.ShouldInsert(context))
        {
            return null;
        }

        NewtonTwoSitesState candidate = saved ?? NewtonTwoSitesState.Create(id, sextus!.InstanceId, descartes!.InstanceId);

        if (!Consistent(candidate))
        {
            return null;
        }

        shared.NewtonTwoSites = candidate;
        return canonical;
    }

    private static bool HasSecondActDescartesAndLaterCombat(RunState run)
    {
        if (run.CurrentActIndex != 2 || !NativeQuestionNodeEvents.HasCompletedCombat(run)
            || run.MapPointHistory.Count <= 1) return false;
        ModelId predecessor = ModelDb.GetId<DescartesMethodicReconstruction>();
        // A real third-act battle follows an actual second-act Descartes entry.
        return run.MapPointHistory[1].Any(point => point.Rooms.Any(room => Equals(room.ModelId, predecessor))
            || point.PlayerStats.Any(stats => stats.EventChoices.Any(choice =>
                choice.Title.LocTable == "events" && choice.Title.LocEntryKey.StartsWith(
                    "DESCARTES_METHODIC_RECONSTRUCTION.pages.", StringComparison.Ordinal))));
    }
}
