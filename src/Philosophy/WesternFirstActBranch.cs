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
    /// <summary>
    /// True when the run's first-act branch is the one this room sits on. A room
    /// asks this with the node it opens from, not with its own act-two or act-three
    /// edge: those never appear among the first act's candidates, so asking with
    /// them could only ever answer no.
    /// </summary>
    public static bool IsOnBranch(RunState run, WesternJourneyState journey, WesternRouteGraph graph, string sourceNodeId)
    {
        // The run already stands on the figure this room opens from, so the
        // branch into it was walked.
        if (sourceNodeId == journey.CurrentNodeId) return true;
        List<WesternGraphEdge> branch = graph.GetEligibleEdges(journey, "Fixed")
            .Where(edge => edge.Act == 1 && edge.FromNodeId == journey.CurrentNodeId)
            .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToList();
        if (branch.Count == 0) return false;
        if (branch.Count == 1) return branch[0].ToNodeId == sourceNodeId;
        string seed = run.Rng.Seed.ToString(CultureInfo.InvariantCulture);
        uint hash = 2166136261;
        foreach (char c in seed)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return branch[(int)(hash % (uint)branch.Count)].ToNodeId == sourceNodeId;
    }

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
