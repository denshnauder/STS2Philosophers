using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public abstract class WesternPracticeRelic : RelicModel
{
    protected abstract string ProblemId { get; }
    protected abstract string ResourceName { get; }
    private WesternPracticeState? _practice;
    private HashSet<object>? _callbacks;
    private WesternPracticeState Practice => _practice ??= new() { ProblemId = ProblemId };

    public override RelicRarity Rarity => RelicRarity.None;
    public override string PackedIconPath => $"res://STS2Philosophers/images/{ResourceName}.svg";
    protected override string PackedIconOutlinePath => $"res://STS2Philosophers/images/{ResourceName}_outline.svg";
    protected override string BigIconPath => PackedIconPath;
    public override bool ShowCounter => CombatManager.Instance.IsInProgress;
    public override int DisplayAmount => IsMutable ? Practice.Plays.Count : 0;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PracticePayload
    {
        get => _practice is null ? string.Empty : WesternPracticeStateCodec.Encode(_practice);
        set
        {
            AssertMutable();
            _practice = WesternPracticeStateCodec.Restore(value, ProblemId);
            _callbacks = null;
        }
    }

    // Also guards debug grants: a Western carrier cannot add a second major doctrine.
    private bool IsSoleDoctrine => Owner.Relics.Count(relic => relic is WesternPracticeRelic) == 1
        && !Owner.Relics.Any(relic => relic is KongziMuduo or KongziQingYuPei or MengziXiongZhang
            or XunziShengMo or MoziMoSeZhuJian or MoziShouChengTu or LaoziWuWeiShuJian
            or LaoziShuiYu or QinGuliShouChengXie or ZhuangziDaHu or YangzhuQuanShengBi or HuishiLiWuChou);

    public override Task BeforeCombatStart()
    {
        Practice.EndCombat();
        _callbacks = null;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } state && Practice.BeginTurn(state.TurnNumber))
        {
            _callbacks = null;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner || !IsSoleDoctrine || Owner.PlayerCombatState is not { } state)
            return Task.CompletedTask;
        _callbacks ??= new(ReferenceEqualityComparer.Instance);
        if (!_callbacks.Add(cardPlay)) return Task.CompletedTask;
        WesternPracticeCardKind kind = cardPlay.Card.Type switch
        {
            CardType.Attack => WesternPracticeCardKind.Attack,
            CardType.Skill => WesternPracticeCardKind.Skill,
            CardType.Power => WesternPracticeCardKind.Power,
            _ => WesternPracticeCardKind.Other,
        };
        Practice.RecordPlay(state.TurnNumber, $"{state.TurnNumber}:{Practice.Plays.Count}",
            cardPlay.Card.Id.ToString(), kind, cardPlay.IsAutoPlay);
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } state)
            Practice.CloseTurn(state.TurnNumber);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.PlayerCombatState is not { } state) return;
        // Consume before awaited commands. Loss of exclusivity cancels, rather than banks, the reward.
        WesternPracticeReward reward = Practice.TakeReward(state.TurnNumber);
        if (!IsSoleDoctrine || reward == default) return;
        Flash();
        if (reward.Energy > 0) await PlayerCmd.GainEnergy(reward.Energy, Owner);
        if (reward.Block > 0) await CreatureCmd.GainBlock(Owner.Creature, reward.Block,
            ValueProp.Unpowered, cardPlay: null, fast: true);
        if (reward.Draw > 0) await CardPileCmd.Draw(choiceContext, reward.Draw, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Practice.EndCombat();
        _callbacks = null;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
