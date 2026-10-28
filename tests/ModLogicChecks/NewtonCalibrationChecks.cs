using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class NewtonCalibrationChecks
{
    static void Check(bool ok, string msg) { if (!ok) throw new InvalidOperationException("Newton calibration: " + msg); }

    static NewtonCalibrationPracticeState Fresh()
    {
        var s = new NewtonCalibrationPracticeState();
        Check(s.StartTurn("2:9:4", 1), "initial turn begins");
        return s;
    }

    static void Record(NewtonCalibrationPracticeState s, params NewtonCalibrationCardKind[] kinds)
    {
        foreach (var k in kinds) Check(s.RecordManualCard(s.CombatKey, s.TurnNumber, k), "record failed");
    }

    static NewtonCalibrationPracticeState Reload(NewtonCalibrationPracticeState s)
    {
        Check(NewtonCalibrationPracticeState.TryRestore(s.Encode(), out var r), "restore failed");
        return r!;
    }

    static void Bad(string? json)
    {
        Check(!NewtonCalibrationPracticeState.TryRestore(json, out var r), "expected reject");
        Check(r!.CombatKey == "" && r.TurnNumber == 0 && r.ManualCardCount == 0
            && r.PlayedCards.Count == 0 && r.ClaimedSite == NewtonSite.None, "pristine");
        Check(!r.Claimed, "reject not claimed");
        Check(!r.TryClaim("2:9:4", 1, NewtonSite.Drop), "reject cannot claim");
    }

    static string Mutate(string json, string key, JsonNode? value)
    {
        var o = JsonNode.Parse(json)!.AsObject();
        o[key] = value;
        return o.ToJsonString();
    }

    public static void Run()
    {
        // 1 pristine + site pairs
        var p = new NewtonCalibrationPracticeState();
        Check(p.Encode() != null && Reload(p).CombatKey == "", "pristine roundtrip");
        var pairs = new (NewtonCalibrationCardKind A, NewtonCalibrationCardKind S, NewtonSite Site, NewtonSite Other)[]
        {
            (NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Skill, NewtonSite.Drop, NewtonSite.Tide),
            (NewtonCalibrationCardKind.Skill, NewtonCalibrationCardKind.Attack, NewtonSite.Tide, NewtonSite.Drop),
        };
        foreach (var t in pairs)
        {
            var s = Fresh(); Record(s, t.A, t.S);
            Check(!s.TryClaim(s.CombatKey, s.TurnNumber, t.Other), "opposite site");
            Check(s.TryClaim(s.CombatKey, s.TurnNumber, t.Site), "pair site");
            Check(!s.TryClaim(s.CombatKey, s.TurnNumber, t.Site), "dup claim");
        }

        // 2 wrong pairs
        var wrong = new (NewtonCalibrationCardKind A, NewtonCalibrationCardKind S)[]
        {
            (NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Attack),
            (NewtonCalibrationCardKind.Skill, NewtonCalibrationCardKind.Skill),
            (NewtonCalibrationCardKind.Other, NewtonCalibrationCardKind.Attack),
            (NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Other),
        };
        foreach (var t in wrong)
        {
            var s = Fresh(); Record(s, t.A, t.S);
            Check(!s.TryClaim(s.CombatKey, s.TurnNumber, NewtonSite.Drop), "wrong pair drop");
            Check(!s.TryClaim(s.CombatKey, s.TurnNumber, NewtonSite.Tide), "wrong pair tide");
        }
        var three = Fresh(); Record(three, NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Skill, NewtonCalibrationCardKind.Attack);
        Check(!three.TryClaim(three.CombatKey, three.TurnNumber, NewtonSite.Drop), "third card");
        Check(!three.RecordManualCard(three.CombatKey, three.TurnNumber, NewtonCalibrationCardKind.Skill), "fourth card");
        Check(three.ManualCardCount == 3, "count stays 3");
        Check(!three.TryClaim(three.CombatKey, three.TurnNumber, NewtonSite.Drop), "third card denial persists");
        foreach (var n in new[] { 0, 1 })
        {
            var s = Fresh();
            for (int i = 0; i < n; i++) Record(s, NewtonCalibrationCardKind.Attack);
            Check(!s.TryClaim(s.CombatKey, s.TurnNumber, NewtonSite.Drop), "too few");
        }

        // 3 valid AS snapshot
        var v = Fresh(); Record(v, NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Skill);
        Check(!v.TryClaim("x", v.TurnNumber, NewtonSite.Drop), "claim wrong combat");
        Check(!v.TryClaim(v.CombatKey, 99, NewtonSite.Drop), "claim wrong turn");
        Check(!v.RecordManualCard("x", v.TurnNumber, NewtonCalibrationCardKind.Attack), "record wrong combat");
        Check(!v.RecordManualCard(v.CombatKey, 99, NewtonCalibrationCardKind.Attack), "record wrong turn");
        var before = v.Encode();
        Check(!v.StartCombat(v.CombatKey), "dup combat");
        Check(!v.StartTurn(v.CombatKey, v.TurnNumber), "dup turn");
        Check(!v.StartTurn(v.CombatKey, 0), "turn 0");
        Check(v.Encode() == before, "same turn unchanged");
        Check(v.TryClaim(v.CombatKey, v.TurnNumber, NewtonSite.Drop), "claim once");
        var claimed = v.Encode();
        Check(!v.StartCombat(v.CombatKey), "dup combat claimed");
        Check(!v.StartTurn(v.CombatKey, v.TurnNumber), "dup turn claimed");
        Check(v.Encode() == claimed, "dup snapshot unchanged");
        Check(v.StartTurn(v.CombatKey, 2), "turn2 start");
        Check(v.ManualCardCount == 0 && v.PlayedCards.Count == 0 && !v.Claimed && v.ClaimedSite == NewtonSite.None, "turn2 reset");
        Record(v, NewtonCalibrationCardKind.Skill, NewtonCalibrationCardKind.Attack);
        Check(v.TryClaim(v.CombatKey, 2, NewtonSite.Tide), "turn2 claim");
        Check(!v.TryClaim(v.CombatKey, 2, NewtonSite.Tide), "turn2 dup claim");
        var turn2 = v.Encode();
        Check(!v.StartTurn(v.CombatKey, 1), "stale turn1");
        Check(v.Encode() == turn2, "stale unchanged");
        Check(v.StartCombat("new"), "new combat");
        Check(v.ManualCardCount == 0 && v.TurnNumber == 0 && !v.Claimed, "combat reset");
        Check(!v.TryClaim("2:9:4", 2, NewtonSite.Tide), "old key denied");

        // 4 restore
        var r = Fresh(); Record(r, NewtonCalibrationCardKind.Attack);
        var r1 = Reload(r);
        Check(r1.ManualCardCount == 1 && r1.PlayedCards[0] == NewtonCalibrationCardKind.Attack, "restore card");
        Check(!r1.StartCombat(r1.CombatKey), "restore dup combat");
        Check(!r1.StartTurn(r1.CombatKey, r1.TurnNumber), "restore dup turn");
        Check(r1.ManualCardCount == 1 && r1.PlayedCards[0] == NewtonCalibrationCardKind.Attack, "restore card kept");
        Check(r1.RecordManualCard(r1.CombatKey, r1.TurnNumber, NewtonCalibrationCardKind.Skill), "restore record skill");
        Check(r1.TryClaim(r1.CombatKey, r1.TurnNumber, NewtonSite.Drop), "restore claim");
        var r2 = Reload(r1);
        Check(!r2.StartCombat(r2.CombatKey), "restored dup combat claimed");
        Check(!r2.StartTurn(r2.CombatKey, r2.TurnNumber), "restored dup turn claimed");
        Check(r2.Claimed && r2.ClaimedSite == NewtonSite.Drop, "restored claim kept");
        Check(!r2.TryClaim(r2.CombatKey, r2.TurnNumber, NewtonSite.Drop), "restored no reclaim");
        Check(r2.StartTurn(r2.CombatKey, 2), "restore later turn start");
        Check(r2.ManualCardCount == 0 && r2.PlayedCards.Count == 0 && !r2.Claimed && r2.ClaimedSite == NewtonSite.None, "restore later turn");

        // 5 mutations
        var valid = Fresh(); Record(valid, NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Skill);
        var json = valid.Encode();
        var muts = new (string Key, JsonNode? Val)[]
        {
            ("Unknown", 1), ("Version", 2), ("ManualCardCount", "x"), ("ManualCardCount", -1),
            ("ManualCardCount", 4), ("ManualCardCount", 1), ("TurnNumber", -1), ("TurnNumber", 0),
            ("CombatKey", ""), ("CombatKey", "bad key"), ("ClaimedSite", 1), ("ClaimedSite", 2),
        };
        foreach (var m in muts) Bad(Mutate(json, m.Key, m.Val));
        var o = JsonNode.Parse(json)!.AsObject();
        o.Remove("PlayedCards"); Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o.Remove("Version"); Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["PlayedCards"] = new JsonArray(99, 2); Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["ClaimedSite"] = 1; o["ManualCardCount"] = 1; Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["ClaimedSite"] = 2; o["ManualCardCount"] = 1; Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["Claimed"] = true; o["ClaimedSite"] = 2; Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["Claimed"] = true; o["ClaimedSite"] = 0; Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["Claimed"] = true; o["ClaimedSite"] = 1; o["ManualCardCount"] = 1;
        o["PlayedCards"] = new JsonArray(1); Bad(o.ToJsonString());
        o = JsonNode.Parse(json)!.AsObject(); o["Claimed"] = true; o["ClaimedSite"] = 1;
        o["PlayedCards"] = new JsonArray(2, 1); Bad(o.ToJsonString());
        Bad(json.Replace("\"CombatKey\":", "\"CombatKey\":\"dup\",\"CombatKey\":"));
        foreach (var b in new string?[] { null, "", " ", "{", "[]", "{}", new string('x', 100000) }) Bad(b);

        // 6 independent states
        var a = Fresh(); var b2 = Fresh();
        Record(a, NewtonCalibrationCardKind.Attack, NewtonCalibrationCardKind.Skill);
        Check(a.TryClaim(a.CombatKey, a.TurnNumber, NewtonSite.Drop), "a claim");
        Check(b2.ManualCardCount == 0 && b2.PlayedCards.Count == 0 && !b2.Claimed, "a leaves b pristine");
        Record(b2, NewtonCalibrationCardKind.Skill, NewtonCalibrationCardKind.Attack);
        Check(b2.ClaimedSite == NewtonSite.None, "b empty");
        Check(b2.TryClaim(b2.CombatKey, b2.TurnNumber, NewtonSite.Tide), "b claim");
        Check(a.ClaimedSite == NewtonSite.Drop, "a still drop");

        Console.WriteLine("NewtonCalibrationChecks passed");
    }
}
