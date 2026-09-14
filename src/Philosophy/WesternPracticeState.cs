namespace STS2Philosophers;

internal enum WesternPracticeCardKind { Other, Attack, Skill, Power }

internal readonly record struct WesternPracticeReward(int Energy = 0, int Draw = 0, int Block = 0);

// Entry exercises are game interpretations, not complete accounts of the thinkers.
// The caller supplies owner-only card facts and an identity for each distinct play.
internal sealed class WesternPracticeState
{
    public string ProblemId { get; set; } = string.Empty;
    public int Turn { get; set; }
    public bool Closed { get; set; }
    public int PreviousCards { get; set; }
    public List<WesternPracticeCardKind> PreviousKinds { get; set; } = [];
    public List<WesternPracticePlay> Plays { get; set; } = [];
    public int PendingTurn { get; set; }
    public WesternPracticeReward PendingReward { get; set; }
    public int LastClaimedTurn { get; set; }
    public int SuccessfulTurns { get; set; }
    public int BrokenTurns { get; set; }

    public bool BeginTurn(int turn)
    {
        if (turn <= 0 || turn <= Turn) return false;
        // A missing close or skipped turn is not evidence of a completed practice.
        PreviousCards = Closed && turn == Turn + 1 ? Plays.Count : 0;
        PreviousKinds = Closed && turn == Turn + 1 ? Plays.Select(play => play.Kind).ToList() : [];
        Plays = [];
        Turn = turn;
        Closed = false;
        if (PendingTurn != turn) ClearPending();
        return true;
    }

    public bool RecordPlay(int turn, string playId, string cardModelId, WesternPracticeCardKind kind, bool automatic = false)
    {
        if (Turn <= 0 || turn != Turn || Closed || string.IsNullOrWhiteSpace(playId)
            || string.IsNullOrWhiteSpace(cardModelId) || !Enum.IsDefined(kind)
            || Plays.Any(play => play.PlayId == playId)) return false;
        Plays.Add(new(playId, cardModelId, kind, automatic));
        return true;
    }

    public bool CloseTurn(int turn)
    {
        if (Turn <= 0 || turn != Turn || Closed) return false;
        Closed = true;
        ClearPending();
        WesternPracticeReward reward = Evaluate();
        if (reward != default)
        {
            PendingTurn = Turn + 1;
            PendingReward = reward;
            SuccessfulTurns++;
        }
        else if (WesternRouteCatalog.Problems.Any(problem => problem.ProblemId == ProblemId))
        {
            BrokenTurns++;
        }
        return true;
    }

    public WesternPracticeReward TakeReward(int turn)
    {
        if (turn != Turn || Closed || PendingTurn != turn || turn <= LastClaimedTurn) return default;
        WesternPracticeReward result = PendingReward;
        LastClaimedTurn = turn;
        ClearPending();
        return result;
    }

    public void EndCombat()
    {
        Turn = 0;
        Closed = false;
        PreviousCards = 0;
        PreviousKinds = [];
        Plays = [];
        LastClaimedTurn = 0;
        ClearPending();
        // Aggregate practice history can inform later candidates; it is not a buff.
    }

    private WesternPracticeReward Evaluate()
    {
        int count = Plays.Count;
        int attacks = Plays.Count(play => play.Kind == WesternPracticeCardKind.Attack);
        int skills = Plays.Count(play => play.Kind == WesternPracticeCardKind.Skill);
        int powers = Plays.Count(play => play.Kind == WesternPracticeCardKind.Power);
        if (count == 0) return default;
        bool qualifies = ProblemId switch
        {
            "BEING_AND_CHANGE" => count >= 3 && attacks + skills == count
                && Plays.Zip(Plays.Skip(1), (a, b) => a.Kind != b.Kind).All(changed => changed),
            "KNOWLEDGE_AND_DOUBT" => count >= 3 && Plays.Select(play => play.CardModelId).Distinct(StringComparer.Ordinal).Count() == count,
            "VIRTUE_AND_HAPPINESS" => count == 4 && attacks == 2 && skills == 2,
            "FREEDOM_AND_INSTITUTIONS" => count == 3 && attacks == 1 && skills == 1 && powers == 1,
            "SELF_AND_OTHER" => count is >= 2 and <= 4 && Plays[0].Kind == WesternPracticeCardKind.Skill
                && attacks >= 1 && Plays.All(play => !play.Automatic),
            "LANGUAGE_AND_MEANING" => count == 4 && new[] { attacks, skills, powers }.Count(n => n == 2) == 2,
            "HISTORY_AND_POWER" => PreviousCards > count,
            _ => false,
        };
        if (!qualifies) return default;
        return RewardFor(ProblemId);
    }

    internal static WesternPracticeReward RewardFor(string problemId)
    {
        return problemId switch
        {
            "BEING_AND_CHANGE" or "HISTORY_AND_POWER" => new(Energy: 1),
            "KNOWLEDGE_AND_DOUBT" => new(Draw: 1),
            "VIRTUE_AND_HAPPINESS" => new(Block: 6),
            "FREEDOM_AND_INSTITUTIONS" => new(Energy: 1, Block: 3),
            "SELF_AND_OTHER" => new(Draw: 1, Block: 3),
            "LANGUAGE_AND_MEANING" => new(Draw: 1, Block: 2),
            _ => default,
        };
    }

    private void ClearPending()
    {
        PendingTurn = 0;
        PendingReward = default;
    }
}

internal sealed record WesternPracticePlay(string PlayId, string CardModelId, WesternPracticeCardKind Kind, bool Automatic);
