namespace STS2Philosophers;

internal enum SocratesCommitmentMode { None, Guard, Clear }
internal enum SocratesCommitmentPhase { AwaitingTurn, Opening, Choosing, Acting, Closed, Ended }
internal enum SocratesCommitmentObservation { NotObserved, NoPractice, NoCondition, Incomplete, Unmet, Met }

// C3 in-memory protocol only. No engine hooks, save migration or philosophical score.
// The adapter must provide a stable combat identity and increasing normal-turn identities.
internal sealed partial class SocratesBoundedCommitmentState
{
    private HashSet<string> _attackers = new(StringComparer.Ordinal);
    private int _pendingDraw;

    public SocratesBoundedCommitmentState(string combatId)
    {
        if (string.IsNullOrWhiteSpace(combatId)) throw new ArgumentException("Combat identity is required.", nameof(combatId));
        CombatId = combatId;
    }

    public string CombatId { get; }
    public int Turn { get; private set; }
    public SocratesCommitmentPhase Phase { get; private set; }
    public SocratesCommitmentMode Mode { get; private set; }
    public bool Declined { get; private set; }
    public bool DecisionMade { get; private set; }
    public bool Revised { get; private set; }
    public int? PublicIncoming { get; private set; }
    public int InitialBlock { get; private set; }
    public int? GuardTarget => PublicIncoming is int n ? n / 2 + n % 2 : null;
    public bool GuardOpportunity => GuardTarget is int target && target > InitialBlock;
    public SocratesCommitmentObservation Observation { get; private set; }
    public IReadOnlyList<string> FrozenAttackers => _attackers.Order(StringComparer.Ordinal).ToArray();
    public bool InvitationAvailable => Phase == SocratesCommitmentPhase.Choosing &&
        Mode == SocratesCommitmentMode.None && !Declined && _attackers.Count > 0;

    private bool Matches(string combatId, int turn) => combatId == CombatId && turn == Turn && turn > 0;

    public bool OpenNormalTurn(string combatId, int turn)
    {
        if (combatId != CombatId || Phase == SocratesCommitmentPhase.Ended || turn <= Turn) return false;
        // Unclaimed rewards expire when their receiving normal turn was passed.
        if (Phase != SocratesCommitmentPhase.Closed) _pendingDraw = 0;
        Turn = turn;
        Phase = SocratesCommitmentPhase.Opening;
        DecisionMade = Revised = false;
        PublicIncoming = null;
        InitialBlock = 0;
        _attackers.Clear();
        Observation = SocratesCommitmentObservation.NotObserved;
        return true;
    }

    public int TakeOpeningDraw(string combatId, int turn)
    {
        if (!Matches(combatId, turn) || Phase != SocratesCommitmentPhase.Opening) return 0;
        int result = _pendingDraw;
        _pendingDraw = 0;
        return result;
    }

    public bool FreezeFacts(string combatId, int turn, int? publicIncoming, int initialBlock,
        IReadOnlyList<string>? publicAttackers)
    {
        if (!Matches(combatId, turn) || Phase != SocratesCommitmentPhase.Opening || _pendingDraw != 0 ||
            publicIncoming < 0 || initialBlock < 0 || publicAttackers is null ||
            publicAttackers.Any(string.IsNullOrWhiteSpace) || publicAttackers.Distinct(StringComparer.Ordinal).Count() != publicAttackers.Count ||
            (publicIncoming > 0 && publicAttackers.Count == 0)) return false;
        PublicIncoming = publicIncoming;
        InitialBlock = initialBlock;
        _attackers = new(publicAttackers, StringComparer.Ordinal);
        Phase = SocratesCommitmentPhase.Choosing;
        return true;
    }

    public bool Select(string combatId, int turn, SocratesCommitmentMode mode)
    {
        if (!Matches(combatId, turn) || Phase != SocratesCommitmentPhase.Choosing || DecisionMade || Declined ||
            !Enum.IsDefined(mode) || mode == SocratesCommitmentMode.None || mode == Mode ||
            (Mode == SocratesCommitmentMode.None && !InvitationAvailable)) return false;
        Revised = Mode != SocratesCommitmentMode.None;
        Mode = mode;
        DecisionMade = true;
        return true;
    }

    public bool DeclineBattle(string combatId, int turn)
    {
        if (!Matches(combatId, turn) || !InvitationAvailable || DecisionMade) return false;
        Declined = DecisionMade = true;
        return true;
    }

    public bool LockActions(string combatId, int turn)
    {
        if (!Matches(combatId, turn) || Phase != SocratesCommitmentPhase.Choosing || InvitationAvailable) return false;
        Phase = SocratesCommitmentPhase.Acting;
        return true;
    }

    public bool CloseNormalTurn(string combatId, int turn, int finalBlock,
        IReadOnlyList<string>? confirmedDefeated, bool targetEvidenceComplete = true)
    {
        if (!Matches(combatId, turn) || Phase != SocratesCommitmentPhase.Acting || finalBlock < 0 ||
            (confirmedDefeated is not null && confirmedDefeated.Any(string.IsNullOrWhiteSpace))) return false;
        Observation = Mode switch
        {
            SocratesCommitmentMode.None => SocratesCommitmentObservation.NoPractice,
            SocratesCommitmentMode.Guard when PublicIncoming is null => SocratesCommitmentObservation.Incomplete,
            SocratesCommitmentMode.Guard when !GuardOpportunity => SocratesCommitmentObservation.NoCondition,
            SocratesCommitmentMode.Guard => finalBlock >= GuardTarget!.Value ?
                SocratesCommitmentObservation.Met : SocratesCommitmentObservation.Unmet,
            SocratesCommitmentMode.Clear when _attackers.Count == 0 => SocratesCommitmentObservation.NoCondition,
            SocratesCommitmentMode.Clear when !targetEvidenceComplete || confirmedDefeated is null => SocratesCommitmentObservation.Incomplete,
            SocratesCommitmentMode.Clear => confirmedDefeated!.Any(_attackers.Contains) ?
                SocratesCommitmentObservation.Met : SocratesCommitmentObservation.Unmet,
            _ => SocratesCommitmentObservation.Incomplete
        };
        _pendingDraw = !Revised && Observation == SocratesCommitmentObservation.Met ? 1 : 0;
        Phase = SocratesCommitmentPhase.Closed;
        return true;
    }

    // Required on combat end or player death, including before the next normal turn.
    public bool EndCombat(string combatId)
    {
        if (combatId != CombatId || Phase == SocratesCommitmentPhase.Ended) return false;
        _pendingDraw = 0;
        _attackers.Clear();
        Phase = SocratesCommitmentPhase.Ended;
        return true;
    }
}
