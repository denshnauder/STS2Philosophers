using System.Text;
using System.Text.Json;

namespace STS2Philosophers;

internal static class PhilosophyRunStateCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public static string Encode(PhilosophyRunState state)
    {
        string json = JsonSerializer.Serialize(state, JsonOptions);
        return Convert.ToHexString(Encoding.UTF8.GetBytes(json));
    }

    public static PhilosophyRunState Decode(string encoded)
    {
        byte[] bytes = Convert.FromHexString(encoded);
        PhilosophyRunState state = JsonSerializer.Deserialize<PhilosophyRunState>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The philosophy run state payload was empty.");
        state.NormalizeAfterLoad();
        return state;
    }
}
