using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using STS2Philosophers;

// A native managed object fixture, not a running game or a replacement cost model.
// Only the Player shell and turn-loop bootstrap use reflection to avoid a real run/save/UI.
internal sealed class NativeRuntimeFixture
{
    public Player Player { get; }
    public PlayerCombatState PlayerState { get; }
    public CombatState Combat { get; } = new();
    public Creature Enemy { get; }
    public WesternPracticeRelic Relic { get; }

    public static void Set(object instance, string name, object? value)
    {
        for (Type? type = instance.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? type.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) { field.SetValue(instance, value); return; }
        }
        throw new MissingFieldException(instance.GetType().Name, name);
    }
    public static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public NativeRuntimeFixture(Type relicType)
    {
        ModelDb.Inject(relicType);
        ModelDb.Inject(typeof(DampCultist));
        Player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Set(Player, "_relics", new List<RelicModel>());
        Set(Player, "_potionSlots", new List<PotionModel>());
        Set(Player, "_runPiles", Array.Empty<CardPile>());
        Set(Player, "IsActiveForHooks", true);
        var creature = new Creature(Player, 50, 50) { CombatState = Combat };
        Set(Player, "Creature", creature);
        PlayerState = new PlayerCombatState(Player);
        Set(Player, "PlayerCombatState", PlayerState);
        ((List<Creature>)Combat.Allies).Add(creature);
        var turnType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState")!;
        var turn = Activator.CreateInstance(turnType, Combat)!;
        Set(turn, "IsInProgress", true);
        Set(CombatManager.Instance, "_turnState", turn);
        var executor = (ActionExecutor)RuntimeHelpers.GetUninitializedObject(typeof(ActionExecutor));
        Set(RunManager(), "ActionExecutor", executor);
        Enemy = AddEnemy(7);
        Relic = (WesternPracticeRelic)ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).ToMutable();
        Relic.Owner = Player;
        ((List<RelicModel>)Player.Relics).Add(Relic);
    }
    private static MegaCrit.Sts2.Core.Runs.RunManager RunManager() => MegaCrit.Sts2.Core.Runs.RunManager.Instance;
    public Creature AddEnemy(uint id)
    {
        var enemy = new Creature(ModelDb.Monster<DampCultist>().ToMutable(), CombatSide.Enemy, null)
            { CombatId = id, CombatState = Combat };
        ((List<Creature>)Combat.Enemies).Add(enemy);
        return enemy;
    }
    public CardModel AddCard<T>() where T : CardModel
    {
        ModelDb.Inject(typeof(T));
        var card = ModelDb.Card<T>().ToMutable();
        card.Owner = Player;
        PlayerState.Hand.AddInternal(card, silent: true);
        return card;
    }
    public async Task Begin() { await Relic.BeforeCombatStart(); await StartTurn(); }
    public async Task StartTurn()
    {
        await Relic.BeforeSideTurnStart(null!, CombatSide.Player, [Player.Creature], Combat);
        await Relic.AfterPlayerTurnStartLate(null!, Player);
    }
    public async Task NextTurn()
    {
        await Relic.AfterSideTurnEnd(null!, CombatSide.Player, [Player.Creature]);
        PlayerState.IncrementTurnNumber();
        await StartTurn();
    }
    public void SelectAction(CardModel card, Creature? target)
    {
        // Bootstrap only the native network identity tables. StartCombat logs through
        // Godot.OS, unavailable in this console host; cost/payment code remains native.
        Set(NetCombatCardDb.Instance, "_idToCard", new Dictionary<uint, CardModel> { [1] = card });
        Set(NetCombatCardDb.Instance, "_cardToId", new Dictionary<CardModel, uint> { [card] = 1 });
        Set(RunManager().ActionExecutor, "CurrentlyRunningAction", new PlayCardAction(card, target));
    }
    public void ClearAction() => Set(RunManager().ActionExecutor, "CurrentlyRunningAction", null);
    public async Task<CardPlay> PayAndComplete(CardModel card, Creature? target)
    {
        SelectAction(card, target);
        var (energy, stars) = await card.SpendResources(); // Native payment occurs before CurrentTarget.
        Set(card, "_currentTarget", target);
        PlayerState.Hand.RemoveInternal(card, silent: true);
        PlayerState.PlayPile.AddInternal(card, silent: true);
        var play = new CardPlay { Card = card, Player = Player, Target = target,
            IsAutoPlay = false, ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
            Resources = new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars } };
        await Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), play);
        Set(card, "_currentTarget", null);
        ClearAction();
        return play;
    }
    public void ReloadRelicPayload() { string payload = Relic.PracticePayload; Relic.PracticePayload = payload; }
    public static IDisposable Preview(CardModel card, Creature? target)
    {
        var type = typeof(DescartesThinkingLens).Assembly.GetType("STS2Philosophers.DescartesThinkingLensTargetContext+PreviewScope")!;
        return (IDisposable)Activator.CreateInstance(type, card, target)!;
    }
}
