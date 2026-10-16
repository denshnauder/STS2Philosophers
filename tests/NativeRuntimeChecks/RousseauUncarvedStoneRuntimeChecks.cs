using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class RousseauUncarvedStoneRuntimeChecks
{
    private static int Strength(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => creature.GetPower<StrengthPower>()?.Amount ?? 0;
    private static JsonNode State(NativeRuntimeFixture fixture) => JsonNode.Parse(fixture.Relic.PracticePayload)!["State"]!;
    private static async Task Establish(NativeRuntimeFixture fixture, bool upgradeSecond = false)
    {
        var first = fixture.AddCard<StrikeSilent>();
        var second = fixture.AddCard<StrikeSilent>();
        var survivor = fixture.AddCard<Survivor>();
        if (upgradeSecond) second.UpgradeInternal();
        await fixture.Begin();
        while (fixture.PlayerState.TurnNumber < 6) await fixture.NextTurn();
        fixture.PlayerState.Energy = 9;
        var firstPlay = await fixture.PayAndComplete(first, fixture.Enemy);
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), firstPlay);
        await fixture.PayAndComplete(second, fixture.Enemy);
        await fixture.PayAndComplete(survivor, null);
        Check(State(fixture)["TurnCardCounts"]![first.Id.ToString()]!.GetValue<int>() == 2,
            "ENC192: two distinct native Strike instances must count twice, despite identical PlayIndex/PlayCount.");
        Check(State(fixture)["TurnCardCounts"]![survivor.Id.ToString()]!.GetValue<int>() == 1, "One Survivor is not a tied winner.");
        await fixture.NextTurn();
        Check(fixture.PlayerState.TurnNumber == 7
            && State(fixture)["ConventionCardModelId"]!.GetValue<string>() == first.Id.ToString(), "Turn six must establish Strike for turn seven.");
    }
    public static async Task Run()
    {
        TestMode.IsOn = true; // Native commands skip graphical waits, not power application or energy payment.
        ModelDb.Inject(typeof(StrengthPower));
        var fixture = new NativeRuntimeFixture(typeof(RousseauUncarvedStone));
        await Establish(fixture);
        fixture.ReloadRelicPayload();
        var other = fixture.AddEnemy(8);
        var dead = fixture.AddEnemy(9);
        Set(dead, "_currentHp", 0);
        var zero = fixture.AddCard<StrikeSilent>();
        zero.EnergyCost.SetThisTurnOrUntilPlayed(0);
        var firstEligible = fixture.AddCard<StrikeSilent>();
        var later = fixture.AddCard<StrikeSilent>();
        fixture.PlayerState.Energy = 3;
        Check(firstEligible.EnergyCost.GetWithModifiers(CostModifiers.All) == 0
            && firstEligible.EnergyCost.GetAmountToSpend() == 0, "Native hand display getter and payment getter agree before play.");
        var freePlay = await fixture.PayAndComplete(zero, fixture.Enemy);
        Check(freePlay.Resources.EnergySpent == 0 && Strength(fixture.Enemy) == 0
            && !State(fixture)["ConventionUsed"]!.GetValue<bool>(), "A locally free card cannot charge Strength or consume the benefit.");
        var play = await fixture.PayAndComplete(firstEligible, fixture.Enemy);
        Check(play.Resources.EnergySpent == 0 && fixture.PlayerState.Energy == 3, "First positive-base-cost Strike actually pays zero.");
        Check(Strength(fixture.Enemy) == 1 && Strength(other) == 1 && Strength(dead) == 0
            && Strength(fixture.Player.Creature) == 0, "Native PowerCmd applies exactly one Strength to every living enemy only.");
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        fixture.ReloadRelicPayload();
        Check(later.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Later same-model instance returns to one after payload restore.");
        var laterPlay = await fixture.PayAndComplete(later, fixture.Enemy);
        Check(laterPlay.Resources.EnergySpent == 1 && fixture.PlayerState.Energy == 2
            && Strength(fixture.Enemy) == 1 && Strength(other) == 1, "Native payment and Strength consequence occur once, including duplicate callbacks.");
        await fixture.NextTurn();
        Check(State(fixture)["ConventionUsed"]!.GetValue<bool>() == false, "A real new turn resets only the per-turn use budget.");

        var upgraded = new NativeRuntimeFixture(typeof(RousseauUncarvedStone));
        await Establish(upgraded, upgradeSecond: true);
        var notDrawn = upgraded.AddCard<DefendSilent>();
        upgraded.PlayerState.Energy = 3;
        await upgraded.PayAndComplete(notDrawn, null);
        Check(Strength(upgraded.Enemy) == 0 && !State(upgraded)["ConventionUsed"]!.GetValue<bool>(), "No drawn convention card means no Strength consequence or consumed benefit.");
        upgraded.ReloadRelicPayload();
        var drawnLater = upgraded.AddCard<StrikeSilent>();
        Check(drawnLater.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "A convention card drawn later in the same turn still receives the unused benefit.");
        await upgraded.NextTurn();
        Check(drawnLater.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "The existing convention persists until a new unique winner replaces it.");
        await upgraded.Relic.AfterCombatEnd(null!);
        Check(drawnLater.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Combat end removes the convention benefit.");
        Console.WriteLine("ENC192 native runtime checks passed: distinct Strike x2 + Survivor on turn 6, upgraded identity, turn 7 display/native payment, real PowerCmd Strength once to living enemies, zero cost, no draw, later draw, turns, payload restore and combat cleanup; no rendered UI/full game replay.");
    }
}
