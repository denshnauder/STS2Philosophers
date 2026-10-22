namespace STS2Philosophers;

internal enum WesternOpeningStage { NotStarted, Choosing, Committed, Completed, Failed }

internal enum WesternEntryCostEffect
{
    None,
    PracticeDiscount,
    LawDiscount,
    LawSurcharge,
    OtherResponse,
    CategoryDiscount,
    ConventionDiscount,
}

internal readonly record struct WesternEntryCostChange(decimal Amount, WesternEntryCostEffect Effect)
{
    public decimal Apply(decimal cost) => Math.Max(0m, cost + Amount);
}

internal readonly record struct WesternEntryPlayResolution(
    bool Counted = false,
    bool DrawOne = false,
    bool ExhaustPlayedCard = false,
    bool ApplyEnemyStrength = false);

internal readonly record struct WesternEntryRetainResolution(
    bool Applied = false,
    bool RetainCurrent = false,
    bool RetainPractice = false);

// The seven entry relics share persistence and callback gates, but keep separate rule state.
// Card-instance and enemy-instance matching stays with the game-facing relic; this class stores
// the stable facts needed to suspend safely when an instance cannot be uniquely restored.
internal sealed class WesternEntryPracticeState
{
    public const string BeingAndChange = "BEING_AND_CHANGE";
    public const string KnowledgeAndDoubt = "KNOWLEDGE_AND_DOUBT";
    public const string VirtueAndHappiness = "VIRTUE_AND_HAPPINESS";
    public const string FreedomAndInstitutions = "FREEDOM_AND_INSTITUTIONS";
    public const string SelfAndOther = "SELF_AND_OTHER";
    public const string LanguageAndMeaning = "LANGUAGE_AND_MEANING";
    public const string HistoryAndPower = "HISTORY_AND_POWER";

    private static readonly HashSet<string> KnownProblems =
    [
        BeingAndChange,
        KnowledgeAndDoubt,
        VirtueAndHappiness,
        FreedomAndInstitutions,
        SelfAndOther,
        LanguageAndMeaning,
        HistoryAndPower,
    ];

    public string ProblemId { get; set; } = string.Empty;
    public bool CombatActive { get; set; }
    public int Turn { get; set; }
    public bool TurnClosed { get; set; }
    public int RetainsResolvedTurn { get; set; }

    public WesternOpeningStage OpeningStage { get; set; }
    public List<string> OpeningCardModelIds { get; set; } = [];

    public string CurrentCardModelId { get; set; } = string.Empty;
    // Nullable distinguishes old version-1 saves from this additive Heraclitus rule.
    public int? FlowRuleVersion { get; set; }
    public bool FlowDrawClaimed { get; set; }
    public WesternPracticeCardKind FlowCardKind { get; set; }
    public int FlowPlayedKinds { get; set; }
    public string PracticeCardModelId { get; set; } = string.Empty;
    public int PracticeWaits { get; set; }

    public WesternPracticeCardKind LawKind { get; set; }
    public bool LawDiscountUsed { get; set; }
    public bool LawSurchargeUsed { get; set; }

    public uint? OtherCombatId { get; set; }
    public int OtherBoundTurn { get; set; }
    public int OtherIntentTurn { get; set; }
    public bool? OtherIntendsAttack { get; set; }
    public bool OtherResponseUsed { get; set; }

    public string CategoryCardModelId { get; set; } = string.Empty;
    public int CategoryBucket { get; set; } = -1;
    public int CategoryDiscountsRemaining { get; set; }

    public string ConventionCardModelId { get; set; } = string.Empty;
    public int ConventionFormedTurn { get; set; }
    public bool ConventionUsed { get; set; }
    public Dictionary<string, int> TurnCardCounts { get; set; } = new(StringComparer.Ordinal);

    public List<string> RecordedPlayIds { get; set; } = [];

    public static bool IsKnownProblem(string problemId) => KnownProblems.Contains(problemId);

