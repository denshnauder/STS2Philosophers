using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Real native cost queries and SpendResources, with the native ordering of
// parent payment -> child automatic receipt -> parent receipt. Card effects,
// selection screens and Sly's full OnPlay animation are not replayed here.
internal static class SlyCostReceiptNativeChecks
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static object Practice(NativeRuntimeFixture f) =>
        typeof(WesternPracticeRelic).GetField("_practice", Fields)!.GetValue(f.Relic)!;
    private static object Value(NativeRuntimeFixture f, string name) =>
        Practice(f).GetType().GetProperty(name)!.GetValue(Practice(f))!;
    private static bool Used(NativeRuntimeFixture f, bool surcharge) =>
        (bool)Value(f, surcharge ? "LawSurchargeUsed" : "LawDiscountUsed");
    private static string Law(NativeRuntimeFixture f) => Value(f, "LawKind").ToString()!;

    private static async Task<NativeRuntimeFixture> NewFixture()
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(PlatoCivicSeal));
        await f.Begin(); f.PlayerState.Energy = 40;
        return f;
    }

    private static CardModel Card(NativeRuntimeFixture f, bool attack)
    {
        CardModel card = attack ? f.AddCard<StrikeSilent>() : f.AddCard<DefendSilent>();
        card.EnergyCost.SetThisTurnOrUntilPlayed(2);
        return card;
    }

    private static async Task<CardPlay> Pay(NativeRuntimeFixture f, CardModel card, int expected)
    {
        Check(card.EnergyCost.GetWithModifiers(CostModifiers.All) == expected,
            "The native hand cost query must show the expected seal price before payment.");
        var target = card.Type == CardType.Attack ? f.Enemy : null;
        f.SelectAction(card, target);
        int before = f.PlayerState.Energy;
        var (energy, stars) = await card.SpendResources();
        Check(energy == expected && before - f.PlayerState.Energy == expected,
            "Native SpendResources must actually deduct the displayed seal price.");
        Set(card, "_currentTarget", target);
        f.PlayerState.Hand.RemoveInternal(card, silent: true);
        f.PlayerState.PlayPile.AddInternal(card, silent: true);
        var resources = new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars };
        var location = f.Relic.ModifyCardPlayResultLocation(card, false, resources,
            new CardLocation(f.Player, PileType.Discard, CardPilePosition.Bottom));
        var play = new CardPlay { Card = card, Player = f.Player, Target = target, IsAutoPlay = false,
            Resources = resources, ResultPile = location.pileType, PlayIndex = 0, PlayCount = 1 };
        await f.Relic.BeforeCardPlayed(play);
        return play;
    }

    private static async Task Complete(NativeRuntimeFixture f, CardPlay play)
    {
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        Set(play.Card, "_currentTarget", null);
        f.ClearAction();
    }

    private static async Task Automatic(NativeRuntimeFixture f, bool sly, CardModel? replay = null, bool attack = true)
    {
        CardModel child = replay ?? Card(f, attack: attack);
        if (sly) child.GiveSingleTurnSly();
        Check(!sly || child.IsSlyThisTurn, "The discarded child is genuinely marked Sly by the native card API.");
        int before = f.PlayerState.Energy;
        // CardCmd.AutoPlay queries EnergyValue but sets EnergySpent to zero and
        // calls OnPlayWrapper with isAutoPlay=true, including AutoPlayType.SlyDiscard.
        int energyValue = child.EnergyCost.GetAmountToSpend();
        var resources = new ResourceInfo { EnergySpent = 0, EnergyValue = energyValue, StarsSpent = 0, StarValue = 0 };
        var location = f.Relic.ModifyCardPlayResultLocation(child, true, resources,
            new CardLocation(f.Player, PileType.Discard, CardPilePosition.Bottom));
        var play = new CardPlay { Card = child, Player = f.Player,
            Target = child.Type == CardType.Attack ? f.Enemy : null, IsAutoPlay = true,
            Resources = resources, ResultPile = location.pileType, PlayIndex = 0, PlayCount = 1 };
        await f.Relic.BeforeCardPlayed(play);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        Check(f.PlayerState.Energy == before, "An automatic receipt reports effective energy value without paying energy.");
        // Keep the parent's CurrentlyRunningAction intact: a nested native auto
        // play does not become another manually queued PlayCardAction.
    }

    private static async Task NestedReceipt(bool surcharge, bool sly, bool replaySameCard = false, bool autoAttack = true)
    {
        var f = await NewFixture();
        await Complete(f, await Pay(f, Card(f, attack: !surcharge), 2));
        Check(Law(f) == (surcharge ? "Skill" : "Attack"), "The first manual card establishes its actual kind.");
        int price = surcharge ? 3 : 1;
        var parent = await Pay(f, Card(f, attack: true), price);
        await Automatic(f, sly, replaySameCard ? parent.Card : null, autoAttack);
        await Complete(f, parent);
        Check(Used(f, surcharge),
            $"ENC198 {(surcharge ? "surcharge" : "discount")}: a nested automatic receipt must preserve the paid parent's one-use receipt.");
        CardModel next = Card(f, attack: true);
        Check(next.EnergyCost.GetWithModifiers(CostModifiers.All) == 2,
            "After the paid parent resolves, the next same-kind card has its base price rather than repeating the seal adjustment.");
        await Complete(f, await Pay(f, next, 2));
        string beforeRepeat = f.Relic.PracticePayload;
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), parent);
        Check(f.Relic.PracticePayload == beforeRepeat, "Repeating the exact parent callback cannot consume or restore another opportunity.");
        await f.NextTurn(); f.PlayerState.Energy = 40;
        Check(!Used(f, surcharge) && Law(f) == (surcharge ? "Skill" : "Attack"),
            "A genuine next turn resets the one-use fee receipt and preserves the combat law.");
        await Complete(f, await Pay(f, Card(f, attack: true), price));
        await Complete(f, await Pay(f, Card(f, attack: true), 2));
    }

    private static async Task AutomaticBeforeLaw(bool attack)
    {
        var f = await NewFixture();
        await Automatic(f, sly: true, attack: attack);
        Check(Law(f) == "Other" && !Used(f, true) && !Used(f, false),
            $"An opening Sly {(attack ? "attack" : "skill")} cannot establish the law or consume either manual price opportunity.");
        await Complete(f, await Pay(f, Card(f, attack: true), 2));
        Check(Law(f) == "Attack", "The actual first manual attack establishes Attack after opening Sly.");
        await Complete(f, await Pay(f, Card(f, attack: true), 1));
        await Complete(f, await Pay(f, Card(f, attack: false), 3));
        await Complete(f, await Pay(f, Card(f, attack: false), 2));
    }

    private static async Task ManualDiscardSkill()
    {
        var f = await NewFixture();
        var skill = f.AddCard<Prepared>(); skill.EnergyCost.SetThisTurnOrUntilPlayed(2);
        var parent = await Pay(f, skill, 2);
        await Automatic(f, sly: true);
        Check(Law(f) == "Other", "The nested Sly attack does not establish a law while its first manual skill is still resolving.");
        await Complete(f, parent);
        Check(Law(f) == "Skill", "A genuinely manual discard skill legally establishes Skill despite its nested automatic attack.");
        await Complete(f, await Pay(f, Card(f, attack: true), 3));
        await Complete(f, await Pay(f, Card(f, attack: true), 2));
    }

    private static async Task ReceiptInvalidated(bool surcharge, bool restorePayload)
    {
        var f = await NewFixture();
        await Complete(f, await Pay(f, Card(f, attack: !surcharge), 2));
        int price = surcharge ? 3 : 1;
        CardPlay interrupted = await Pay(f, Card(f, attack: true), price);
        if (restorePayload) f.ReloadRelicPayload();
        else { await f.NextTurn(); f.PlayerState.Energy = 40; }
        // Deliver the obsolete receipt without another cost query for that card.
        // Neither a restored payload nor a later turn proves this old payment.
        await Complete(f, interrupted);
        Check(!Used(f, surcharge), "A pre-restore/pre-turn paid receipt cannot consume a newly initialized turn's fee opportunity.");
        await Complete(f, await Pay(f, Card(f, attack: true), price));
        await Complete(f, await Pay(f, Card(f, attack: true), 2));
    }

    private static async Task IndependentMutableCopies()
    {
        var first = await NewFixture();
        await Complete(first, await Pay(first, Card(first, attack: false), 2));
        CardPlay firstParent = await Pay(first, Card(first, attack: true), 3);
        // A second native ToMutable copy starts/reset its own combat while the
        // first has a pending receipt. Its lifecycle must not erase the first.
        var second = await NewFixture();
        Check(!ReferenceEquals(first.Relic, second.Relic), "The fixture uses two actual independent native mutable relics.");
        await Complete(second, await Pay(second, Card(second, attack: true), 2));
        CardPlay secondParent = await Pay(second, Card(second, attack: true), 1);
        await Automatic(second, sly: true);
        await Complete(second, secondParent);
        await Complete(first, firstParent);
        Check(Used(first, true) && !Used(first, false) && Used(second, false) && !Used(second, true),
            "Separate mutable seals retain only their own paid surcharge/discount receipts across the other's initialization and automatic callback.");
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? priorRun = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        object? priorExecutor = manager.ActionExecutor;
        bool priorTest = TestMode.IsOn;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Fields)!.GetValue(CombatManager.Instance);
        object? priorIds = typeof(NetCombatCardDb).GetField("_idToCard", Fields)!.GetValue(NetCombatCardDb.Instance);
        object? priorCards = typeof(NetCombatCardDb).GetField("_cardToId", Fields)!.GetValue(NetCombatCardDb.Instance);
        List<Exception> failures = [];
        async Task Scenario(string name, Func<Task> run)
        {
            try { await run(); Console.WriteLine($"ENC198 PASS {name}"); }
            catch (Exception error) { failures.Add(new InvalidOperationException(name, error)); Console.WriteLine($"ENC198 FAIL {name}: {error.Message}"); }
        }
        try
        {
            TestMode.IsOn = true;
            await Scenario("opening Sly attack has no manual law", () => AutomaticBeforeLaw(attack: true));
            await Scenario("opening Sly skill has no manual law", () => AutomaticBeforeLaw(attack: false));
            await Scenario("manual discard skill keeps its legal law", ManualDiscardSkill);
            foreach (bool sly in new[] { true, false })
                foreach (bool surcharge in new[] { false, true })
                    await Scenario($"{(sly ? "Sly" : "ordinary auto")} nested {(surcharge ? "surcharge" : "discount")}",
                        () => NestedReceipt(surcharge, sly));
            foreach (bool surcharge in new[] { false, true })
                await Scenario($"same-card automatic replay {(surcharge ? "surcharge" : "discount")}",
                    () => NestedReceipt(surcharge, sly: true, replaySameCard: true));
            foreach (bool surcharge in new[] { false, true })
                await Scenario($"Sly nested opposite-kind automatic {(surcharge ? "surcharge" : "discount")}",
                    () => NestedReceipt(surcharge, sly: true, autoAttack: false));
            foreach (bool restorePayload in new[] { true, false })
                foreach (bool surcharge in new[] { false, true })
                    await Scenario($"{(restorePayload ? "payload restore" : "new turn")} invalidates pending {(surcharge ? "surcharge" : "discount")}",
                        () => ReceiptInvalidated(surcharge, restorePayload));
            await Scenario("independent native mutable receipt lifecycles", IndependentMutableCopies);
            if (failures.Count != 0) throw new AggregateException("ENC198 native cost receipt failures", failures);
            Console.WriteLine("ENC198 native checks passed: real native hand cost queries/SpendResources, nested Sly and ordinary automatic receipts, same-card replay, manual lawmaking, once-only discount/surcharge, duplicate callbacks, payload/turn invalidation and independent mutable lifecycles. No full Sly card effects, rendered hand, player saves or game replay.");
        }
        finally
        {
            Set(manager, "State", priorRun); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn);
            Set(NetCombatCardDb.Instance, "_idToCard", priorIds); Set(NetCombatCardDb.Instance, "_cardToId", priorCards);
            TestMode.IsOn = priorTest;
        }
    }
}
