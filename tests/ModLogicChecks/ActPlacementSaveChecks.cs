using System.Reflection;
using System.Text.Json;
using STS2Philosophers;

/// <summary>
/// A room a route places in a later act carries that act and the edge that
/// placed it. Both live in the saved record, so a round trip has to keep them:
/// if they were dropped, a reloaded run would put the room back in the act it
/// was written for and refuse to open.
/// </summary>
internal static class ActPlacementSaveChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        var placed = typeof(WesternJourneyState).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetProperty("ActIndex", BindingFlags.Public | BindingFlags.Instance) is { CanRead: true, CanWrite: true })
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
        Check(placed.Count >= 25, $"Only {placed.Count} room states carry the act they were placed in.");

        var lost = new List<string>();
        foreach (Type type in placed)
        {
            object state;
            try { state = Activator.CreateInstance(type, nonPublic: true)!; }
            catch (MissingMethodException) { continue; }
            type.GetProperty("ActIndex")!.SetValue(state, 3);
            PropertyInfo? edge = type.GetProperty("EdgeId");
            if (edge is { CanWrite: true }) edge.SetValue(state, "WESTERN_EDGE_TEST");
            string payload = JsonSerializer.Serialize(state, type);
            object? restored = JsonSerializer.Deserialize(payload, type);
            if (restored is null
                || (int)type.GetProperty("ActIndex")!.GetValue(restored)! != 3
                || edge is { CanWrite: true } && (string?)edge.GetValue(restored) != "WESTERN_EDGE_TEST")
                lost.Add(type.Name);
        }
        // The route a run follows, the entry it started from and the rules it was
        // started under all live in the same record. A reload that dropped any of
        // them would leave the run unable to open its later acts.
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        journey.RouteRules = true;
        journey.RouteId = "UNCONSCIOUS_AND_NORM";
        journey.LastFixedAct = 2;
        journey.DeclinedFixedActs.Add(2);
        journey.ResolvedQuestionActs.Add(1);
        journey.CandidateWindows["2:Fixed:DESCARTES__SELF_AND_OTHER__CORE:"] = ["WESTERN_EDGE_087"];
        string journeyPayload = JsonSerializer.Serialize(journey);
        WesternJourneyState? reloaded = JsonSerializer.Deserialize<WesternJourneyState>(journeyPayload);
        Check(reloaded is not null
            && reloaded.RouteId == "UNCONSCIOUS_AND_NORM"
            && reloaded.EntryNodeId == "DESCARTES__SELF_AND_OTHER__CORE"
            && reloaded.RouteRules
            && reloaded.LastFixedAct == 2
            && reloaded.DeclinedFixedActs.Contains(2)
            && reloaded.ResolvedQuestionActs.Contains(1)
            && reloaded.CandidateWindows.Count == 1,
            "A saved journey must keep its route, entry and rules across a reload.");

        Check(lost.Count == 0,
            "These room states lose the act they were placed in when saved: " + string.Join(", ", lost) + ".");
    }
}
