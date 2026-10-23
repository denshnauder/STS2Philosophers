using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class GalileoMeasuredPracticeChecks
{
    private sealed record Fixture(GalileoMeasuredPracticeState State, WesternJourneyState Journey,
        DemocritusArrangementState Democritus, EmpedoclesRootsInquiryState Empedocles);
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static T Reload<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static string Journey(Fixture fixture) => JsonSerializer.Serialize(fixture.Journey);
    private static bool Consistent(Fixture fixture) => GalileoMeasuredPracticeFlow.IsRouteConsistent(
        fixture.State, fixture.Journey, fixture.Democritus, fixture.Empedocles);

    private static Fixture Start(int outcome)
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        var journey = graph.Start("HERACLITUS__BEING_AND_CHANGE__CORE");
        Check(graph.TryApply(journey, DemocritusArrangementState.PrerequisiteEdgeId), "The real Parmenides challenge precedes Democritus.");
        var democritus = DemocritusArrangementState.Create("DEMOCRITUS:GALILEO:2");
        foreach (var action in new[] { DemocritusArrangementAction.AcceptInvitation, DemocritusArrangementAction.StudyMotion,
            DemocritusArrangementAction.LeaveGap, DemocritusArrangementAction.AdoptAtoms, DemocritusArrangementAction.Leave })
            Check(DemocritusArrangementFlow.TryPrepare(democritus, action)
                && DemocritusArrangementFlow.TryCommitPending(democritus, journey, graph), "The actual atomist Fixed event adopts and closes.");
        Check(graph.TryReserveQuestion(journey, 2), "The real Empedocles question spends Q2.");
        var empedocles = EmpedoclesRootsInquiryState.Create("EMPEDOCLES:GALILEO:2");
        EmpedoclesRootsInquiryAction[] path = outcome switch
        {
            2 => [EmpedoclesRootsInquiryAction.AcceptInvitation, EmpedoclesRootsInquiryAction.StopInquiry, EmpedoclesRootsInquiryAction.Leave],
            3 => [EmpedoclesRootsInquiryAction.DeclineInvitation, EmpedoclesRootsInquiryAction.Leave],
            _ => [EmpedoclesRootsInquiryAction.AcceptInvitation, EmpedoclesRootsInquiryAction.StudyComposition,
                EmpedoclesRootsInquiryAction.CombineDifferent, outcome == 0 ? EmpedoclesRootsInquiryAction.RecordComparison
                    : EmpedoclesRootsInquiryAction.KeepAtoms, EmpedoclesRootsInquiryAction.Leave],
        };
        foreach (var action in path)
            Check(EmpedoclesRootsInquiryFlow.TryPrepare(empedocles, action)
                && EmpedoclesRootsInquiryFlow.TryCommitPending(empedocles, journey, graph), "The actual second question closes with its chosen outcome.");
        return Reload(new Fixture(GalileoMeasuredPracticeState.Create("GALILEO:TEST:3", democritus.InstanceId,
            empedocles.InstanceId), journey, democritus, empedocles));
    }

    private static Fixture Commit(Fixture fixture, GalileoMeasuredPracticeAction action)
    {
        string before = Journey(fixture);
        Check(Consistent(fixture) && GalileoMeasuredPracticeFlow.TryPrepare(fixture.State, action)
            && !GalileoMeasuredPracticeFlow.TryPrepare(fixture.State, action), $"Exactly one operation prepares: {action}.");
        fixture = Reload(fixture);
        Check(fixture.State.IsUsable() && fixture.State.PendingAction == action && Consistent(fixture),
            "The prepared operation survives serialization without becoming confirmed.");
        Check(GalileoMeasuredPracticeFlow.TryCommitPending(fixture.State, fixture.Journey, fixture.Democritus, fixture.Empedocles)
            && !GalileoMeasuredPracticeFlow.TryCommitPending(fixture.State, fixture.Journey, fixture.Democritus, fixture.Empedocles),
            "A saved pending action commits once, never twice.");
        fixture = Reload(fixture);
        Check(Consistent(fixture) && before == Journey(fixture), "Practice cannot consume Fixed/Q, change a graph edge, or replace the doctrine.");
        return fixture;
    }

    private static IEnumerable<GalileoMeasuredPracticeAction[]> Paths()
    {
        yield return [GalileoMeasuredPracticeAction.DeclineInvitation, GalileoMeasuredPracticeAction.Leave];
        foreach (var first in new[] { GalileoMeasuredPracticeAction.ShortFirst, GalileoMeasuredPracticeAction.LongFirst })
        {
            GalileoMeasuredPracticeAction[] start = [GalileoMeasuredPracticeAction.AcceptInvitation, first,
                GalileoMeasuredPracticeAction.TakeRemaining];
            yield return [.. start, GalileoMeasuredPracticeAction.PublishRaw, GalileoMeasuredPracticeAction.Leave];
            foreach (var final in new[] { GalileoMeasuredPracticeAction.AdoptProtocol, GalileoMeasuredPracticeAction.KeepOld })
                yield return [.. start, GalileoMeasuredPracticeAction.CheckClock, GalileoMeasuredPracticeAction.RepeatCorrected,
                    final, GalileoMeasuredPracticeAction.Leave];
        }
    }

    public static void Run()
    {
        int paths = 0;
        foreach (int outcome in new[] { 0, 1, 2 })
        foreach (var path in Paths())
        {
            var fixture = Start(outcome);
            Check(Consistent(fixture), "Galileo uses actual predecessors without requiring a Copernicus record or fork.");
            foreach (var action in path)
            {
                Check(!fixture.State.Adopted || !fixture.State.Closed, "No grant is complete before the final closing action.");
                if (fixture.State.Stage is not GalileoMeasuredPracticeStage.Decision)
                    Check(!GalileoMeasuredPracticeFlow.TryPrepare(fixture.State, GalileoMeasuredPracticeAction.AdoptProtocol),
                        "Neither raw readings nor merely checking the zero point can authorize adoption.");
                fixture = Commit(fixture, action);
                if (action is GalileoMeasuredPracticeAction.ShortFirst or GalileoMeasuredPracticeAction.LongFirst)
                {
                    var reading = fixture.State.Measurements.Single();
                    bool shortFirst = action == GalileoMeasuredPracticeAction.ShortFirst;
                    Check(reading.Distance == (shortFirst ? 1 : 4) && reading.RawTicks == (shortFirst ? 3 : 5)
                        && reading.CorrectedTicks is null, "The first actual observation alone records its original, uncorrected reading.");
                }
                if (action == GalileoMeasuredPracticeAction.CheckClock)
                    Check(fixture.State.StationaryTicks == 1 && fixture.State.Measurements.All(r => r.CorrectedTicks is null),
                        "Checking the stationary clock does not fabricate repeat measurements.");
                if (action == GalileoMeasuredPracticeAction.RepeatCorrected)
                    Check(fixture.State.Measurements.All(r => r.RawTicks == (r.Segment == GalileoMeasuredSegment.Short ? 3 : 5)
                        && r.CorrectedTicks == (r.Segment == GalileoMeasuredSegment.Short ? 2 : 4)),
                        "Only the repeat action adds corrected observations while preserving all originals.");
                if (action == GalileoMeasuredPracticeAction.AdoptProtocol)
                    Check(fixture.State.Adopted && !fixture.State.Closed
                        && GalileoMeasuredPracticeFlow.DescriptionKey(fixture.State) == "RESULT_ADOPTED",
                        "The adopted result before Leave is still pending its real relic grant and native save.");
            }
            Check(fixture.State.Closed && fixture.State.Adopted == path.Contains(GalileoMeasuredPracticeAction.AdoptProtocol)
                && !GalileoMeasuredPracticeFlow.TryPrepare(fixture.State, GalileoMeasuredPracticeAction.Leave),
                "All seven endings close; only genuine calibrated adoption can enable a later native grant.");
            Check(GalileoMeasuredPracticeFlow.DescriptionKey(fixture.State)
                == (fixture.State.Adopted ? "RESULT_ADOPTED_CONFIRMED" : GalileoMeasuredPracticeFlow.StageKey(fixture.State.Stage)),
                "Only a closed adopted result uses the confirmed grant page.");
            paths++;
        }
        Check(paths == 21, "Seven real practice paths run against three accepted Empedocles histories.");
        InvalidEventData(); Payments();
        Console.WriteLine("Galileo pure checks passed: 21 serialized practice paths, original/zero/repeated readings, calibrated closing gate, unchanged route opportunities, strict predecessors and monotonic actual-payment receipts.");
    }

    private static void InvalidEventData()
    {
        var good = Start(0);
        foreach (string field in new[] { "Version", "InstanceId", "DemocritusInstanceId", "EmpedoclesInstanceId", "Stage",
            "PendingAction", "ConfirmedActions", "Measurements", "Adopted", "Closed" })
        {
            JsonObject json = JsonSerializer.SerializeToNode(good.State)!.AsObject(); json.Remove(field);
            var invalid = json.Deserialize<GalileoMeasuredPracticeState>()!;
            Check(!invalid.IsUsable() && !Reload(invalid).IsUsable(), $"Missing {field} cannot become a fresh event or reward after resaving.");
        }
        var declined = Start(3);
        Check(!Consistent(declined), "A declined Empedocles invitation is not an accepted predecessor.");
        var bad = Reload(good); bad.State.DemocritusInstanceId = "OTHER"; Check(!Consistent(bad), "An unrelated Democritus record cannot substitute.");
        bad = Reload(good); bad.State.EmpedoclesInstanceId = "OTHER"; Check(!Consistent(bad), "An unrelated Empedocles record cannot substitute.");
        bad = Reload(good); bad.Democritus.Closed = false; Check(!Consistent(bad), "An unfinished adopted predecessor is insufficient.");
        bad = Reload(good); bad.Journey.LastFixedAct = 3; Check(!Consistent(bad), "A spent later fixed opportunity cannot masquerade as the unchanged source route.");
        bad = Reload(good); bad.Journey.CompletedEdgeIds.Remove(DemocritusArrangementState.PrerequisiteEdgeId);
        Check(!Consistent(bad), "The actual first challenge must remain present.");
        bad = Reload(good); bad.State.Version = 99; Check(!Consistent(bad), "Unknown versions cannot migrate into a grant.");
        var measured = good;
        foreach (var action in new[] { GalileoMeasuredPracticeAction.AcceptInvitation, GalileoMeasuredPracticeAction.LongFirst,
            GalileoMeasuredPracticeAction.TakeRemaining, GalileoMeasuredPracticeAction.CheckClock,
            GalileoMeasuredPracticeAction.RepeatCorrected }) measured = Commit(measured, action);
        var altered = Reload(measured); altered.State.Measurements.Reverse(); Check(!Consistent(altered), "Measurement order cannot be rewritten after seeing the result.");
        altered = Reload(measured); altered.State.Measurements[0].RawTicks = 4; Check(!Consistent(altered), "Raw readings are immutable historical evidence.");
        altered = Reload(measured); altered.State.Measurements[0].CorrectedTicks = 99; Check(!Consistent(altered), "Unobserved corrected readings cannot authorize a protocol.");
        foreach (string field in new[] { "StationaryTicks" })
        {
            JsonObject json = JsonSerializer.SerializeToNode(measured.State)!.AsObject(); json.Remove(field);
            Check(!json.Deserialize<GalileoMeasuredPracticeState>()!.IsUsable(), "A corrected record needs its actual zero-point measurement.");
        }
        altered = Reload(measured); altered.State.ConfirmedActions.Remove(GalileoMeasuredPracticeAction.RepeatCorrected);
        Check(!Consistent(altered), "Corrected data without the repeat action is invalid.");
        altered = Reload(good); altered.State.PendingAction = GalileoMeasuredPracticeAction.AdoptProtocol;
        Check(!Consistent(altered), "A forged pending reward cannot bypass measurement stages.");
    }

    private static void Payments()
    {
        const string key = "2:4:12", model = "CARD.STRIKE_SILENT";
        var state = GalileoRepeatedPaymentState.Create();
        Check(!state.ObservePayment(key, model, 1, 1, true, false, true) && state.BeginCombat(key),
            "Only a known actual combat can receive a sample.");
        string empty = state.Encode();
        foreach (var invalid in new (string Key, string Model, decimal Paid, int Turn, bool Manual, bool X, bool Eligible)[]
        {
            (Key: key, Model: model, Paid: 0m, Turn: 1, Manual: true, X: false, Eligible: true),
            (key, model, -1m, 1, true, false, true), (key, model, 1m, 0, true, false, true),
            (key, model, 1m, 1, false, false, true), (key, model, 1m, 1, true, true, true),
            (key, model, 1m, 1, true, false, false), ("2:4:99", model, 1m, 1, true, false, true),
            (key, "", 1m, 1, true, false, true), (key, "foreign", 1m, 1, true, false, true),
        })
            Check(!state.ObservePayment(invalid.Key, invalid.Model, invalid.Paid, invalid.Turn,
                invalid.Manual, invalid.X, invalid.Eligible) && state.Encode() == empty,
                "Free, X, automatic, wrong-type, wrong-combat and invalid-identity cards never create a measurement.");
        Check(state.ObservePayment(key, model, 1.25m, 2, true, false, true)
            && !state.ObservePayment(key, "CARD.DEFEND_SILENT", 3, 3, true, false, true),
            "Only the first actual positive payment fixes the sample, with exact decimal precision.");
        string sampled = state.Encode();
        Check(state.BeginCombat(key) && state.Encode() == sampled
            && GalileoRepeatedPaymentState.TryRestore(sampled, out state) && state.Encode() == sampled,
            "A repeated combat-start or native payload reload cannot erase the first sample.");
        foreach (var invalid in new (string Key, string Model, decimal Paid, int Turn, bool Manual, bool X, bool Release)[]
        {
            (Key: key, Model: model, Paid: 1.25m, Turn: 2, Manual: true, X: false, Release: true),
            (key, model, 1.25m, 1, true, false, true), (key, model, 1m, 3, true, false, true),
            (key, "CARD.DEFEND_SILENT", 1.25m, 3, true, false, true), (key, model, 0m, 3, true, false, true),
            (key, model, 1.25m, 3, false, false, true), (key, model, 1.25m, 3, true, true, true),
            (key, model, 1.25m, 3, true, false, false), ("2:5:13", model, 1.25m, 3, true, false, true),
        })
            Check(!state.TryClaim(invalid.Key, invalid.Model, invalid.Paid, invalid.Turn,
                invalid.Manual, invalid.X, invalid.Release) && state.Encode() == sampled,
                "A genuine later-turn manual, same-model, same-actual-payment release is required.");
        Check(state.TryClaim(key, model, 1.25m, 3, true, false, true) && state.Claimed
            && !state.TryClaim(key, model, 1.25m, 3, true, false, true), "The receipt is claimed before a native draw and cannot replay.");
        string claimed = state.Encode();
        Check(GalileoRepeatedPaymentState.TryRestore(claimed, out state) && state.BeginCombat(key)
            && !state.TryClaim(key, model, 1.25m, 4, true, false, true) && state.Encode() == claimed,
            "Claimed means once per combat, not once per later turn or reload.");
        Check(state.BeginCombat("2:5:13") && state.SampleModelId.Length == 0 && !state.Claimed
            && !state.BeginCombat(key) && !state.ObservePayment(key, model, 1, 1, true, false, true),
            "A different real combat starts fresh; returning to an already seen key cannot reset its receipt.");
        foreach (string field in new[] { "Version", "CombatKey", "SampleModelId", "SamplePaid", "SampleTurn", "Claimed", "PreviousCombatKeys" })
        {
            JsonObject json = JsonNode.Parse(claimed)!.AsObject(); json.Remove(field);
            Check(!GalileoRepeatedPaymentState.TryRestore(json.ToJsonString(), out _), $"Missing {field} cannot mint an unclaimed ledger.");
        }
        foreach (string payload in new[] { "broken", "{}", "null", "{\"Version\":99}" })
            Check(!GalileoRepeatedPaymentState.TryRestore(payload, out _), "Unknown or corrupt payloads are rejected for native raw isolation.");
        foreach (string keyBad in new[] { "", "2:4", "2:4:12:1", "-1:4:12", "two:4:12" })
            Check(!state.BeginCombat(keyBad), "Malformed combat identity cannot clear the sample.");
        var malformed = GalileoRepeatedPaymentState.Create(); malformed.Claimed = true;
        Check(!malformed.IsUsable(), "No empty combat or absent sample can already be claimed.");
        malformed = GalileoRepeatedPaymentState.Create(); malformed.PreviousCombatKeys = [key, key];
        Check(!malformed.IsUsable(), "A duplicated historical combat receipt is corrupt.");
    }
}
