using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed record WesternGraphNode(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("thinker_id")] string ThinkerId,
    [property: JsonPropertyName("problem_id")] string ProblemId,
    [property: JsonPropertyName("stage_id")] string StageId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("terminal")] bool Terminal);

internal sealed record WesternGraphEdge(
    [property: JsonPropertyName("edge_id")] string EdgeId,
    [property: JsonPropertyName("from_node_id")] string FromNodeId,
    [property: JsonPropertyName("to_node_id")] string ToNodeId,
    [property: JsonPropertyName("act")] int Act,
    [property: JsonPropertyName("slot")] string Slot,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("relation_summary")] string RelationSummary,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("required_edge_ids")] string[] RequiredEdgeIds,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("context")] WesternEdgeContext Context,
    [property: JsonPropertyName("retirement_reason")] string? RetirementReason = null,
    [property: JsonPropertyName("route_ids")] string[]? RouteIds = null);

internal sealed record WesternEdgeContext(
    [property: JsonPropertyName("from_problem_id")] string FromProblemId,
    [property: JsonPropertyName("to_problem_id")] string ToProblemId,
    [property: JsonPropertyName("relation_types")] string[] RelationTypes,
    [property: JsonPropertyName("compressed_mediators")] string CompressedMediators,
    [property: JsonPropertyName("behavior_requirement")] string BehaviorRequirement,
    [property: JsonPropertyName("ethical_boundary")] string EthicalBoundary,
    [property: JsonPropertyName("interpretation_boundary")] string InterpretationBoundary,
    [property: JsonPropertyName("evidence_status")] string EvidenceStatus,
    [property: JsonPropertyName("evidence_sources")] string[] EvidenceSources);

internal sealed record WesternGraphSample(
    [property: JsonPropertyName("sample_id")] string SampleId,
    [property: JsonPropertyName("entry_node_id")] string EntryNodeId,
    [property: JsonPropertyName("edge_ids")] string[] EdgeIds,
    [property: JsonPropertyName("terminal_node_id")] string TerminalNodeId,
    [property: JsonPropertyName("disposition")] string Disposition,
    [property: JsonPropertyName("review_note")] string ReviewNote,
    [property: JsonPropertyName("route_id")] string? RouteId = null);

internal sealed class WesternJourneyState
{
    public string CurrentNodeId { get; set; } = string.Empty;
    public int LastFixedAct { get; set; }
    public HashSet<string> SeenThinkerIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> CompletedEdgeIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<int> ResolvedQuestionActs { get; set; } = [];
    public Dictionary<string, List<string>> CandidateWindows { get; set; } = new(StringComparer.Ordinal);
    public HashSet<int> DeclinedFixedActs { get; set; } = [];
    public bool RetainedEnding { get; set; }

    /// <summary>
    /// The default mainline this run follows. Empty for saves written before the
    /// route table existed, which keeps every edge open exactly as before.
    /// </summary>
    public string RouteId { get; set; } = string.Empty;

    /// <summary>The entry node the run started from, kept so a route can be named later.</summary>
    public string EntryNodeId { get; set; } = string.Empty;

    /// <summary>
    /// True for a run started under the route rules. Those runs only ever walk
    /// content that belongs to a route; the edges written before routes existed
    /// stay visible to older saves, which is why this is a saved fact and not a
    /// build constant.
    /// </summary>
    public bool RouteRules { get; set; }

    internal void NormalizeAfterLoad()
    {
        CurrentNodeId ??= string.Empty;
        SeenThinkerIds ??= new(StringComparer.Ordinal);
        CompletedEdgeIds ??= new(StringComparer.Ordinal);
        ResolvedQuestionActs ??= [];
        CandidateWindows ??= new(StringComparer.Ordinal);
        DeclinedFixedActs ??= [];
        RouteId ??= string.Empty;
        EntryNodeId ??= string.Empty;
        if (EntryNodeId.Length == 0) RouteRules = false;
    }
}

