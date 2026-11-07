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

    // The question is asked in the first act only. A record that claims another
    // act is not a question left open — and an open record keeps every later
    // question closed, so it must not be trusted.
    public int Act { get; set; }

    public static RouteTurnQuestionState Create(string instanceId) =>
        new() { InstanceId = instanceId, Act = 1 };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        // Records written before the act was recorded belong to the first act;
        // anything that names another act is not a question left open.
        if (Act <= 0) Act = 1;
        else if (Act != 1) Closed = true;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable() => Version == CurrentVersion && !string.IsNullOrWhiteSpace(InstanceId);
}
