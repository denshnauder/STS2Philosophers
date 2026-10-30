using System.Text.Json.Serialization;

namespace STS2Philosophers;

/// <summary>
/// The record of the first act's question. It holds nothing but whether the
/// question was answered: a fork a run left open must still be there when the
/// save is loaded, or the line it was about to enter is lost for that run.
/// </summary>
internal sealed class RouteTurnQuestionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public bool Closed { get; set; }

    public static RouteTurnQuestionState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable() => Version == CurrentVersion && !string.IsNullOrWhiteSpace(InstanceId);
}
