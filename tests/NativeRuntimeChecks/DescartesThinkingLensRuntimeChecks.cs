using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class DescartesThinkingLensRuntimeChecks
{
    public static async Task Run()
    {
        var harmony = new HarmonyLib.Harmony("STS2Philosophers.NativeCostChecks");
        var patch = typeof(DescartesThinkingLens).Assembly.GetType("STS2Philosophers.DescartesThinkingLensPreviewPatch")!;
        harmony.CreateClassProcessor(patch).Patch(); // Validate native overload and private preview-field binding.
        harmony.CreateClassProcessor(typeof(DescartesThinkingLens).Assembly.GetType("STS2Philosophers.DescartesThinkingLensAffordabilityPatch")!).Patch();
        var fixture = new NativeRuntimeFixture(typeof(DescartesThinkingLens));
        var strike = fixture.AddCard<StrikeSilent>();
        var copy = fixture.AddCard<StrikeSilent>();
        var skill = fixture.AddCard<DefendSilent>();
        await fixture.Begin();
        fixture.PlayerState.Energy = 3;
        Check(strike.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Unbound attack must stay at one.");
        await fixture.PayAndComplete(strike, fixture.Enemy);
        Check(fixture.PlayerState.Energy == 2, "The binding attack pays normally on turn one.");
        await fixture.NextTurn();
        fixture.ReloadRelicPayload();
        fixture.PlayerState.Energy = 3;
        Check(copy.CurrentTarget is null && copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 0,
            "ENC191: sole locked non-attacking enemy must show zero before CurrentTarget is assigned.");
        var friendly = new MegaCrit.Sts2.Core.Entities.Creatures.Creature(
            MegaCrit.Sts2.Core.Models.ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.DampCultist>().ToMutable(),
            MegaCrit.Sts2.Core.Combat.CombatSide.Player, null) { CombatId = 99, CombatState = fixture.Combat };
        ((List<MegaCrit.Sts2.Core.Entities.Creatures.Creature>)fixture.Combat.Allies).Add(friendly);
        Check(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "A friendly summon must not count as an enemy target candidate.");
        using (Preview(copy, friendly)) Check(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "A friendly preview never receives the enemy-target discount.");
        var secondEnemy = fixture.AddEnemy(8);
        Check(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Multiple enemies without a target cannot promise zero.");
        using (Preview(copy, secondEnemy)) Check(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Other target preview stays at one.");
        using (Preview(copy, fixture.Enemy)) Check(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Locked target preview uses native cost getter at zero.");
        fixture.PlayerState.Energy = 0;
        Check(copy.CanPlayTargeting(fixture.Enemy) && !copy.CanPlayTargeting(secondEnemy),
            "At zero energy, native pre-enqueue affordability must permit just the discounted target.");
        fixture.PlayerState.Energy = 3;
        fixture.SelectAction(copy, secondEnemy);
        Check(copy.EnergyCost.GetAmountToSpend() == 1, "Native action targeting another enemy must pay one.");
        fixture.ClearAction();
        var wrongPlay = await fixture.PayAndComplete(copy, secondEnemy);
        Check(wrongPlay.Resources.EnergySpent == 1 && fixture.PlayerState.Energy == 2, "Actual wrong-target payment must deduct one without consuming the locked discount.");
        var lockedCopy = fixture.AddCard<StrikeSilent>();
        fixture.PlayerState.Energy = 3;
        var play = await fixture.PayAndComplete(lockedCopy, fixture.Enemy);
        Check(play.Resources.EnergySpent == 0 && fixture.PlayerState.Energy == 3, "Native SpendResources must actually deduct zero, before CurrentTarget exists.");
        await fixture.Relic.AfterCardPlayed(null!, play);
        fixture.ReloadRelicPayload();
        var later = fixture.AddCard<StrikeSilent>();
        using (Preview(later, fixture.Enemy)) Check(later.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Consumed discount remains consumed after repeat callback and payload restore.");
        await fixture.NextTurn();
        Set(fixture.Enemy.Monster!, "NextMove", new MoveState("ATTACK", _ => Task.CompletedTask, new SingleAttackIntent(6)));
        await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
        // Intent is frozen once per turn; establish the attacking intent before the next real turn.
        await fixture.NextTurn();
        Check(skill.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Attacking locked enemy discounts a skill.");
        using (Preview(later, fixture.Enemy)) Check(later.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Attacking intent never discounts an attack.");
        fixture.PlayerState.Energy = 3;
        var skillPlay = await fixture.PayAndComplete(skill, null);
        Check(skillPlay.Resources.EnergySpent == 0 && fixture.PlayerState.Energy == 3, "Native skill payment agrees with preview.");
        await fixture.NextTurn();
        Set(fixture.Enemy, "_currentHp", 0);
        await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
        await fixture.NextTurn();
        Check(later.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Dead or missing lock cannot migrate to another enemy.");
        var unbound = new NativeRuntimeFixture(typeof(DescartesThinkingLens));
        var automatic = unbound.AddCard<StrikeSilent>();
        await unbound.Begin();
        await unbound.NextTurn();
        Check(automatic.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "A second turn without a binding attack grants no discount.");
        harmony.UnpatchAll(harmony.Id);
        Console.WriteLine("ENC191 native runtime checks passed: native CardEnergyCost/Hook/SpendResources, action targets, scoped previews, intent branches, one use, turns and payload restore; no rendered NCard or full game replay.");
    }
}
