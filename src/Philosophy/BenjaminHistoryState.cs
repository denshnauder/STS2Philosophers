using System.Text.Json.Serialization;

namespace STS2Philosophers;

/// <summary>
/// Benjamin's independent second-act perspective after Foucault's fixed room.
/// It adds no map edge and does not reopen Foucault's question; the earlier
/// nested page inside Foucault's result stays for old saves.
/// </summary>
internal sealed class BenjaminHistoryState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 2;
    public bool Closed { get; set; }

    public static BenjaminHistoryState CreateAt(string instanceId, int actIndex) =>
        new() { InstanceId = instanceId, ActIndex = actIndex };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        if (ActIndex is < 1 or > 3) ActIndex = 2;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable() =>
        Version == CurrentVersion && !string.IsNullOrWhiteSpace(InstanceId)
        && ActIndex is >= 1 and <= 3;
}
