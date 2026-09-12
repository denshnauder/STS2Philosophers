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
    [property: JsonPropertyName("enabled")] bool Enabled);

internal sealed record WesternGraphSample(
    [property: JsonPropertyName("sample_id")] string SampleId,
    [property: JsonPropertyName("entry_node_id")] string EntryNodeId,
    [property: JsonPropertyName("edge_ids")] string[] EdgeIds,
    [property: JsonPropertyName("terminal_node_id")] string TerminalNodeId);

internal sealed class WesternJourneyState
{
    public string CurrentNodeId { get; set; } = string.Empty;
    public int LastFixedAct { get; set; }
    public HashSet<string> SeenThinkerIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> CompletedEdgeIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<int> ResolvedQuestionActs { get; set; } = [];
    public Dictionary<string, List<string>> CandidateWindows { get; set; } = new(StringComparer.Ordinal);
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
        if (data.SchemaVersion != 1 || data.Nodes is null || data.Edges is null || data.Samples is null
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
        return new() { CurrentNodeId = entryNodeId, LastFixedAct = 1, SeenThinkerIds = [node.ThinkerId] };
    }

    public WesternGraphNode GetNode(string nodeId) => _nodes[nodeId];
    public WesternGraphEdge GetEdge(string edgeId) => _edges[edgeId];

    public IReadOnlyList<WesternGraphEdge> GetEligibleEdges(WesternJourneyState state, string slot)
    {
        if (!_nodes.ContainsKey(state.CurrentNodeId) || state.LastFixedAct is < 1 or > 3) return [];
        return _edges.Values.Where(edge => IsEligible(state, edge, slot)).OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray();
    }

    public bool TryApply(WesternJourneyState state, string edgeId)
    {
        if (!_edges.TryGetValue(edgeId, out WesternGraphEdge? edge) || !IsEligible(state, edge, edge.Slot)) return false;
        state.CompletedEdgeIds.Add(edgeId);
        state.SeenThinkerIds.Add(_nodes[edge.ToNodeId].ThinkerId);
        if (edge.Slot == "Question") state.ResolvedQuestionActs.Add(edge.Act);
        else state.LastFixedAct = edge.Act;
        if (edge.Outcome is "Adopt" or "Switch") state.CurrentNodeId = edge.ToNodeId;
        return true;
    }

    public bool IsComplete(WesternJourneyState state) => state.LastFixedAct == 3
        && _nodes.TryGetValue(state.CurrentNodeId, out WesternGraphNode? node) && node.Terminal;

    public IReadOnlyList<string> OfferCandidates(WesternJourneyState state, string slot, ulong randomValue)
    {
        string key = $"{state.LastFixedAct}:{slot}:{state.CurrentNodeId}:{string.Join(',', state.CompletedEdgeIds.Order(StringComparer.Ordinal))}";
        if (state.CandidateWindows.TryGetValue(key, out List<string>? saved)) return saved.AsReadOnly();
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
        string key = $"{state.LastFixedAct}:{slot}:{state.CurrentNodeId}:{string.Join(',', state.CompletedEdgeIds.Order(StringComparer.Ordinal))}";
        return state.CandidateWindows.TryGetValue(key, out List<string>? offered)
            && offered.Contains(edgeId, StringComparer.Ordinal)
            && _edges.TryGetValue(edgeId, out WesternGraphEdge? edge) && edge.Slot == slot
            && TryApply(state, edgeId);
    }

    public bool CanFinish(WesternJourneyState state)
    {
        if (IsComplete(state)) return true;
        // Each branch consumes a fixed slot or the one question slot for that act; depth is bounded by three acts.
        return GetEligibleEdges(state, "Fixed").Concat(GetEligibleEdges(state, "Question"))
            .Any(edge => CanFinishAfter(state, edge.EdgeId));
    }

    private bool CanFinishAfter(WesternJourneyState state, string edgeId)
    {
        WesternJourneyState copy = new()
        {
            CurrentNodeId = state.CurrentNodeId,
            LastFixedAct = state.LastFixedAct,
            SeenThinkerIds = new(state.SeenThinkerIds, StringComparer.Ordinal),
            CompletedEdgeIds = new(state.CompletedEdgeIds, StringComparer.Ordinal),
            ResolvedQuestionActs = new(state.ResolvedQuestionActs),
        };
        return TryApply(copy, edgeId) && CanFinish(copy);
    }

    private bool IsEligible(WesternJourneyState state, WesternGraphEdge edge, string slot) => edge.Enabled
        && edge.Slot == slot && edge.FromNodeId == state.CurrentNodeId
        && edge.Act == (slot == "Fixed" ? state.LastFixedAct + 1 : state.LastFixedAct)
        && edge.Act <= 3 && !state.CompletedEdgeIds.Contains(edge.EdgeId)
        && !state.SeenThinkerIds.Contains(_nodes[edge.ToNodeId].ThinkerId)
        && edge.RequiredEdgeIds.All(state.CompletedEdgeIds.Contains)
        && (slot != "Question" || !state.ResolvedQuestionActs.Contains(edge.Act))
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
                || edge.RequiredEdgeIds is null || edge.RequiredEdgeIds.Any(id => !_edges.ContainsKey(id) || id == edge.EdgeId)
                || (edge.Slot == "Fixed" && (edge.Outcome != "Adopt" || edge.Confidence == "Low"))
                || (edge.Slot == "Question" && edge.Outcome is not ("Keep" or "Revise" or "Switch")))
                throw new InvalidDataException($"Invalid western edge: {edge.EdgeId}");
        }
        HashSet<string> sampleIds = new(StringComparer.Ordinal);
        foreach (WesternGraphSample sample in Samples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId) || !sampleIds.Add(sample.SampleId) || sample.EdgeIds is null)
                throw new InvalidDataException("Invalid sample path.");
            WesternJourneyState state;
            try { state = Start(sample.EntryNodeId); }
            catch (InvalidOperationException exception) { throw new InvalidDataException("Invalid sample entry.", exception); }
            foreach (string id in sample.EdgeIds)
                if (!TryApply(state, id)) throw new InvalidDataException($"Sample {sample.SampleId} has an unreachable or repeated edge: {id}");
            if (!IsComplete(state) || state.CurrentNodeId != sample.TerminalNodeId)
                throw new InvalidDataException($"Sample {sample.SampleId} has no legal third-act ending.");
        }
    }
}