    public void BeginCombat(string problemId)
    {
        ResetCombatState();
        ProblemId = IsKnownProblem(problemId) ? problemId : string.Empty;
        CombatActive = ProblemId.Length > 0;
    }

    public bool BeginTurn(int turn)
    {
        if (!CombatActive || turn <= 0 || turn <= Turn) return false;
        Turn = turn;
        TurnClosed = false;
        RetainsResolvedTurn = 0;
        CurrentCardModelId = string.Empty;
        FlowRuleVersion = ProblemId == BeingAndChange ? 1 : null;
        FlowDrawClaimed = false;
        FlowCardKind = WesternPracticeCardKind.Other;
        FlowPlayedKinds = 0;
        LawDiscountUsed = false;
        LawSurchargeUsed = false;
        OtherIntentTurn = 0;
        OtherIntendsAttack = null;
        OtherResponseUsed = false;
        CategoryCardModelId = string.Empty;
        CategoryBucket = -1;
        CategoryDiscountsRemaining = 0;
        ConventionUsed = false;
        TurnCardCounts = new(StringComparer.Ordinal);
        RecordedPlayIds = [];
        return true;
    }

    public bool TryBeginOpeningChoice()
    {
        if (!CombatActive || Turn != 1 || TurnClosed || OpeningStage != WesternOpeningStage.NotStarted
            || ProblemId is not (KnowledgeAndDoubt or VirtueAndHappiness)) return false;
        OpeningStage = WesternOpeningStage.Choosing;
        return true;
    }

    public bool TryCommitOpeningChoice(IEnumerable<string> cardModelIds)
    {
        if (OpeningStage != WesternOpeningStage.Choosing) return false;
        List<string> selected = cardModelIds.ToList();
        int maximum = ProblemId == KnowledgeAndDoubt ? 2 : ProblemId == VirtueAndHappiness ? 1 : -1;
        if (maximum < 0 || selected.Count > maximum || selected.Any(string.IsNullOrWhiteSpace)) return false;
        OpeningCardModelIds = selected;
        OpeningStage = WesternOpeningStage.Committed;
        return true;
    }

    public bool TryCompleteOpeningChoice()
    {
        if (OpeningStage != WesternOpeningStage.Committed) return false;
        if (ProblemId == VirtueAndHappiness && OpeningCardModelIds.Count == 1)
        {
            PracticeCardModelId = OpeningCardModelIds[0];
            PracticeWaits = 0;
        }
        OpeningCardModelIds = [];
        OpeningStage = WesternOpeningStage.Completed;
        return true;
    }

    public bool FailOpeningChoice()
    {
        if (OpeningStage is not (WesternOpeningStage.Choosing or WesternOpeningStage.Committed)) return false;
        OpeningCardModelIds = [];
        OpeningStage = WesternOpeningStage.Failed;
        return true;
    }

    public bool ObserveDraw(string cardModelId, WesternPracticeCardKind kind, bool alreadyExhausts)
    {
        if (ProblemId != BeingAndChange || !CanAct() || FlowRuleVersion != 1 || FlowDrawClaimed
            || RetainsResolvedTurn == Turn
            || string.IsNullOrWhiteSpace(cardModelId) || alreadyExhausts
            || kind is not (WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)) return false;
        CurrentCardModelId = cardModelId;
        FlowDrawClaimed = true;
        FlowCardKind = kind;
        return true;
    }

    public bool CanRetainCurrent => ProblemId == BeingAndChange && CanAct()
        && FlowRuleVersion == 1 && FlowDrawClaimed && CurrentCardModelId.Length > 0;

    public bool CanReleaseCurrent => CanRetainCurrent && (FlowCardKind switch
    {
        WesternPracticeCardKind.Attack => (FlowPlayedKinds & 2) != 0,
        WesternPracticeCardKind.Skill => (FlowPlayedKinds & 1) != 0,
        _ => false,
    });

