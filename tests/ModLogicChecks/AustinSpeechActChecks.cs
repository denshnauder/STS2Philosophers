using System.Text.Json;
using STS2Philosophers;

internal static class AustinSpeechActChecks
{
    internal static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState NewJourney()
        {
            var j = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
            Check(graph.TryDecline(j, "Fixed", 2), "Austin fixture declines the second fixed slot.");
            Check(graph.TryReserveQuestion(j, 2), "Austin fixture resolves Ockham's second-act question.");
            return j;
        }

        var journey = NewJourney();
        var state = AustinSpeechActState.Create("AUSTIN_SPEECH_ACT:TEST:2");
        Check(AustinSpeechActFlow.IsRouteConsistent(state, journey),
            "Austin opens at the language handover station after Ockham resolves question 2.");
        Check(AustinSpeechActFlow.TryPrepare(state, AustinSpeechActAction.CheckAuthorization)
            && AustinSpeechActFlow.TryCommitPending(state, journey)
            && state.Stage == AustinSpeechActStage.Station && state.CheckCount == 1,
            "Austin records the first verified condition without transferring the key.");
        Check(AustinSpeechActFlow.TryPrepare(state, AustinSpeechActAction.CheckPresence)
            && AustinSpeechActFlow.TryCommitPending(state, journey)
            && state.Stage == AustinSpeechActStage.Evidence && state.CheckCount == 2
            && state.UnauthorizedSignatureDiscovered,
            "Austin's second check exposes the unauthorized signature and enters evidence.");
        Check(!AustinSpeechActFlow.TryPrepare(state, AustinSpeechActAction.CheckProcedure),
            "Austin cannot verify a third condition after the evidence appears.");
        Check(AustinSpeechActFlow.TryPrepare(state, AustinSpeechActAction.CompleteProcedure)
            && AustinSpeechActFlow.TryCommitPending(state, journey)
            && state.Outcome == AustinSpeechActOutcome.Completed && state.PriorityPassSpent,
            "Austin completion spends the priority passage before the key is effective.");
        Check(AustinSpeechActFlow.TryPrepare(state, AustinSpeechActAction.Leave)
            && AustinSpeechActFlow.TryCommitPending(state, journey) && state.Closed,
            "Austin completed result closes.");

        var withdrawnJourney = NewJourney();
        var withdrawn = AustinSpeechActState.Create("AUSTIN_SPEECH_ACT:TEST:2");
        Check(AustinSpeechActFlow.TryPrepare(withdrawn, AustinSpeechActAction.CheckProcedure)
            && AustinSpeechActFlow.TryCommitPending(withdrawn, withdrawnJourney)
            && AustinSpeechActFlow.TryPrepare(withdrawn, AustinSpeechActAction.CheckAuthorization)
            && AustinSpeechActFlow.TryCommitPending(withdrawn, withdrawnJourney)
            && AustinSpeechActFlow.TryPrepare(withdrawn, AustinSpeechActAction.Withdraw)
            && AustinSpeechActFlow.TryCommitPending(withdrawn, withdrawnJourney)
            && withdrawn.Outcome == AustinSpeechActOutcome.Withdrawn && withdrawn.Cost == 10,
            "Austin withdrawal spends 10 gold on a lawful handover officer.");
        withdrawn.CostAttempted = true;
        withdrawn.CostPaid = true;
        string encoded = JsonSerializer.Serialize(withdrawn);
        AustinSpeechActState reloaded = JsonSerializer.Deserialize<AustinSpeechActState>(encoded)!;
        reloaded.NormalizeAfterLoad();
        Check(reloaded.IsUsable() && AustinSpeechActFlow.IsRouteConsistent(reloaded, withdrawnJourney),
            "Austin withdrawal state reloads at the same station.");

        var pendingJourney = NewJourney();
        var pending = AustinSpeechActState.Create("AUSTIN_SPEECH_ACT:TEST:2");
        Check(AustinSpeechActFlow.TryPrepare(pending, AustinSpeechActAction.CheckPresence)
            && AustinSpeechActFlow.TryCommitPending(pending, pendingJourney)
            && AustinSpeechActFlow.TryPrepare(pending, AustinSpeechActAction.CheckProcedure)
            && AustinSpeechActFlow.TryCommitPending(pending, pendingJourney)
            && AustinSpeechActFlow.TryPrepare(pending, AustinSpeechActAction.RecordPromise)
            && AustinSpeechActFlow.TryCommitPending(pending, pendingJourney)
            && pending.Outcome == AustinSpeechActOutcome.Pending && pending.PendingPromise
            && pending.Cost == 0,
            "Austin records an unfulfilled promise without granting key or passage access.");

        var before = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
        var early = AustinSpeechActState.Create("AUSTIN_SPEECH_ACT:TEST:2");
        Check(!AustinSpeechActFlow.IsRouteConsistent(early, before),
            "Austin cannot open before Ockham's question 2 is resolved.");

        string[] keys =
        [
            "AUSTIN_SPEECH_ACT.title",
            "AUSTIN_SPEECH_ACT.pages.STATION.description",
            "AUSTIN_SPEECH_ACT.pages.STATION_CHECKED.description",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_AUTHORIZATION.title",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_AUTHORIZATION.description",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_PRESENCE.title",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_PRESENCE.description",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_PROCEDURE.title",
            "AUSTIN_SPEECH_ACT.pages.STATION.options.CHECK_PROCEDURE.description",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.description",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.COMPLETE_PROCEDURE.title",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.COMPLETE_PROCEDURE.description",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.WITHDRAW.title",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.WITHDRAW.description",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.RECORD_PROMISE.title",
            "AUSTIN_SPEECH_ACT.pages.EVIDENCE.options.RECORD_PROMISE.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_COMPLETED.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_COMPLETED.options.LEAVE.title",
            "AUSTIN_SPEECH_ACT.pages.RESULT_COMPLETED.options.LEAVE.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_WITHDRAWN.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_WITHDRAWN.options.LEAVE.title",
            "AUSTIN_SPEECH_ACT.pages.RESULT_WITHDRAWN.options.LEAVE.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_PENDING.description",
            "AUSTIN_SPEECH_ACT.pages.RESULT_PENDING.options.LEAVE.title",
            "AUSTIN_SPEECH_ACT.pages.RESULT_PENDING.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"Austin localization is missing {language}/{key}.");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
