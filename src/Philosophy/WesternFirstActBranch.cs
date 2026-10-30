using System.Globalization;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// The first act can offer more than one fixed承接 from the same entry node
/// (for example Plato's institutions line). Rows are cross-run alternatives,
/// so a run takes exactly one of the eligible first-act fixed edges; the
/// choice is derived from the run seed and stays stable across reloads.
/// </summary>
internal static class WesternFirstActBranch
{
    public static bool IsChosen(RunState run, WesternJourneyState journey, WesternRouteGraph graph, string edgeId)
    {
        List<string> candidates = graph.GetEligibleEdges(journey, "Fixed")
            .Where(edge => edge.Act == 1 && edge.FromNodeId == journey.CurrentNodeId)
            .Select(edge => edge.EdgeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count <= 1) return candidates.Count == 0 || candidates[0] == edgeId;
        string seed = run.Rng.Seed.ToString(CultureInfo.InvariantCulture);
        uint hash = 2166136261;
        foreach (char c in seed)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return candidates[(int)(hash % (uint)candidates.Count)] == edgeId;
    }
}
