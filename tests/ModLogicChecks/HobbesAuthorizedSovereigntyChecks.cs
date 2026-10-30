using System.Text.Json;
using STS2Philosophers;

internal static class HobbesAuthorizedSovereigntyChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph, bool completeQuestion = true)
    {
        WesternJourneyState journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_016"), "Aristotle 016 must be completed first.");
        if (completeQuestion)
        {
            Check(graph.TryApply(journey, "WESTERN_EDGE_017"), "The institutional question 017 must be completed first.");
        }
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            HobbesAuthorizedSovereignty = HobbesAuthorizedSovereigntyState.Create("HOBBES_AUTHORIZED_SOVEREIGNTY:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        HobbesSovereigntyAction action)
    {
        Check(HobbesAuthorizedSovereigntyFlow.TryPrepare(shared.HobbesAuthorizedSovereignty!, action),
            $"Prepare {action}.");
        shared = Reload(shared);
        HobbesAuthorizedSovereigntyState state = shared.HobbesAuthorizedSovereignty!;
        Check(HobbesAuthorizedSovereigntyFlow.TryCommitPending(state, shared.WesternJourney!, graph),
            $"Commit {action} after a saved preparation.");
        Check(!HobbesAuthorizedSovereigntyFlow.TryCommitPending(state, shared.WesternJourney!, graph),
            "A confirmed action cannot replay.");
        shared = Reload(shared);
        Check(HobbesAuthorizedSovereigntyFlow.IsRouteConsistent(shared.HobbesAuthorizedSovereignty!, shared.WesternJourney!),
            "Event and route survive reload together.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(HobbesAuthorizedSovereigntyFlow.IsRouteConsistent(shared.HobbesAuthorizedSovereignty!, shared.WesternJourney!),
            "The act-three fixed event is consistent after 016 and 017.");

        shared = Commit(shared, graph, HobbesSovereigntyAction.DelegateNightWatch);
        Check(shared.HobbesAuthorizedSovereignty!.Stage == HobbesSovereigntyStage.Announcement
            && shared.HobbesAuthorizedSovereignty.Scope == HobbesDelegatedScope.NightWatch
            && !shared.HobbesAuthorizedSovereignty.FixedSlotResolved,
            "Delegating the night watch opens the announcement without spending the fixed slot.");
        shared = Commit(shared, graph, HobbesSovereigntyAction.AcceptPublicRule);
        Check(shared.HobbesAuthorizedSovereignty!.Stage == HobbesSovereigntyStage.ResultAccepted
            && shared.HobbesAuthorizedSovereignty.EdgeCommitted
            && shared.HobbesAuthorizedSovereignty.FixedSlotResolved,
            "Accepting the public rule commits 018 and resolves the fixed slot.");
        Check(shared.WesternJourney!.LastFixedAct == 3
            && shared.WesternJourney.CurrentNodeId == HobbesAuthorizedSovereigntyState.AdoptedNodeId
            && shared.WesternJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_018")
            && graph.IsComplete(shared.WesternJourney),
            "Acceptance enters the terminal Hobbes node with 018 completed.");
        shared = Commit(shared, graph, HobbesSovereigntyAction.Leave);
        Check(shared.HobbesAuthorizedSovereignty!.Closed, "The accepted event closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, HobbesSovereigntyAction.DelegatePassageList);
        shared = Commit(shared, graph, HobbesSovereigntyAction.RequestException);
        Check(shared.HobbesAuthorizedSovereignty!.Stage == HobbesSovereigntyStage.ResultException
            && shared.HobbesAuthorizedSovereignty.RuleRevised
            && shared.HobbesAuthorizedSovereignty.EdgeCommitted
            && shared.WesternJourney!.CompletedEdgeIds.Contains("WESTERN_EDGE_018"),
            "A public exception still commits 018 and records the rule revision.");
        shared = Commit(shared, graph, HobbesSovereigntyAction.Leave);
        Check(shared.HobbesAuthorizedSovereignty!.Closed, "The exception result closes.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, HobbesSovereigntyAction.KeepBoth);
        Check(shared.HobbesAuthorizedSovereignty!.Stage == HobbesSovereigntyStage.ResultKept
            && !shared.HobbesAuthorizedSovereignty.EdgeCommitted
            && shared.HobbesAuthorizedSovereignty.FixedSlotResolved
            && shared.WesternJourney!.LastFixedAct == 3
            && shared.WesternJourney.CurrentNodeId == HobbesAuthorizedSovereigntyState.SourceNodeId
            && shared.WesternJourney.DeclinedFixedActs.Contains(3)
            && !shared.WesternJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_018")
            && graph.CanFinish(shared.WesternJourney),
            "Keeping both declines the fixed slot without entering Hobbes.");
        shared = Commit(shared, graph, HobbesSovereigntyAction.Leave);
        Check(shared.HobbesAuthorizedSovereignty!.Closed, "The kept result closes.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, HobbesSovereigntyAction.DelegateNightWatch);
        shared = Commit(shared, graph, HobbesSovereigntyAction.WithdrawCooperation);
        Check(shared.HobbesAuthorizedSovereignty!.Stage == HobbesSovereigntyStage.ResultWithdrawn
            && shared.HobbesAuthorizedSovereignty.PublicProtectionLost
            && !shared.HobbesAuthorizedSovereignty.EdgeCommitted
            && shared.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !shared.WesternJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_018"),
            "Withdrawal loses public protection and declines 018.");
        shared = Commit(shared, graph, HobbesSovereigntyAction.Leave);
        Check(shared.HobbesAuthorizedSovereignty!.Closed, "The withdrawn result closes.");

        shared = Fixture(graph, completeQuestion: false);
        Check(!HobbesAuthorizedSovereigntyFlow.IsRouteConsistent(shared.HobbesAuthorizedSovereignty!, shared.WesternJourney!),
            "Without 017 the Hobbes event is not route-consistent.");
        Check(HobbesAuthorizedSovereigntyFlow.TryPrepare(shared.HobbesAuthorizedSovereignty!, HobbesSovereigntyAction.KeepBoth),
            "Preparation itself does not bypass the missing 017 gate.");
        Check(!HobbesAuthorizedSovereigntyFlow.TryCommitPending(shared.HobbesAuthorizedSovereignty!,
            shared.WesternJourney!, graph), "A missing 017 gate must reject the commit.");

        Check(!new HobbesAuthorizedSovereigntyState { Version = 0 }.IsUsable(), "Version zero is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            Stage = (HobbesSovereigntyStage)99,
        }.IsUsable(), "An undefined stage is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            PendingAction = (HobbesSovereigntyAction)99,
        }.IsUsable(), "An undefined pending action is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            ConfirmedActions = null!,
        }.IsUsable(), "A null action list is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            Scope = (HobbesDelegatedScope)99,
        }.IsUsable(), "An undefined delegated scope is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            EdgeCommitted = true,
        }.IsUsable(), "A forged edge commitment without actions is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            RuleRevised = true,
        }.IsUsable(), "A forged public revision without actions is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            Scope = HobbesDelegatedScope.PassageList,
        }.IsUsable(), "A forged scope without actions is unusable.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            Closed = true,
            PendingAction = HobbesSovereigntyAction.Leave,
        }.IsUsable(), "A closed state cannot keep a pending action.");
        Check(!new HobbesAuthorizedSovereigntyState
        {
            InstanceId = "x",
            ConfirmedActions = [HobbesSovereigntyAction.AcceptPublicRule],
        }.IsUsable(), "An action outside the invitation options is unusable.");

        HobbesAuthorizedSovereigntyTriggerContext eligible = new(
            true, true, true, 2, true, 1, HobbesAuthorizedSovereigntyState.SourceNodeId, 2,
            true, true, true, false, false, false, false, true);
        Check(HobbesAuthorizedSovereigntyTriggerPolicy.ShouldInsert(eligible),
            "The Hobbes event is offered after a real act-three combat with 017 completed.");
        foreach (HobbesAuthorizedSovereigntyTriggerContext blocked in new[]
        {
            eligible with { RunInProgress = false },
            eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false },
            eligible with { CurrentActIndex = 1 },
            eligible with { SourceIsCompletedNonBossCombat = false },
            eligible with { CompletedCombatCount = 0 },
            eligible with { CurrentNodeId = "ARISTOTLE__BEING_AND_CHANGE__CORE" },
            eligible with { LastFixedAct = 1 },
            eligible with { LastFixedAct = 1 },
            eligible with { RequiredEdgeCompleted = false },
            eligible with { CompletionEdgeEligible = false },
            eligible with { AristotleClosed = false },
            eligible with { ModelAvailable = false },
        })
        {
            Check(!HobbesAuthorizedSovereigntyTriggerPolicy.ShouldInsert(blocked),
                $"Blocked context must not offer Hobbes: {blocked}");
        }
        Check(HobbesAuthorizedSovereigntyTriggerPolicy.ShouldInsert(new(
                true, true, true, 2, false, 0, null, 0, false, false, false,
                true, true, false, true, true)),
            "A usable saved instance resumes without a fresh offer boundary.");
        Check(!HobbesAuthorizedSovereigntyTriggerPolicy.ShouldInsert(new(
                true, true, true, 2, false, 0, null, 0, false, false, false,
                true, true, true, true, true)),
            "A closed saved instance must not resume.");

        string[] keys =
        [
            "HOBBES_AUTHORIZED_SOVEREIGNTY.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT_NIGHT_WATCH.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT_PASSAGE_LIST.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_KEPT.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_ACCEPTED.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_EXCEPTION.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_WITHDRAWN.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.DELEGATE_NIGHT_WATCH.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.DELEGATE_NIGHT_WATCH.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.DELEGATE_PASSAGE_LIST.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.DELEGATE_PASSAGE_LIST.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.KEEP_BOTH.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.INVITATION.options.KEEP_BOTH.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.ACCEPT_PUBLIC_RULE.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.ACCEPT_PUBLIC_RULE.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.REQUEST_EXCEPTION.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.REQUEST_EXCEPTION.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.WITHDRAW_COOPERATION.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.ANNOUNCEMENT.options.WITHDRAW_COOPERATION.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_KEPT.options.LEAVE.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_KEPT.options.LEAVE.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_ACCEPTED.options.LEAVE.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_ACCEPTED.options.LEAVE.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_EXCEPTION.options.LEAVE.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_EXCEPTION.options.LEAVE.description",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_WITHDRAWN.options.LEAVE.title",
            "HOBBES_AUTHORIZED_SOVEREIGNTY.pages.RESULT_WITHDRAWN.options.LEAVE.description",
        ];
        foreach (string lang in new[] { "eng", "zhs" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{lang}/events.json"));
            foreach (string key in keys)
            {
                Check(localization.RootElement.TryGetProperty(key, out JsonElement value)
                    && !string.IsNullOrWhiteSpace(value.GetString()), $"{lang} must localize {key}.");
            }
        }
    }
}
