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
            string adoptEdge = SpinozaCauseNetworkState.CompletionEdgeId;
            string sourceNode = SpinozaCauseNetworkState.SourceNodeId;
            string sourceThinker = "DESCARTES";
            if (journey is not null)
            {
                var arrival = WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                    .FirstOrDefault(edge => edge.Act == 2
                        && edge.ToNodeId == SpinozaCauseNetworkState.AdoptedNodeId);
                if (arrival is not null)
                {
                    adoptEdge = arrival.EdgeId;
                    sourceNode = arrival.FromNodeId;
                    sourceThinker = arrival.FromNodeId.Split("__")[0];
                }
            }
            string entryThinker = journey is null || journey.EntryNodeId.Length == 0
                ? sourceThinker
                : journey.EntryNodeId.Split("__")[0];
        SpinozaCauseNetworkTriggerContext context = new(
            RunInProgress: RunManager.Instance.IsInProgress,
            AtNativeQuestionEntry: NativeQuestionNodeEvents.IsEntry(run),
            IsSingleplayer: players.Count == 1,
            CurrentActIndex: run.CurrentActIndex,
            // Whichever line the run walks, it carries that line's practice relic and
            // its entry thinker's doctrine; both gates read those, not Descartes'.
            HasRequiredRelic: player?.Relics.Any(relic =>
                relic is WesternPracticeRelic { CanOfferNativeQuestion: true }) == true,
            HasSourceDoctrine: shared.CurrentDoctrine is { ThinkerId: var thinkerId }
                && thinkerId == entryThinker,
            HasOpenRouteQuestion: WesternQuestionNodePolicy.HasOpenQuestion(shared),
            CurrentNodeId: journey?.CurrentNodeId,
            LastFixedAct: journey?.LastFixedAct ?? 0,
            // The second act's fixed position is open at one (no first-act fixed
            // event) or two (the first act's event was taken); it is spent only
            // after a second-act fixed adoption or an explicit refusal.
            FixedActTwoResolved: journey?.LastFixedAct > 2
                || journey?.DeclinedFixedActs.Contains(2) == true,
            RetainedEnding: journey?.RetainedEnding == true,
            HasEligibleFixedEdge: journey is not null
                && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                    .Any(edge => edge.EdgeId == adoptEdge),
            HasCompletedActCombat: NativeQuestionNodeEvents.HasCompletedCombat(run),
            HasSeenSpinoza: journey?.SeenThinkerIds.Contains("BARUCH_SPINOZA") == true,
            HasSavedInstance: saved is not null,
            SavedInstanceUsable: saved?.IsUsable() == true,
            SavedInstanceClosed: saved?.Closed == true,
            SavedInstanceConsistent: saved is not null && Consistent(saved),
            ModelAvailable: canonical is not null,
            SourceNode: sourceNode);
        if (!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(context)) return null;
        SpinozaCauseNetworkState candidate = saved ?? SpinozaCauseNetworkState.Create(id);
        if (saved is null)
        {
            candidate.SourceNode = sourceNode;
            candidate.AdoptEdge = adoptEdge;
            candidate.SourceThinker = sourceThinker;
        }
        if (!Consistent(candidate)) return null;
        shared.SpinozaCauseNetwork = candidate;
        return canonical;
    }
}
