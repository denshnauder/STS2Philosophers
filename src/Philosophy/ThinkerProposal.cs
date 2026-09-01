using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed class ThinkerProposal
{
    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("thinker_id")]
    public string ThinkerId { get; set; } = string.Empty;

    [JsonPropertyName("doctrine_id")]
    public string DoctrineId { get; set; } = string.Empty;

    [JsonPropertyName("relic_model_id")]
    public string RelicModelId { get; set; } = string.Empty;

    [JsonPropertyName("route_tags")]
    public List<string> RouteTags { get; set; } = [];

    [JsonPropertyName("qualification_rule_ids")]
    public List<string> QualificationRuleIds { get; set; } = [];

    [JsonPropertyName("resonance_tags")]
    public List<string> ResonanceTags { get; set; } = [];
}

internal static class ThinkerProposalCatalog
{
    public const string KongziLi = "KONGZI_LI";
    public const string KongziRen = "KONGZI_REN";
    public const string MoziJianAi = "MOZI_JIAN_AI";
    public const string MoziFeiGong = "MOZI_FEI_GONG";
    public const string LaoziWuWei = "LAOZI_WU_WEI";
    public const string LaoziRuoShui = "LAOZI_RUO_SHUI";

    private const string EmbeddedResourceName = "STS2Philosophers.config.thinker_proposals.json";
    private static readonly Lazy<IReadOnlyDictionary<string, ThinkerProposal>> Proposals = new(LoadEmbedded);

    public static ThinkerProposal Get(string proposalId)
    {
        return Proposals.Value.TryGetValue(proposalId, out ThinkerProposal? proposal)
            ? proposal
            : throw new KeyNotFoundException($"Unknown thinker proposal: {proposalId}");
    }

    internal static IReadOnlyDictionary<string, ThinkerProposal> ParseJson(string json)
    {
        List<ThinkerProposal> proposals = JsonSerializer.Deserialize<List<ThinkerProposal>>(json)
            ?? throw new InvalidDataException("The thinker proposal configuration was empty.");
        Dictionary<string, ThinkerProposal> byId = new(StringComparer.Ordinal);
        foreach (ThinkerProposal proposal in proposals)
        {
            Validate(proposal);
            if (!byId.TryAdd(proposal.ProposalId, proposal))
            {
                throw new InvalidDataException($"Duplicate thinker proposal id: {proposal.ProposalId}");
            }
        }

        return byId;
    }

    private static IReadOnlyDictionary<string, ThinkerProposal> LoadEmbedded()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Missing embedded thinker proposal configuration: {EmbeddedResourceName}");
        using StreamReader reader = new(stream);
        return ParseJson(reader.ReadToEnd());
    }

    private static void Validate(ThinkerProposal proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.ProposalId)
            || string.IsNullOrWhiteSpace(proposal.ThinkerId)
            || string.IsNullOrWhiteSpace(proposal.DoctrineId)
            || string.IsNullOrWhiteSpace(proposal.RelicModelId))
        {
            throw new InvalidDataException("Each thinker proposal requires proposal, thinker, doctrine, and relic ids.");
        }

        proposal.RouteTags ??= [];
        proposal.QualificationRuleIds ??= [];
        proposal.ResonanceTags ??= [];
    }
}
