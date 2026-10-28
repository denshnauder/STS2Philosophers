using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// ENC130 native question entry.
internal static class LeibnizIndependentClocksPatch
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
        LeibnizHarmonyState? saved = shared.LeibnizHarmony;
        SpinozaCauseNetworkState? spinoza = shared.SpinozaCauseNetwork;

        IReadOnlyList<Player> players = run.Players;
        Player? player = players.Count == 1 ? players[0] : null;

        EventModel? canonical = null;
        try
        {
            canonical = ModelDb.Event<LeibnizIndependentClocks>();
        }
        catch (Exception ex)
        {
            Log.Error($"[ENC130] LeibnizIndependentClocks canonical event unavailable: {ex.Message}");
        }

        if (canonical is null) return null;
        if (run.VisitedEventIds.Contains(ModelDb.GetId<LeibnizIndependentClocks>()))
        {
            return null;
        }

        string id = LeibnizHarmonyState.BuildInstanceId(run.Rng.Seed);
        string spinozaId = $"SPINOZA_CAUSE_NETWORK:{run.Rng.Seed}:2";

        bool Consistent(LeibnizHarmonyState state) =>
            state.InstanceId == id
            && state.SpinozaInstanceId == spinozaId
            && journey is not null
            && spinoza is not null
            && LeibnizHarmonyFlow.IsRouteConsistent(state, journey, spinoza);

        bool predecessorUsable = spinoza?.IsUsable() == true;
        bool predecessorClosed = spinoza?.Closed == true;
        bool predecessorConsistent = spinoza is not null
            && spinoza.InstanceId == spinozaId
            && predecessorUsable
            && predecessorClosed
            && spinoza.EdgeCommitted
            && spinoza.Disposition is SpinozaDisposition.Intervened or SpinozaDisposition.Reordered
            && journey is not null
            && LeibnizHarmonyFlow.IsRouteConsistent(
                saved ?? LeibnizHarmonyState.Create(id, spinozaId), journey, spinoza);

        bool predecessorModelAvailable;
        try
        {
            _ = ModelDb.Event<SpinozaCauseNetwork>();
            predecessorModelAvailable = true;
        }
        catch (Exception ex)
        {
            Log.Error($"[ENC130] SpinozaCauseNetwork predecessor model unavailable: {ex.Message}");
            predecessorModelAvailable = false;
        }

        bool fixed3Unused = journey is not null
            && journey.LastFixedAct == 2
            && !journey.DeclinedFixedActs.Contains(3)
            && !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId);
        bool completionEdgeEligible = journey is not null
            && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == LeibnizHarmonyState.CompletionEdgeId);
        bool hasEdge024 = journey?.CompletedEdgeIds.Contains("WESTERN_EDGE_024") == true;
        bool leibnizSeen = journey?.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ") == true;
        bool hasResolvedLaterQuestions = journey is not null
            && (journey.ResolvedQuestionActs.Contains(2) || journey.ResolvedQuestionActs.Contains(3));

        LeibnizHarmonyTriggerContext context = new(
            RunSeed: run.Rng.Seed,
            RunInProgress: RunManager.Instance.IsInProgress,
            AtNativeQuestionEntry: NativeQuestionNodeEvents.IsEntry(run),
            IsSingleplayer: players.Count == 1,
            CurrentActIndex: run.CurrentActIndex,
            OwnsSoleDescartesThinkingLens: player?.GetRelicById(ModelDb.GetId<DescartesThinkingLens>())
                is DescartesThinkingLens { CanOfferNativeQuestion: true } lens
                && ReferenceEquals(lens.Owner, player),
            CurrentDoctrineId: shared.CurrentDoctrine?.ThinkerId ?? string.Empty,
            CurrentDoctrineLensId: shared.CurrentDoctrine?.DoctrineId ?? string.Empty,
            PredecessorUsable: predecessorUsable,
            PredecessorConsistent: predecessorConsistent,
            PredecessorClosed: predecessorClosed,
            PredecessorModelAvailable: predecessorModelAvailable,
            PredecessorInstanceId: spinoza?.InstanceId ?? string.Empty,
            CurrentNodeId: journey?.CurrentNodeId ?? string.Empty,
            LastFixedAct: journey?.LastFixedAct ?? 0,
            Fixed3Unused: fixed3Unused,
            CompletionEdgeEligible: completionEdgeEligible,
            InEligibleCombat: HasSecondActSpinozaAndLaterCombat(run),
            HasOpenRouteQuestion: WesternQuestionNodePolicy.HasOpenQuestion(shared),
            HasEdge024: hasEdge024,
            LeibnizSeen: leibnizSeen,
            HasResolvedLaterQuestions: hasResolvedLaterQuestions,
            RetainedEnding: journey?.RetainedEnding == true,
            HasSavedInstance: saved is not null,
            SavedStateUsable: saved?.IsUsable() == true,
            SavedStateConsistent: saved is not null && Consistent(saved),
            SavedStateClosed: saved?.Closed == true,
            SavedModelAvailable: canonical is not null,
            SavedInstanceId: saved?.InstanceId ?? string.Empty,
            SavedSpinozaInstanceId: saved?.SpinozaInstanceId ?? string.Empty);

        if (!LeibnizHarmonyTriggerPolicy.ShouldInsert(context))
        {
            return null;
        }

        LeibnizHarmonyState candidate = saved ?? LeibnizHarmonyState.Create(id, spinozaId);

        if (!Consistent(candidate))
        {
            return null;
        }

        shared.LeibnizHarmony = candidate;
        return canonical;
    }

    private static bool HasSecondActSpinozaAndLaterCombat(RunState run)
    {
        if (run.CurrentActIndex != 2 || !NativeQuestionNodeEvents.HasCompletedCombat(run)
            || run.MapPointHistory.Count <= 1) return false;
        ModelId predecessor = ModelDb.GetId<SpinozaCauseNetwork>();
        // A real third-act battle follows an actual second-act Spinoza entry.
        return run.MapPointHistory[1].Any(point => point.Rooms.Any(room => Equals(room.ModelId, predecessor))
            || point.PlayerStats.Any(stats => stats.EventChoices.Any(choice =>
                choice.Title.LocTable == "events" && choice.Title.LocEntryKey.StartsWith(
                    "SPINOZA_CAUSE_NETWORK.pages.", StringComparison.Ordinal))));
    }
}