    // Old combat records cannot prove a once-per-turn claim or an opposing play.
    // Suspend only this turn's marker; the next genuine BeginTurn starts the new rule.
    internal void SuspendLegacyFlow()
    {
        if (ProblemId != BeingAndChange || FlowRuleVersion is not null) return;
        CurrentCardModelId = string.Empty;
        FlowCardKind = WesternPracticeCardKind.Other;
        FlowPlayedKinds = 0;
        FlowDrawClaimed = CombatActive && Turn > 0;
        FlowRuleVersion = FlowDrawClaimed ? 1 : null;
    }

    public bool ObserveOtherIntent(uint combatId, bool intendsAttack)
    {
        if (ProblemId != SelfAndOther || !CanAct() || OtherCombatId != combatId
            || Turn <= OtherBoundTurn || OtherIntentTurn == Turn) return false;
        OtherIntentTurn = Turn;
        OtherIntendsAttack = intendsAttack;
        return true;
    }

    public bool ForgetOther(uint combatId)
    {
        if (ProblemId != SelfAndOther || OtherCombatId != combatId) return false;
        OtherCombatId = null;
        OtherBoundTurn = 0;
        OtherIntentTurn = 0;
        OtherIntendsAttack = null;
        OtherResponseUsed = false;
        return true;
    }

    public WesternEntryCostChange GetCostChange(
        string cardModelId,
        WesternPracticeCardKind kind,
        decimal costBeforeThisRelic,
        bool costsX,
        bool trackedPractice,
        uint? targetCombatId = null,
        bool namedCategory = false)
    {
        if (!CanAct() || string.IsNullOrWhiteSpace(cardModelId) || !Enum.IsDefined(kind)
            || costBeforeThisRelic < 0m || costsX) return default;

        if (ProblemId == VirtueAndHappiness && trackedPractice && PracticeCardModelId == cardModelId
            && PracticeWaits > 0 && costBeforeThisRelic > 0m)
            return new(-Math.Min(costBeforeThisRelic, PracticeWaits), WesternEntryCostEffect.PracticeDiscount);

        if (ProblemId == FreedomAndInstitutions && LawKind is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)
        {
            if (kind == LawKind && !LawDiscountUsed && costBeforeThisRelic > 0m)
                return new(-1m, WesternEntryCostEffect.LawDiscount);
            if (kind is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill
                && kind != LawKind && !LawSurchargeUsed)
                return new(1m, WesternEntryCostEffect.LawSurcharge);
        }

        if (ProblemId == SelfAndOther && !OtherResponseUsed && OtherIntentTurn == Turn
            && OtherIntendsAttack is bool intendsAttack && costBeforeThisRelic > 0m)
        {
            if (intendsAttack && kind == WesternPracticeCardKind.Skill)
                return new(-1m, WesternEntryCostEffect.OtherResponse);
            if (!intendsAttack && kind == WesternPracticeCardKind.Attack && targetCombatId == OtherCombatId)
                return new(-1m, WesternEntryCostEffect.OtherResponse);
        }

        if (ProblemId == LanguageAndMeaning && CategoryBucket > 0 && CategoryDiscountsRemaining > 0
            && kind is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill or WesternPracticeCardKind.Power
            && costBeforeThisRelic > 0m
            && (namedCategory ? CategoryCardModelId == cardModelId
                : CostBucket(costBeforeThisRelic) == CategoryBucket))
            return new(namedCategory ? -2m : -1m, WesternEntryCostEffect.CategoryDiscount);

        if (ProblemId == HistoryAndPower && !ConventionUsed && ConventionFormedTurn < Turn
            && ConventionCardModelId == cardModelId && costBeforeThisRelic > 0m)
            return new(-1m, WesternEntryCostEffect.ConventionDiscount);

        return default;
    }

