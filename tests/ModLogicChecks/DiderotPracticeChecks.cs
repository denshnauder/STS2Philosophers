using STS2Philosophers;

internal static class DiderotPracticeChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        var state = DiderotPracticeState.Create("DIDEROT_PRACTICE:TEST:1");
        Check(DiderotPracticeFlow.Options(state.Stage).Count == 3,
            "Diderot must offer system, sample, or refusal.");
        Check(!DiderotPracticeFlow.TryPrepare(state, DiderotPracticeAction.Leave),
            "Diderot must not close before a verdict.");
        Check(DiderotPracticeFlow.TryPrepare(state, DiderotPracticeAction.CollectIntoSystem),
            "Collecting into the system must be selectable.");
        DiderotPracticeFlow.Apply(state, DiderotPracticeAction.CollectIntoSystem);
        Check(state.Conclusion == DiderotPracticeConclusion.System
            && state.Stage == DiderotPracticeStage.ResultSystem,
            "Collecting must record its conclusion.");
        Check(DiderotPracticeFlow.TryPrepare(state, DiderotPracticeAction.Leave),
            "A verdict page must close.");
        DiderotPracticeFlow.Apply(state, DiderotPracticeAction.Leave);
        Check(state.Closed && state.IsUsable(), "A closed segment must stay replayable.");

        var kept = DiderotPracticeState.Create("DIDEROT_PRACTICE:TEST:2");
        DiderotPracticeFlow.TryPrepare(kept, DiderotPracticeAction.KeepSample);
        DiderotPracticeFlow.Apply(kept, DiderotPracticeAction.KeepSample);
        Check(kept.Conclusion == DiderotPracticeConclusion.Sample, "Keeping must stay a sample.");
        var refused = DiderotPracticeState.Create("DIDEROT_PRACTICE:TEST:3");
        DiderotPracticeFlow.TryPrepare(refused, DiderotPracticeAction.RefuseSummary);
        DiderotPracticeFlow.Apply(refused, DiderotPracticeAction.RefuseSummary);
        Check(refused.Conclusion == DiderotPracticeConclusion.Refused, "Refusal must stay unclassified.");

        // Sol P1: a prepared action is persisted before it is applied, so a crash
        // leaves a legal pending action. Recovery applies exactly that action, and
        // the pending action must always be one of the page's own options.
        foreach (DiderotPracticeStage stage in Enum.GetValues<DiderotPracticeStage>())
        {
            var page = DiderotPracticeState.Create("DIDEROT_PRACTICE:PENDING:1");
            if (stage != DiderotPracticeStage.Invitation)
                DiderotPracticeFlow.Apply(page, DiderotPracticeAction.CollectIntoSystem);
            if (page.Stage != stage) continue;
            foreach (DiderotPracticeAction action in DiderotPracticeFlow.Options(stage))
            {
                var pending = DiderotPracticeState.Create("DIDEROT_PRACTICE:PENDING:2");
                if (stage != DiderotPracticeStage.Invitation)
                    DiderotPracticeFlow.Apply(pending, DiderotPracticeAction.CollectIntoSystem);
                Check(DiderotPracticeFlow.TryPrepare(pending, action),
                    $"The legal {action} choice must be preparable on {stage}.");
                var restored = System.Text.Json.JsonSerializer.Deserialize<DiderotPracticeState>(
                    System.Text.Json.JsonSerializer.Serialize(pending))!;
                Check(restored.IsUsable() && !restored.Closed
                    && restored.PendingAction == action
                    && DiderotPracticeFlow.Options(restored.Stage).Contains(restored.PendingAction),
                    "A pending segment action must survive a reload as a recoverable choice.");
                DiderotPracticeFlow.Apply(restored, restored.PendingAction);
                Check(restored.PendingAction == DiderotPracticeAction.None && restored.IsUsable(),
                    "Applying the recovered action must clear the pending slot.");
            }
        }

        var tampered = DiderotPracticeState.Create("DIDEROT_PRACTICE:TEST:4");
        tampered.Conclusion = DiderotPracticeConclusion.System;
        Check(!tampered.IsUsable(), "A conclusion without its action must be rejected.");
        tampered.Version = DiderotPracticeState.CurrentVersion + 1;
        Check(!tampered.IsUsable(), "An unknown version must be rejected.");

        var host = BaconSampleLedgerState.Create("BACON_SAMPLE_LEDGER:TEST:3", "OCKHAM:TEST");
        Check(host.Diderot is { Closed: false } && host.Diderot.IsUsable(),
            "A new Bacon record must carry an open Diderot segment.");
        host.Inspecting = true;
        host.Diderot!.Closed = true;
        Check(!host.IsUsable(), "Inspecting a closed Diderot segment must be rejected.");
    }
}
