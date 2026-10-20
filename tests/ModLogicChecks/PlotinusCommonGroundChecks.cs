using System.Text.Json;
using STS2Philosophers;

internal static class PlotinusCommonGroundChecks
{
    private static void CheckLocalization()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                "content", "STS2Philosophers", "localization", language, "events.json")));
            JsonElement entries = document.RootElement;
            Check(entries.TryGetProperty("PLOTINUS_COMMON_GROUND.title", out _),
                $"{language} lacks the Plotinus title.");
            foreach (PlotinusGroundStage stage in Enum.GetValues<PlotinusGroundStage>())
            {
                string page = $"PLOTINUS_COMMON_GROUND.pages.{PlotinusCommonGroundFlow.StageKey(stage)}";
                Check(entries.TryGetProperty($"{page}.description", out _),
                    $"{language} lacks {stage} description.");
                foreach (PlotinusGroundAction action in PlotinusCommonGroundFlow.Options(stage))
                {
                    string option = $"{page}.options.{PlotinusCommonGroundFlow.ActionKey(action)}";
                    Check(entries.TryGetProperty($"{option}.title", out _)
                        && entries.TryGetProperty($"{option}.description", out _),
                        $"{language} lacks {stage}/{action} option copy.");
                }
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static (PhilosophyRunState, WesternRouteGraph) NewRun()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_008") && graph.TryApply(journey, "WESTERN_EDGE_009"),
            "Plato source fixture must complete its actual route.");
        return (new PhilosophyRunState
        {
            WesternJourney = journey,
            PlotinusCommonGround = PlotinusCommonGroundState.Create("PLOTINUS:fixture:3"),
        }, graph);
    }

    private static PhilosophyRunState RoundTrip(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static PhilosophyRunState Commit(PhilosophyRunState run, WesternRouteGraph graph, PlotinusGroundAction action)
    {
        PlotinusCommonGroundState state = run.PlotinusCommonGround!;
        Check(PlotinusCommonGroundFlow.TryPrepare(state, action), $"Can prepare {action}.");
        run = RoundTrip(run);
        state = run.PlotinusCommonGround!;
        Check(PlotinusCommonGroundFlow.TryCommitPending(state, run.WesternJourney!, graph), $"Can commit {action} after save.");
        run = RoundTrip(run);
        Check(PlotinusCommonGroundFlow.IsRouteConsistent(run.PlotinusCommonGround!, run.WesternJourney!),
            $"Route survives confirmed save after {action}.");
        return run;
    }

    public static void Run()
    {
        PlotinusGroundAction[][] paths =
        [
            [PlotinusGroundAction.DeclineInvitation],
            [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.ExamineCommon, PlotinusGroundAction.DeliverQualifiedMap],
            [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.ExamineCommon, PlotinusGroundAction.HandOffLocalGap],
            [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.RepairLocal, PlotinusGroundAction.DeliverLocalRevision],
            [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.RepairLocal, PlotinusGroundAction.PreserveVariants],
        ];
        foreach (PlotinusGroundAction[] path in paths)
        {
            (PhilosophyRunState run, WesternRouteGraph graph) = NewRun();
            foreach (PlotinusGroundAction action in path) run = Commit(run, graph, action);
            bool adopted = path[^1] is PlotinusGroundAction.DeliverQualifiedMap or PlotinusGroundAction.PreserveVariants;
            WesternJourneyState journey = run.WesternJourney!;
            Check(journey.LastFixedAct == 3 && run.PlotinusCommonGround!.FixedSlotResolved
                && run.PlotinusCommonGround.EdgeCommitted == adopted
                && journey.CompletedEdgeIds.Contains(PlotinusCommonGroundState.CompletionEdgeId) == adopted,
                "Exactly the terminal adoption choices commit the fixed edge.");
            Check(journey.CurrentNodeId == (adopted
                    ? "PLOTINUS__VIRTUE_AND_HAPPINESS__CORE"
                    : PlotinusCommonGroundState.SourceNodeId),
                "Adoption changes practice; local work or refusal retains Plato.");
            Check(!PlotinusCommonGroundFlow.TryPrepare(run.PlotinusCommonGround!, path[^1]),
                "A stale terminal button cannot resolve the slot again.");
            run = Commit(run, graph, PlotinusGroundAction.Leave);
            Check(run.PlotinusCommonGround!.Closed && !PlotinusCommonGroundFlow.TryPrepare(run.PlotinusCommonGround, PlotinusGroundAction.Leave),
                "Closing persists and a second LEAVE is inert.");
        }

        (PhilosophyRunState source, WesternRouteGraph sourceGraph) = NewRun();
        source = Commit(source, sourceGraph, PlotinusGroundAction.AcceptInvitation);
        source = Commit(source, sourceGraph, PlotinusGroundAction.ExamineCommon);
        PlotinusCommonGroundState active = source.PlotinusCommonGround!;
        Check(PlotinusCommonGroundFlow.TryPrepare(active, PlotinusGroundAction.DeliverQualifiedMap), "Can prepare route adoption.");
        PhilosophyRunState prepared = RoundTrip(source);
        Check(prepared.WesternJourney!.CurrentNodeId == PlotinusCommonGroundState.SourceNodeId,
            "Prepared save must not claim the new doctrine.");
        Check(PlotinusCommonGroundFlow.TryCommitPending(prepared.PlotinusCommonGround!, prepared.WesternJourney, sourceGraph),
            "Prepared choice can recover on reload.");
        Check(!PlotinusCommonGroundFlow.TryCommitPending(prepared.PlotinusCommonGround!, prepared.WesternJourney, sourceGraph),
            "A committed choice cannot replay.");

        (PhilosophyRunState invalid, WesternRouteGraph invalidGraph) = NewRun();
        invalid.PlotinusCommonGround!.Stage = PlotinusGroundStage.ResultQualifiedMap;
        Check(!invalid.PlotinusCommonGround.IsUsable()
            && !PlotinusCommonGroundFlow.TryCommitPending(invalid.PlotinusCommonGround, invalid.WesternJourney!, invalidGraph),
            "Malformed stage history cannot grant a route.");
        (PhilosophyRunState noEdge, WesternRouteGraph noEdgeGraph) = NewRun();
        noEdge.WesternJourney!.CompletedEdgeIds.Remove(PlotinusCommonGroundState.RequiredEdgeId);
        Check(!PlotinusCommonGroundFlow.IsRouteConsistent(noEdge.PlotinusCommonGround!, noEdge.WesternJourney),
            "Missing history makes the event route inconsistent.");
        Check(PlotinusCommonGroundFlow.TryPrepare(noEdge.PlotinusCommonGround!, PlotinusGroundAction.DeclineInvitation)
            && !PlotinusCommonGroundFlow.TryCommitPending(noEdge.PlotinusCommonGround!, noEdge.WesternJourney!, noEdgeGraph),
            "Even a stale prepared button cannot consume a fixed slot without its route history.");

        PlotinusCommonGroundTriggerContext eligible = new(true, true, true, 2, true, 2,
            PlotinusCommonGroundState.SourceNodeId, 2, true, true, true, true,
            false, false, false, false, true);
        Check(PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible), "Second combat after Epicurus permits the offer.");
        Check(!PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with { SourceIsCompletedNonBossCombat = false }),
            "Closing Epicurus on the same map cannot immediately chain the Plotinus event.");
        Check(!PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with { EpicurusClosed = false }),
            "Epicurus must be closed before Plotinus is considered.");
        Check(!PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with { CompletionEdgeEligible = false }),
            "A consumed fixed slot cannot be reused.");
        Check(!PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false }),
            "Missing route relic blocks the invitation.");
        Check(PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with {
            HasSavedInstance = true, SavedInstanceUsable = true, SavedRouteConsistent = true,
            SourceIsCompletedNonBossCombat = false, EpicurusClosed = false }),
            "An existing usable instance may resume without fabricating a fresh combat receipt.");
        Check(!PlotinusCommonGroundTriggerPolicy.ShouldInsert(eligible with {
            HasSavedInstance = true, SavedInstanceUsable = false, SavedRouteConsistent = true }),
            "Invalid saved state must never become a new invitation.");
        CheckLocalization();
        Console.WriteLine("Plotinus common ground checks passed: five paths, route effects, save replay, event priority and bilingual localization.");
    }
}