    public WesternEntryPlayResolution RecordManualPlay(
        string playId,
        string cardModelId,
        WesternPracticeCardKind kind,
        decimal costBeforeThisRelic,
        bool costsX,
        bool trackedCurrent,
        bool trackedPractice,
        uint? targetCombatId = null,
        bool isSingleLivingEnemyTarget = false,
        WesternEntryCostEffect appliedCostEffect = WesternEntryCostEffect.None,
        bool namedCategory = false)
    {
        if (!CanAct() || string.IsNullOrWhiteSpace(playId) || string.IsNullOrWhiteSpace(cardModelId)
            || !Enum.IsDefined(kind) || RecordedPlayIds.Contains(playId, StringComparer.Ordinal)) return default;
        RecordedPlayIds.Add(playId);

        WesternEntryCostEffect expectedEffect = GetCostChange(cardModelId, kind, costBeforeThisRelic,
            costsX, trackedPractice, targetCombatId, namedCategory).Effect;
        bool consumeCostEffect = appliedCostEffect != WesternEntryCostEffect.None
            && appliedCostEffect == expectedEffect;
        bool applyEnemyStrength = false;
        if (consumeCostEffect)
        {
            switch (appliedCostEffect)
            {
                case WesternEntryCostEffect.LawDiscount: LawDiscountUsed = true; break;
                case WesternEntryCostEffect.LawSurcharge: LawSurchargeUsed = true; break;
                case WesternEntryCostEffect.OtherResponse: OtherResponseUsed = true; break;
                case WesternEntryCostEffect.CategoryDiscount: CategoryDiscountsRemaining--; break;
                case WesternEntryCostEffect.ConventionDiscount:
                    ConventionUsed = true;
                    applyEnemyStrength = true;
                    break;
            }
        }

        bool currentPlayed = ProblemId == BeingAndChange && trackedCurrent && CurrentCardModelId == cardModelId;
        bool drawOne = currentPlayed && CanReleaseCurrent;
        bool practicePlayed = ProblemId == VirtueAndHappiness && trackedPractice && PracticeCardModelId == cardModelId;
        if (currentPlayed) ObserveTrackedCardDeparture(true, false);
        if (ProblemId == BeingAndChange && FlowRuleVersion == 1)
            FlowPlayedKinds |= kind switch
            {
                WesternPracticeCardKind.Attack => 1,
                WesternPracticeCardKind.Skill => 2,
                _ => 0,
            };
        if (practicePlayed)
        {
            PracticeCardModelId = string.Empty;
            PracticeWaits = 0;
        }

        if (ProblemId == FreedomAndInstitutions && LawKind == WesternPracticeCardKind.Other
            && kind is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)
            LawKind = kind;

        if (ProblemId == SelfAndOther && OtherCombatId is null && kind == WesternPracticeCardKind.Attack
            && isSingleLivingEnemyTarget && targetCombatId is uint target)
        {
            OtherCombatId = target;
            OtherBoundTurn = Turn;
            OtherIntentTurn = 0;
            OtherIntendsAttack = null;
            OtherResponseUsed = false;
        }

        if (ProblemId == LanguageAndMeaning && CategoryBucket < 0 && !costsX && costBeforeThisRelic >= 0m
            && kind is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill or WesternPracticeCardKind.Power)
        {
            CategoryCardModelId = cardModelId;
            CategoryBucket = CostBucket(costBeforeThisRelic);
            CategoryDiscountsRemaining = CategoryBucket == 0 ? 0 : 2;
        }

        if (ProblemId == HistoryAndPower)
            TurnCardCounts[cardModelId] = TurnCardCounts.GetValueOrDefault(cardModelId) + 1;

        return new(true, drawOne, drawOne || practicePlayed, applyEnemyStrength);
    }

    public bool RecordAutomaticPlay(string playId, bool trackedCurrent, bool trackedPractice)
    {
        if (!CanAct() || string.IsNullOrWhiteSpace(playId)
            || RecordedPlayIds.Contains(playId, StringComparer.Ordinal)) return false;
        RecordedPlayIds.Add(playId);
        ObserveTrackedCardDeparture(trackedCurrent, trackedPractice);
        return true;
    }

