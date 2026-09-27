using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

public sealed class PhilosophyRunStateRitsuPayload
{
    public const int CurrentSchemaVersion = PhilosophyRunStateRitsuContract.SchemaVersion;

    private string _encodedState = string.Empty;

    [JsonIgnore]
    internal PhilosophyRunState? LiveState { get; private set; }

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string EncodedState
    {
        get
        {
            if (LiveState is null)
            {
                return _encodedState;
            }

            return LiveState.HasData
                ? PhilosophyRunStateCodec.Encode(LiveState)
                : string.Empty;
        }
        set => _encodedState = value ?? string.Empty;
    }

    internal static PhilosophyRunStateRitsuPayload Bind(PhilosophyRunState state)
    {
        PhilosophyRunStateRitsuPayload payload = new();
        payload.BindLiveState(state);
        return payload;
    }

    internal void BindLiveState(PhilosophyRunState state)
    {
        LiveState = state;
    }

    internal bool TryDecode(out PhilosophyRunState? state)
    {
        state = null;
        if (SchemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(_encodedState))
        {
            return false;
        }

        try
        {
            PhilosophyRunState decoded = PhilosophyRunStateCodec.Decode(_encodedState);
            if (!CanPersist(decoded))
            {
                return false;
            }

            state = decoded;
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or InvalidDataException or InvalidOperationException or
            ArgumentException or OverflowException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool CanPersist(PhilosophyRunState state)
    {
        return state.PreservedSaveMarkerEntries.Count == 0 &&
               state.ZenoRoutePayload.Classification is
                   ZenoRoutePayloadClassification.PreFeatureLegacy or
                   ZenoRoutePayloadClassification.Current;
    }
}
