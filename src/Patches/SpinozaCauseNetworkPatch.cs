using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SpinozaCauseNetworkPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SpinozaCauseNetworkState? saved = shared.SpinozaCauseNetwork;
        IReadOnlyList<Player> players = run.Players;
        Player? player = players.Count == 1 ? players[0] : null;
        EventModel? canonical = null;
        try { canonical = ModelDb.Event<SpinozaCauseNetwork>(); }
        catch (Exception exception)
        {
            Log.Error($"[ENC129] SpinozaCauseNetwork event unavailable: {exception.Message}");
        }
        if (canonical is not null && run.VisitedEventIds.Contains(ModelDb.GetId<SpinozaCauseNetwork>()))
            return null;
        string id = $"SPINOZA_CAUSE_NETWORK:{run.Rng.Seed}:2";
        bool Consistent(SpinozaCauseNetworkState state) =>
            state.InstanceId == id && journey is not null
            && SpinozaCauseNetworkFlow.IsRouteConsistent(state, journey);
        SpinozaCauseNetworkTriggerContext context = new(
            RunInProgress: RunManager.Instance.IsInProgress,
            AtNativeQuestionEntry: NativeQuestionNodeEvents.IsEntry(run),
            IsSingleplayer: players.Count == 1,
            CurrentActIndex: run.CurrentActIndex,
            HasRequiredRelic: player?.GetRelicById(ModelDb.GetId<DescartesThinkingLens>())
                is DescartesThinkingLens { CanOfferNativeQuestion: true },
            HasSourceDoctrine: shared.CurrentDoctrine is
                { ThinkerId: "DESCARTES", DoctrineId: "DESCARTES_THINKING_LENS" },
            HasOpenRouteQuestion: WesternQuestionNodePolicy.HasOpenQuestion(shared),
            CurrentNodeId: journey?.CurrentNodeId,
            LastFixedAct: journey?.LastFixedAct ?? 0,
            FixedActTwoResolved: journey?.LastFixedAct >= 2
                || journey?.DeclinedFixedActs.Contains(2) == true,
            RetainedEnding: journey?.RetainedEnding == true,
            HasEligibleFixedEdge: journey is not null
                && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                    .Any(edge => edge.EdgeId == SpinozaCauseNetworkState.CompletionEdgeId),
            HasCompletedActCombat: NativeQuestionNodeEvents.HasCompletedCombat(run),
            HasSeenSpinoza: journey?.SeenThinkerIds.Contains("BARUCH_SPINOZA") == true,
            HasSavedInstance: saved is not null,
            SavedInstanceUsable: saved?.IsUsable() == true,
            SavedInstanceClosed: saved?.Closed == true,
            SavedInstanceConsistent: saved is not null && Consistent(saved),
            ModelAvailable: canonical is not null);
        if (!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(context)) return null;
        SpinozaCauseNetworkState candidate = saved ?? SpinozaCauseNetworkState.Create(id);
        if (!Consistent(candidate)) return null;
        shared.SpinozaCauseNetwork = candidate;
        return canonical;
    }
}