internal sealed class WesternRouteGraph
{
    private sealed record Payload(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("nodes")] WesternGraphNode[] Nodes,
        [property: JsonPropertyName("edges")] WesternGraphEdge[] Edges,
        [property: JsonPropertyName("samples")] WesternGraphSample[] Samples);

    private readonly IReadOnlyDictionary<string, WesternGraphNode> _nodes;
    private readonly IReadOnlyDictionary<string, WesternGraphEdge> _edges;
    public IReadOnlyList<WesternGraphSample> Samples { get; }
    public IEnumerable<WesternGraphSample> ExecutableSamples => Samples.Where(sample => sample.Disposition == "Executable");
    public IEnumerable<WesternGraphNode> Nodes => _nodes.Values;
    public IEnumerable<WesternGraphEdge> Edges => _edges.Values;

    private WesternRouteGraph(Payload data)
    {
        _nodes = data.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        _edges = data.Edges.ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        Samples = Array.AsReadOnly(data.Samples);
    }

    public static WesternRouteGraph LoadEmbedded()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("STS2Philosophers.config.western_graph.json")
            ?? throw new InvalidOperationException("Missing western graph resource.");
        using StreamReader reader = new(stream);
        return ParseJson(reader.ReadToEnd());
    }

    public static WesternRouteGraph ParseJson(string json)
    {
        Payload data = JsonSerializer.Deserialize<Payload>(json)
            ?? throw new InvalidDataException("Western graph is empty.");
        if (data.SchemaVersion != 3 || data.Nodes is null || data.Edges is null || data.Samples is null
            || data.Nodes.Any(node => node is null) || data.Edges.Any(edge => edge is null) || data.Samples.Any(sample => sample is null))
            throw new InvalidDataException("Invalid western graph structure.");
        WesternRouteGraph graph;
        try { graph = new(data); }
        catch (ArgumentException exception) { throw new InvalidDataException("Duplicate or missing graph IDs.", exception); }
        graph.Validate();
        return graph;
    }

    public WesternJourneyState Start(string entryNodeId)
    {
        if (!_nodes.TryGetValue(entryNodeId, out WesternGraphNode? node)
            || !WesternRouteCatalog.IsEntryProblem(node.ThinkerId, node.ProblemId))
            throw new InvalidOperationException("This node is not an approved entry.");
        return new()
        {
            CurrentNodeId = entryNodeId,
            EntryNodeId = entryNodeId,
            LastFixedAct = 1,
            SeenThinkerIds = [node.ThinkerId],
        };
    }

    public WesternGraphNode GetNode(string nodeId) => _nodes[nodeId];
    public WesternGraphEdge GetEdge(string edgeId) => _edges[edgeId];

    public IReadOnlyList<WesternGraphEdge> GetEligibleEdges(WesternJourneyState state, string slot)
    {
        if (!_nodes.ContainsKey(state.CurrentNodeId) || state.LastFixedAct is < 1 or > 3) return [];
        return _edges.Values.Where(edge => IsEligible(state, edge, slot)).OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// A run that takes no question still follows a line. The design gives every
    /// entry a default one, and the route table lists that line first, so a run
    /// that reaches the second act unnamed joins it. Runs that predate the route
    /// rules keep whatever they were saved with.
    /// </summary>
    public static void NameDefaultRoute(WesternJourneyState state)
    {
        if (!state.RouteRules || !string.IsNullOrEmpty(state.RouteId)
            || string.IsNullOrEmpty(state.EntryNodeId)) return;
        // A run that already answered the first act's question has made its
        // choice, even when that question named no single line; the default is
        // only for runs that took no question at all. It also has to still be
        // standing on its entry: a run that walked the first act's turn is on a
        // different node, and the default line does not start there.
        if (state.ResolvedQuestionActs.Contains(1) || state.CurrentNodeId != state.EntryNodeId) return;
        WesternRouteDefinition? route = WesternRouteTable.Instance.ForEntry(state.EntryNodeId);
        if (route is not null) state.RouteId = route.RouteId;
    }

    public bool TryApply(WesternJourneyState state, string edgeId)
    {
        if (!_edges.TryGetValue(edgeId, out WesternGraphEdge? edge) || !IsEligible(state, edge, edge.Slot)) return false;
        // A refused or answered question ends its own act: the same question
        // cannot be walked later as if the refusal had not happened.
        if (edge.Slot == "Question" && state.ResolvedQuestionActs.Contains(edge.Act)) return false;
        state.CompletedEdgeIds.Add(edgeId);
        state.SeenThinkerIds.Add(_nodes[edge.ToNodeId].ThinkerId);
        if (edge.Slot == "Question")
        {
            // A question belongs to the act it is written for, not to wherever
            // the fixed position happens to stand. Recording the position too
            // made the second act's question spend the third act's, which the
            // design forbids.
            state.ResolvedQuestionActs.Add(edge.Act);
            // The first act's question is how the design says a run picks its
            // mainline ("第一幕问号X转线后"). Name the route only when exactly one
            // route of this entry continues through that figure.
            if (edge.Act == 1 && string.IsNullOrEmpty(state.RouteId))
            {
                string target = _nodes[edge.ToNodeId].ThinkerId;
                string[] candidates = WesternRouteTable.Instance.Routes
                    .Where(route => route.EntryNodeId == state.EntryNodeId && route.ActTwoFigure == target)
                    .Select(route => route.RouteId)
                    .ToArray();
                if (candidates.Length == 1) state.RouteId = candidates[0];
            }
        }
        else state.LastFixedAct = Math.Min(3, state.LastFixedAct + 1);
        if (edge.Outcome is "Adopt" or "Switch") state.CurrentNodeId = edge.ToNodeId;
        return true;
    }

    public bool TryReserveQuestion(WesternJourneyState state, int expectedAct)
    {
        // This act's question stays answerable after the fixed slot moved the
        // position on: the act, not the position, decides whose question it is.
        return _nodes.ContainsKey(state.CurrentNodeId)
            && state.LastFixedAct is var position && expectedAct <= position
            && expectedAct is >= 1 and <= 3
            && !state.RetainedEnding
            && state.ResolvedQuestionActs.Add(expectedAct);
    }

    public bool TryCompleteReservedQuestion(WesternJourneyState state, string edgeId)
    {
        if (!_edges.TryGetValue(edgeId, out WesternGraphEdge? edge)
            || edge.Slot != "Question"
            || !edge.Enabled
            || edge.FromNodeId != state.CurrentNodeId
            || state.LastFixedAct is < 1 or > 3
            || edge.Act > state.LastFixedAct
            || !state.ResolvedQuestionActs.Contains(edge.Act)
            || edge.RequiredEdgeIds.Any(required => !state.CompletedEdgeIds.Contains(required)))
        {
            return false;
        }

        if (state.CompletedEdgeIds.Contains(edgeId))
        {
            return true;
        }

        if (state.SeenThinkerIds.Contains(_nodes[edge.ToNodeId].ThinkerId))
        {
            return false;
        }

        state.CompletedEdgeIds.Add(edgeId);
        state.SeenThinkerIds.Add(_nodes[edge.ToNodeId].ThinkerId);
        if (edge.Outcome is "Adopt" or "Switch")
        {
            state.CurrentNodeId = edge.ToNodeId;
        }

        return true;
    }

    /// <summary>
    /// An edge that belongs to a route is invisible to a run that has no route
    /// identity, so new route content cannot hijack a save written before routes
    /// existed. Unscoped edges stay visible to everyone.
    /// </summary>
    private bool RouteMatches(WesternGraphEdge edge, WesternJourneyState state)
    {
        if (edge.RouteIds is null or { Length: 0 })
        {
            if (!state.RouteRules || edge.Slot != "Fixed" || edge.Act <= 1) return true;
            // A route that has its own handover for this act never walks an edge
            // written before routes existed. A route that has none (the education
            // line's 040 and 042) still needs them, or its boundary opens nothing.
            if (string.IsNullOrEmpty(state.RouteId)) return false;
            return !_edges.Values.Any(other => other.Enabled && other.Slot == "Fixed"
                && other.Act == edge.Act && other.RouteIds is { Length: > 0 }
                && other.RouteIds.Contains(state.RouteId, StringComparer.Ordinal));
        }
        return !string.IsNullOrEmpty(state.RouteId)
            && edge.RouteIds.Contains(state.RouteId, StringComparer.Ordinal);
    }

    /// <summary>
    /// A figure can close one route and stand in the middle of another. The route
    /// table names the figure each route ends on, so a terminal node only ends the
    /// run when the route has already reached its own third-act figure.
    /// </summary>
    private bool RouteStillHasActs(WesternJourneyState state)
    {
        if (string.IsNullOrEmpty(state.RouteId)) return false;
        WesternRouteDefinition? route = WesternRouteTable.Instance.Routes
            .FirstOrDefault(candidate => candidate.RouteId == state.RouteId);
        return route is not null
            && _nodes.TryGetValue(state.CurrentNodeId, out WesternGraphNode? node)
            && node.ThinkerId != route.ActThreeFigure
            && node.ThinkerId != route.ClosingFigure;
    }

    public bool IsComplete(WesternJourneyState state) => state.LastFixedAct >= 3
        && _nodes.TryGetValue(state.CurrentNodeId, out WesternGraphNode? node)
        && (state.RetainedEnding || node.Terminal && !RouteStillHasActs(state));

    public bool TryDecline(WesternJourneyState state, string slot, int expectedAct)
    {
        if (!_nodes.ContainsKey(state.CurrentNodeId) || state.LastFixedAct is < 1 or > 3 || state.RetainedEnding) return false;
        if (slot == "Question")
            return expectedAct is >= 1 and <= 3 && expectedAct <= state.LastFixedAct
                && state.ResolvedQuestionActs.Add(expectedAct);
        // A refusal ends this act's event and nothing else. A room opens at the
        // act boundary before the position has moved on, so the refused act may
        // be the one the boundary is entering; the position then moves to that
        // act and no further. A later act's slot is never spent here.
        if (slot != "Fixed" || expectedAct is < 1 or > 3
            || state.DeclinedFixedActs.Contains(expectedAct)
            || expectedAct != state.LastFixedAct && expectedAct != state.LastFixedAct + 1) return false;
        state.LastFixedAct = expectedAct;
        state.DeclinedFixedActs.Add(expectedAct);
        return true;
    }

    /// The act boundary entered an act whose fixed event is not available. The
    /// slot stays empty: the act is recorded as declined and the position moves
    /// on. This is the boundary's move, not a room refusing its own event.
    public bool TrySkipEmptyFixedSlot(WesternJourneyState state)
    {
        if (!_nodes.ContainsKey(state.CurrentNodeId) || state.RetainedEnding) return false;
        if (state.LastFixedAct is < 1 or > 2) return false;
        int act = state.LastFixedAct + 1;
        if (state.DeclinedFixedActs.Contains(act)) return false;
        state.LastFixedAct = act;
        state.DeclinedFixedActs.Add(act);
        return true;
    }

    public bool TryRetainEnding(WesternJourneyState state)
    {
        if (state.LastFixedAct < 3 || !_nodes.ContainsKey(state.CurrentNodeId) || IsComplete(state)) return false;
        state.RetainedEnding = true;
        return true;
    }

    private static string WindowKey(WesternJourneyState state, string slot) =>
        $"{state.LastFixedAct}:{slot}:{state.CurrentNodeId}:{string.Join(',', state.CompletedEdgeIds.Order(StringComparer.Ordinal))}:{string.Join(',', state.ResolvedQuestionActs.Order())}:{state.RetainedEnding}";

    public IReadOnlyList<string> OfferCandidates(WesternJourneyState state, string slot, ulong randomValue)
    {
        string key = WindowKey(state, slot);
        if (state.CandidateWindows.TryGetValue(key, out List<string>? saved) && saved is not null
            && saved.Count <= 3 && saved.Distinct(StringComparer.Ordinal).Count() == saved.Count
            && saved.All(id => _edges.TryGetValue(id, out WesternGraphEdge? edge) && IsEligible(state, edge, slot))) return saved.AsReadOnly();
        List<WesternGraphEdge> choices = GetEligibleEdges(state, slot)
            .Where(edge => CanFinishAfter(state, edge.EdgeId))
            .GroupBy(edge => _nodes[edge.ToNodeId].ThinkerId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(edge => edge.Confidence == "High")
                .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal).First())
            .ToList();
        // Preserve one high-confidence option when available; rotate peers instead of weighting hubs by their total degree.
        List<string> selected = [];
        if (choices.Count > 0)
        {
            List<WesternGraphEdge> primary = choices.Where(edge => edge.Confidence == "High").ToList();
            if (primary.Count == 0) primary = choices;
            WesternGraphEdge first = primary[(int)(randomValue % (ulong)primary.Count)];
            selected.Add(first.EdgeId);
            choices.Remove(first);
        }
        while (choices.Count > 0 && selected.Count < 3)
        {
            int index = (int)(randomValue % (ulong)choices.Count);
            selected.Add(choices[index].EdgeId);
            choices.RemoveAt(index);
            randomValue /= 3;
        }
        state.CandidateWindows[key] = selected;
        return selected.AsReadOnly();
    }

    public bool TryAcceptOffered(WesternJourneyState state, string slot, string edgeId)
    {
        string key = WindowKey(state, slot);
        return state.CandidateWindows.TryGetValue(key, out List<string>? offered)
            && offered is not null && offered.Contains(edgeId, StringComparer.Ordinal)
            && _edges.TryGetValue(edgeId, out WesternGraphEdge? edge) && edge.Slot == slot
            && TryApply(state, edgeId);
    }

    public bool CanFinish(WesternJourneyState state)
    {
        if (IsComplete(state)) return true;
        if (state.LastFixedAct == 3 && _nodes.ContainsKey(state.CurrentNodeId)) return true; // Explicit no-new-doctrine conclusion remains available.
        // Each branch consumes a fixed slot or the one question slot for that act; depth is bounded by three acts.
        if (GetEligibleEdges(state, "Fixed").Concat(GetEligibleEdges(state, "Question"))
            .Any(edge => CanFinishAfter(state, edge.EdgeId))) return true;
        // Skipping an act's fixed event means the boundary moves the position
        // on and the refusal is recorded for the act that was skipped; it does
        // not spend a later act's slot.
        if (state.LastFixedAct >= 3) return false;
        WesternJourneyState declined = CopyState(state);
        declined.LastFixedAct++;
        return TryDecline(declined, "Fixed", declined.LastFixedAct) && CanFinish(declined);
    }

    private bool CanFinishAfter(WesternJourneyState state, string edgeId)
    {
        WesternJourneyState copy = CopyState(state);
        return TryApply(copy, edgeId) && CanFinish(copy);
    }

    private static WesternJourneyState CopyState(WesternJourneyState state) => new()
        {
            CurrentNodeId = state.CurrentNodeId,
            LastFixedAct = state.LastFixedAct,
            SeenThinkerIds = new(state.SeenThinkerIds, StringComparer.Ordinal),
            CompletedEdgeIds = new(state.CompletedEdgeIds, StringComparer.Ordinal),
            ResolvedQuestionActs = new(state.ResolvedQuestionActs),
            DeclinedFixedActs = new(state.DeclinedFixedActs),
            RetainedEnding = state.RetainedEnding,
        };

    private bool IsEligible(WesternJourneyState state, WesternGraphEdge edge, string slot) => edge.Enabled && !state.RetainedEnding
        && edge.Slot == slot && edge.FromNodeId == state.CurrentNodeId
        && state.LastFixedAct is >= 1 and <= 3
        && (slot != "Fixed" || state.LastFixedAct <= 3 && !IsComplete(state))
        && edge.Act <= (slot == "Fixed" ? state.LastFixedAct + 1 : state.LastFixedAct)
        && edge.Act <= 3 && !state.CompletedEdgeIds.Contains(edge.EdgeId)
        && !state.SeenThinkerIds.Contains(_nodes[edge.ToNodeId].ThinkerId)
        && edge.RequiredEdgeIds.All(state.CompletedEdgeIds.Contains)
        && RouteMatches(edge, state)
        && !(slot == "Fixed" && edge.RouteIds is not { Length: > 0 } && !string.IsNullOrEmpty(state.RouteId)
            && _edges.Values.Any(other => other.Enabled && other.Slot == "Fixed"
                && other.FromNodeId == edge.FromNodeId && other.Act == edge.Act
                && other.RouteIds is { Length: > 0 } && RouteMatches(other, state)))
        // A question edge is spent once its act has answered. Legacy edges are
        // written for act one but carry a later handover, so the position is the
        // only act they can be matched against; the act itself is recorded when
        // the question is taken or refused.
        && (slot != "Question" || !state.ResolvedQuestionActs.Contains(state.LastFixedAct))
        && (slot != "Fixed" || edge.Confidence != "Low");

    private void Validate()
    {
        foreach (WesternGraphNode node in _nodes.Values)
            if (string.IsNullOrWhiteSpace(node.NodeId) || string.IsNullOrWhiteSpace(node.ThinkerId)
                || string.IsNullOrWhiteSpace(node.StageId) || string.IsNullOrWhiteSpace(node.DisplayName)
                || !WesternRouteCatalog.Problems.Any(problem => problem.ProblemId == node.ProblemId))
                throw new InvalidDataException("Invalid western graph node.");
        foreach (WesternGraphEdge edge in _edges.Values)
        {
            if (string.IsNullOrWhiteSpace(edge.EdgeId) || !_nodes.ContainsKey(edge.FromNodeId) || !_nodes.ContainsKey(edge.ToNodeId)
                || edge.Act is < 1 or > 3 || edge.Slot is not ("Fixed" or "Question")
                || edge.Confidence is not ("High" or "Medium" or "Low")
                || string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.RelationSummary)
                || (edge.Enabled ? !string.IsNullOrEmpty(edge.RetirementReason) : string.IsNullOrWhiteSpace(edge.RetirementReason))
                || edge.RequiredEdgeIds is null || edge.RequiredEdgeIds.Any(id => !_edges.ContainsKey(id) || id == edge.EdgeId)
                || (edge.Slot == "Fixed" && (edge.Outcome != "Adopt" || edge.Confidence == "Low"))
                || (edge.Slot == "Question" && edge.Outcome is not ("Keep" or "Revise" or "Switch")))
                throw new InvalidDataException($"Invalid western edge: {edge.EdgeId}");
            ValidateContext(edge);
        }
        HashSet<string> sampleIds = new(StringComparer.Ordinal);
        foreach (WesternGraphSample sample in Samples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId) || !sampleIds.Add(sample.SampleId) || sample.EdgeIds is null
                || sample.Disposition is not ("Executable" or "SourceOnly") || sample.ReviewNote is null
                || (sample.Disposition == "SourceOnly" && string.IsNullOrWhiteSpace(sample.ReviewNote)))
                throw new InvalidDataException("Invalid sample path.");
            WesternJourneyState state;
            try { state = Start(sample.EntryNodeId); }
            catch (InvalidOperationException exception) { throw new InvalidDataException("Invalid sample entry.", exception); }
            if (sample.Disposition == "SourceOnly")
            {
                // Keep the original projection auditable without granting it execution authority.
                bool containsRetiredEdge = false;
                foreach (string id in sample.EdgeIds)
                {
                    if (!_edges.TryGetValue(id, out WesternGraphEdge? historical) || historical.FromNodeId != state.CurrentNodeId)
                        throw new InvalidDataException($"Invalid source sample sequence: {sample.SampleId}");
                    containsRetiredEdge |= !historical.Enabled;
                    if (historical.Outcome is "Adopt" or "Switch") state.CurrentNodeId = historical.ToNodeId;
                }
                if (!containsRetiredEdge || state.CurrentNodeId != sample.TerminalNodeId)
                    throw new InvalidDataException($"Source sample must retain its original endpoint and a retired edge: {sample.SampleId}");
                continue;
            }
            state.RouteId = sample.RouteId ?? string.Empty;
            foreach (string id in sample.EdgeIds)
                if (!TryApply(state, id)) throw new InvalidDataException($"Sample {sample.SampleId} has an unreachable or repeated edge: {id}");
            if (!IsComplete(state) || state.CurrentNodeId != sample.TerminalNodeId)
                throw new InvalidDataException($"Sample {sample.SampleId} has no legal third-act ending.");
        }
    }

    private void ValidateContext(WesternGraphEdge edge)
    {
        WesternEdgeContext? context = edge.Context;
        string[] types = ["ParallelComparison", "ProblemShift", "LaterReinterpretation", "TextualCritique",
            "TraditionResponse", "InheritanceAndRevision", "DirectInfluence"];
        if (context is null || context.FromProblemId != _nodes[edge.FromNodeId].ProblemId
            || context.ToProblemId != _nodes[edge.ToNodeId].ProblemId
            || context.RelationTypes is not { Length: > 0 }
            || context.RelationTypes.Any(type => !types.Contains(type, StringComparer.Ordinal))
            || context.RelationTypes.Distinct(StringComparer.Ordinal).Count() != context.RelationTypes.Length
            || context.CompressedMediators is null
            || ((edge.RelationSummary.Contains("中介", StringComparison.Ordinal) || edge.RelationSummary.Contains("压缩", StringComparison.Ordinal))
                && string.IsNullOrWhiteSpace(context.CompressedMediators))
            || context.BehaviorRequirement != "NoAdditionalGate"
            || string.IsNullOrWhiteSpace(context.EthicalBoundary) || string.IsNullOrWhiteSpace(context.InterpretationBoundary)
            || context.EvidenceStatus != "LocalPlanning"
            || context.EvidenceSources is not { Length: > 0 }
            || context.EvidenceSources.Any(string.IsNullOrWhiteSpace)
            || !context.EvidenceSources.Contains(edge.Source, StringComparer.Ordinal))
            throw new InvalidDataException($"Invalid western edge context: {edge.EdgeId}");
        // An explicit source caveat must not be silently upgraded to direct influence.
        if (context.RelationTypes.Contains("DirectInfluence", StringComparer.Ordinal)
            && new[] { "直接影响未建立", "不声称直接", "不主张直接" }.Any(edge.RelationSummary.Contains))
            throw new InvalidDataException($"Direct influence contradicts the recorded source: {edge.EdgeId}");
    }
}
