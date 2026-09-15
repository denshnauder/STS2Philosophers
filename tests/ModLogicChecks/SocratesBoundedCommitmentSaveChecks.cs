using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class SocratesBoundedCommitmentSaveChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SocratesBoundedCommitmentState Resume(SocratesBoundedCommitmentState source)
    {
        string payload = SocratesBoundedCommitmentStateCodec.Serialize(source);
        Check(SocratesBoundedCommitmentStateCodec.TryRestore(payload, "battle", out var restored), "Restore a valid checkpoint.");
        Check(SocratesBoundedCommitmentStateCodec.Serialize(restored!) == payload, "All protocol fields survive a checkpoint.");
        return restored!;
    }

    public static void Run()
    {
        var s = Resume(new("battle"));
        Check(s.OpenNormalTurn("battle", 1), "Open after restoring an unused state.");
        s = Resume(s);
        Check(s.FreezeFacts("battle", 1, 8, 0, ["A", "B"]), "Freeze after restore.");
        s = Resume(s);
        Check(s.Select("battle", 1, SocratesCommitmentMode.Guard), "Initial choice after restore.");
        s = Resume(s);
        Check(!s.Select("battle", 1, SocratesCommitmentMode.Clear), "Restore cannot reopen the declaration.");
        Check(s.LockActions("battle", 1), "Lock after declaration.");
        s = Resume(s);
        Check(!s.Select("battle", 1, SocratesCommitmentMode.Clear), "Restore cannot rewind an action lock.");
        Check(s.CloseNormalTurn("battle", 1, 4, []), "Close from restored action phase.");
        s = Resume(s);
        Check(!s.CloseNormalTurn("battle", 1, 4, []), "Restored close is idempotent.");
        Check(s.OpenNormalTurn("battle", 2), "Open receiving turn.");
        s = Resume(s);
        Check(s.TakeOpeningDraw("battle", 2) == 1, "Pending draw survives the opening checkpoint.");
        s = Resume(s);
        Check(s.TakeOpeningDraw("battle", 2) == 0, "Consumed draw cannot return through reload.");
        Check(s.FreezeFacts("battle", 2, 0, 0, []), "Freeze a quiet turn.");
        Check(s.Select("battle", 2, SocratesCommitmentMode.Clear), "Revise without an opportunity.");
        s = Resume(s);
        Check(s.Revised && !s.Select("battle", 2, SocratesCommitmentMode.Guard), "Revision cost and same-turn lock survive.");
        Check(s.LockActions("battle", 2) && s.CloseNormalTurn("battle", 2, 0, []), "Quiet revision can close.");
        s = Resume(s);
        Check(s.OpenNormalTurn("battle", 3) && s.FreezeFacts("battle", 3, null, 0, ["A"]), "Unknown public damage is retained.");
        Check(s.Select("battle", 3, SocratesCommitmentMode.Guard), "A second revision survives earlier saves.");
        s = Resume(s);
        Check(s.LockActions("battle", 3) && s.CloseNormalTurn("battle", 3, 10, []), "Unknown evidence closes safely.");
        s = Resume(s);
        Check(s.Observation == SocratesCommitmentObservation.Incomplete, "Unknown does not become a guessed target.");
        Check(s.EndCombat("battle"), "End the restored combat.");
        s = Resume(s);
        Check(!s.OpenNormalTurn("battle", 4) && s.TakeOpeningDraw("battle", 3) == 0, "Ended checkpoints cannot restart.");

        string blank = SocratesBoundedCommitmentStateCodec.Serialize(new("battle"));
        void Reject(string? payload, string combat = "battle")
        {
            Check(!SocratesBoundedCommitmentStateCodec.TryRestore(payload, combat, out var restored) && restored is null,
                "Invalid checkpoint must fail without a fresh claimable state.");
        }
        Reject(null); Reject("{}"); Reject("[]"); Reject("not json"); Reject(blank, "other");
        Reject(blank.Replace("\"Version\":1", "\"Version\":1,\"Version\":1"));
        foreach (var (field, value) in new[] { ("Version", "2"), ("Mode", "99"), ("Phase", "99"),
            ("Turn", "-1"), ("Attackers", "null"), ("Attackers", "[\"A\",\"A\"]"),
            ("Attackers", "[\"\"]"), ("PendingDraw", "1"), ("Revised", "true"), ("CombatId", "null") })
        {
            JsonObject node = JsonNode.Parse(blank)!.AsObject(); node[field] = JsonNode.Parse(value); Reject(node.ToJsonString());
        }
        JsonObject missing = JsonNode.Parse(blank)!.AsObject(); missing.Remove("DecisionMade"); Reject(missing.ToJsonString());
        missing = JsonNode.Parse(blank)!.AsObject(); missing["Extra"] = 1; Reject(missing.ToJsonString());
        Console.WriteLine("Socrates C3 save protocol checks passed; actual engine draw/save transaction remains unverified.");
    }
}
