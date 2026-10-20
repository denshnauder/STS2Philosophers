using System.Text.Json;

namespace STS2Philosophers;

// Saved with SocratesBalanceWeight, because the route retains that actual relic.
// An invalid payload stays inert and is preserved verbatim by the relic host.
internal sealed class PlotinusCombatPracticeState
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public int Turn { get; set; }
    public bool Closed { get; set; }
    public bool InvalidTurn { get; set; }
    public List<WesternPracticePlay> Plays { get; set; } = [];

    public bool IsValid() => Version == CurrentVersion && Turn >= 0
        && Plays is not null && Plays.Count <= 3
        && (Turn > 0 || !Closed && !InvalidTurn && Plays.Count == 0)
        && Plays.All(play => play is not null && !string.IsNullOrWhiteSpace(play.PlayId)
            && !string.IsNullOrWhiteSpace(play.CardModelId)
            && !play.Automatic && Enum.IsDefined(play.Kind))
        && Plays.Select(play => play.PlayId).Distinct(StringComparer.Ordinal).Count() == Plays.Count;

    public void BeginCombat()
    {
        Turn = 0;
        Closed = false;
        InvalidTurn = false;
        Plays = [];
    }

    public bool BeginTurn(int turn)
    {
        if (turn <= Turn || turn <= 0) return false;
        Turn = turn;
        Closed = false;
        InvalidTurn = false;
        Plays = [];
        return true;
    }

    public bool RecordPlay(int turn, string cardModelId, WesternPracticeCardKind kind, bool automatic)
    {
        if (turn != Turn || turn <= 0 || Closed || InvalidTurn) return false;
        if (automatic || kind == WesternPracticeCardKind.Other || !Enum.IsDefined(kind)
            || string.IsNullOrWhiteSpace(cardModelId) || Plays.Count >= 3)
        {
            InvalidTurn = true;
            return false;
        }
        Plays.Add(new($"Plotinus:{turn}:{Plays.Count}", cardModelId, kind, false));
        return true;
    }

    public int CloseTurn(int turn)
    {
        if (turn != Turn || turn <= 0 || Closed) return 0;
        Closed = true;
        return InvalidTurn ? 0 : WesternSocialPracticePolicy.Evaluate(
            "VIRTUE_AND_HAPPINESS", "PLOTINUS", Plays).Block;
    }

    public string Encode() => JsonSerializer.Serialize(this);

    public static bool TryRestore(string payload, out PlotinusCombatPracticeState state)
    {
        state = new();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            PlotinusCombatPracticeState? restored = JsonSerializer.Deserialize<PlotinusCombatPracticeState>(payload);
            if (restored is null || !restored.IsValid()) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