    public void ObserveTrackedCardDeparture(bool trackedCurrent, bool trackedPractice)
    {
        if (trackedCurrent)
        {
            CurrentCardModelId = string.Empty;
            FlowCardKind = WesternPracticeCardKind.Other;
        }
        if (trackedPractice)
        {
            PracticeCardModelId = string.Empty;
            PracticeWaits = 0;
        }
    }

    public WesternEntryRetainResolution ResolveRetains(bool currentStillInHand, bool practiceStillInHand)
    {
        if (!CanAct() || RetainsResolvedTurn == Turn) return default;
        RetainsResolvedTurn = Turn;
        bool retainCurrent = CanRetainCurrent && currentStillInHand;
        bool retainPractice = ProblemId == VirtueAndHappiness && PracticeCardModelId.Length > 0 && practiceStillInHand;
        if (!retainCurrent) ObserveTrackedCardDeparture(true, false);
        if (retainPractice) PracticeWaits++;
        else if (ProblemId == VirtueAndHappiness)
        {
            PracticeCardModelId = string.Empty;
            PracticeWaits = 0;
        }
        return new(true, retainCurrent, retainPractice);
    }

    public bool CloseTurn()
    {
        if (!CanAct()) return false;
        TurnClosed = true;
        if (ProblemId == HistoryAndPower)
        {
            KeyValuePair<string, int>[] ordered = TurnCardCounts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length > 0 && ordered[0].Value >= 2
                && (ordered.Length == 1 || ordered[0].Value > ordered[1].Value))
            {
                ConventionCardModelId = ordered[0].Key;
                ConventionFormedTurn = Turn;
            }
        }
        return true;
    }

    public void EndCombat() => ResetCombatState();

    internal bool IsValidForRestore(string expectedProblemId)
    {
        if (!IsKnownProblem(expectedProblemId) || ProblemId != expectedProblemId
            || Turn < 0 || RetainsResolvedTurn < 0 || RetainsResolvedTurn > Turn
            || PracticeWaits < 0 || OtherBoundTurn < 0 || OtherIntentTurn < 0
            || OtherBoundTurn > Turn || OtherIntentTurn > Turn
            || CategoryCardModelId is null || CategoryBucket is < -1 or > 2 || CategoryDiscountsRemaining is < 0 or > 2
            || ConventionFormedTurn < 0 || ConventionFormedTurn > Turn
            || !Enum.IsDefined(OpeningStage) || !Enum.IsDefined(LawKind)
            || OpeningCardModelIds is null || RecordedPlayIds is null || TurnCardCounts is null
            || CurrentCardModelId is null || PracticeCardModelId is null || ConventionCardModelId is null
            || OpeningCardModelIds.Any(string.IsNullOrWhiteSpace)
            || RecordedPlayIds.Any(string.IsNullOrWhiteSpace)
            || RecordedPlayIds.Distinct(StringComparer.Ordinal).Count() != RecordedPlayIds.Count
            || TurnCardCounts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)) return false;

        if (FlowRuleVersion is not (null or 1) || !Enum.IsDefined(FlowCardKind)
            || FlowPlayedKinds is < 0 or > 3) return false;
        if (FlowRuleVersion == 1 && (ProblemId != BeingAndChange || Turn < 1
            || (CurrentCardModelId.Length > 0 && (!FlowDrawClaimed
                || FlowCardKind is not (WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)))
            || (CurrentCardModelId.Length == 0 && FlowCardKind != WesternPracticeCardKind.Other))) return false;
        if (FlowRuleVersion is null && (FlowDrawClaimed || FlowCardKind != WesternPracticeCardKind.Other
            || FlowPlayedKinds != 0)) return false;

        int openingMaximum = ProblemId == KnowledgeAndDoubt ? 2 : ProblemId == VirtueAndHappiness ? 1 : 0;
        if (OpeningCardModelIds.Count > openingMaximum
            || (OpeningStage != WesternOpeningStage.Committed && OpeningCardModelIds.Count > 0)
            || (OpeningStage is WesternOpeningStage.Choosing or WesternOpeningStage.Committed
                && (Turn != 1 || TurnClosed))
            || (OpeningStage != WesternOpeningStage.NotStarted && ProblemId is not (KnowledgeAndDoubt or VirtueAndHappiness)))
            return false;

        if (!CombatActive)
            return Turn == 0 && !TurnClosed && RetainsResolvedTurn == 0
                && OpeningStage == WesternOpeningStage.NotStarted && OpeningCardModelIds.Count == 0
                && CurrentCardModelId.Length == 0 && PracticeCardModelId.Length == 0 && PracticeWaits == 0
                && FlowRuleVersion is null && !FlowDrawClaimed && FlowPlayedKinds == 0
                && LawKind == WesternPracticeCardKind.Other && !LawDiscountUsed && !LawSurchargeUsed
                && OtherCombatId is null && OtherBoundTurn == 0 && OtherIntentTurn == 0 && OtherIntendsAttack is null
                && !OtherResponseUsed && CategoryCardModelId.Length == 0 && CategoryBucket == -1 && CategoryDiscountsRemaining == 0
                && ConventionCardModelId.Length == 0 && ConventionFormedTurn == 0 && !ConventionUsed
                && TurnCardCounts.Count == 0 && RecordedPlayIds.Count == 0;

        return (Turn > 0 || !TurnClosed)
            && (LawKind is WesternPracticeCardKind.Other or WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)
            && (CategoryCardModelId.Length == 0 || CategoryBucket >= 0)
            && (CategoryBucket >= 0 || CategoryDiscountsRemaining == 0)
            && (CategoryBucket != 0 || CategoryDiscountsRemaining == 0)
            && (OtherCombatId is not null || (OtherBoundTurn == 0 && OtherIntentTurn == 0 && OtherIntendsAttack is null))
            && (OtherIntentTurn > 0 || OtherIntendsAttack is null)
            && (PracticeCardModelId.Length > 0 || PracticeWaits == 0)
            && ((ConventionCardModelId.Length == 0 && ConventionFormedTurn == 0)
                || (ConventionCardModelId.Length > 0 && ConventionFormedTurn > 0));
    }

    private bool CanAct() => CombatActive && Turn > 0 && !TurnClosed;

    private static int CostBucket(decimal cost) => cost <= 0m ? 0 : cost <= 1m ? 1 : 2;

    private void ResetCombatState()
    {
        CombatActive = false;
        Turn = 0;
        TurnClosed = false;
        RetainsResolvedTurn = 0;
        OpeningStage = WesternOpeningStage.NotStarted;
        OpeningCardModelIds = [];
        CurrentCardModelId = string.Empty;
        FlowRuleVersion = null;
        FlowDrawClaimed = false;
        FlowCardKind = WesternPracticeCardKind.Other;
        FlowPlayedKinds = 0;
        PracticeCardModelId = string.Empty;
        PracticeWaits = 0;
        LawKind = WesternPracticeCardKind.Other;
        LawDiscountUsed = false;
        LawSurchargeUsed = false;
        OtherCombatId = null;
        OtherBoundTurn = 0;
        OtherIntentTurn = 0;
        OtherIntendsAttack = null;
        OtherResponseUsed = false;
        CategoryCardModelId = string.Empty;
        CategoryBucket = -1;
        CategoryDiscountsRemaining = 0;
        ConventionCardModelId = string.Empty;
        ConventionFormedTurn = 0;
        ConventionUsed = false;
        TurnCardCounts = new(StringComparer.Ordinal);
        RecordedPlayIds = [];
    }
}
