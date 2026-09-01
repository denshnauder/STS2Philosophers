namespace STS2Philosophers;

internal static class BehaviorGameFactIds
{
    public const string CombatStarted = "COMBAT_STARTED";
    public const string CombatCompleted = "COMBAT_COMPLETED";
    public const string CardPlayed = "CARD_PLAYED";
    public const string AttackCardPlayed = "ATTACK_CARD_PLAYED";
    public const string SkillCardPlayed = "SKILL_CARD_PLAYED";
    public const string PowerCardPlayed = "POWER_CARD_PLAYED";
    public const string StatusCardPlayed = "STATUS_CARD_PLAYED";
    public const string CurseCardPlayed = "CURSE_CARD_PLAYED";
    public const string QuestCardPlayed = "QUEST_CARD_PLAYED";
    public const string OtherCardPlayed = "OTHER_CARD_PLAYED";
}

internal static class BehaviorObservationRecorder
{
    public static bool BeginCombat(
        PhilosophyRunState runState,
        int actIndex,
        string combatId)
    {
        if (string.IsNullOrWhiteSpace(combatId))
        {
            return false;
        }

        ActBehaviorState actState = runState.GetOrCreateActBehaviorState(actIndex);
        if (string.Equals(
                actState.ActiveCombat?.CombatId,
                combatId,
                StringComparison.Ordinal))
        {
            return false;
        }

        actState.DiscardActiveCombat();
        if (!actState.TryBeginCombat(combatId))
        {
            return false;
        }

        return actState.RecordGameFact(BehaviorGameFactIds.CombatStarted);
    }

    public static bool RecordCardPlayed(
        PhilosophyRunState runState,
        int actIndex,
        string cardTypeFactId)
    {
        if (string.IsNullOrWhiteSpace(cardTypeFactId))
        {
            return false;
        }

        if (!runState.ActBehaviorStates.TryGetValue(actIndex, out ActBehaviorState? actState))
        {
            return false;
        }

        if (!actState.RecordGameFact(BehaviorGameFactIds.CardPlayed))
        {
            return false;
        }

        return actState.RecordGameFact(cardTypeFactId);
    }

    public static bool CompleteCombat(
        PhilosophyRunState runState,
        int actIndex)
    {
        if (!runState.ActBehaviorStates.TryGetValue(actIndex, out ActBehaviorState? actState))
        {
            return false;
        }

        return actState.RecordGameFact(BehaviorGameFactIds.CombatCompleted)
            && actState.CompleteCombat();
    }
}
