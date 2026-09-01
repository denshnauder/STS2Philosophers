using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BehaviorObservationCombatStartPatch
{
    private static void Prefix(IRunState runState)
    {
        if (runState is not RunState concreteRunState
            || concreteRunState.Players.Count != 1
            || CombatManager.Instance.CurrentCombatId is not { } combatId)
        {
            return;
        }

        BehaviorObservationRecorder.BeginCombat(
            PhilosophyRunStateService.GetOrCreate(concreteRunState),
            concreteRunState.CurrentActIndex,
            combatId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class BehaviorObservationCardPlayedPatch
{
    private static void Prefix(ICombatState combatState, CardPlay cardPlay)
    {
        if (combatState.RunState is not RunState runState
            || runState.Players.Count != 1
            || !ReferenceEquals(runState.Players[0], cardPlay.Player))
        {
            return;
        }

        BehaviorObservationRecorder.RecordCardPlayed(
            PhilosophyRunStateService.GetOrCreate(runState),
            runState.CurrentActIndex,
            GetCardTypeFactId(cardPlay.Card.Type));
    }

    private static string GetCardTypeFactId(CardType cardType)
    {
        return cardType switch
        {
            CardType.Attack => BehaviorGameFactIds.AttackCardPlayed,
            CardType.Skill => BehaviorGameFactIds.SkillCardPlayed,
            CardType.Power => BehaviorGameFactIds.PowerCardPlayed,
            CardType.Status => BehaviorGameFactIds.StatusCardPlayed,
            CardType.Curse => BehaviorGameFactIds.CurseCardPlayed,
            CardType.Quest => BehaviorGameFactIds.QuestCardPlayed,
            _ => BehaviorGameFactIds.OtherCardPlayed,
        };
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class BehaviorObservationCombatEndPatch
{
    private static void Prefix(IRunState runState, CombatRoom room)
    {
        if (runState is not RunState concreteRunState
            || concreteRunState.Players.Count != 1)
        {
            return;
        }

        PhilosophyRunState philosophyState =
            PhilosophyRunStateService.GetOrCreate(concreteRunState);
        int actIndex = concreteRunState.CurrentActIndex;
        if (BehaviorObservationRecorder.CompleteCombat(
                philosophyState,
                actIndex))
        {
            Log.Info(
                "[STS2Philosophers] Behavior observation: "
                + BehaviorObservationDiagnostics.FormatActSummary(
                    philosophyState.ActBehaviorStates[actIndex]));
        }
    }
}
