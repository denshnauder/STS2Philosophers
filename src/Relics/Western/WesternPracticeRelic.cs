using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public abstract class WesternPracticeRelic : RelicModel
{
    private readonly record struct CostQuote(
        decimal CostBeforeThisRelic,
        bool CostsX,
        WesternEntryCostEffect Effect);

    protected abstract string ProblemId { get; }
    protected abstract string ResourceName { get; }

    private WesternEntryPracticeState? _practice;
    private HashSet<object>? _callbacks;
    private CardModel? _currentCard;
    private CardModel? _practiceCard;
    private CardModel? _flowReleasePrepared;
    private bool _restoreTrackedCards;
    private readonly Dictionary<CardModel, CostQuote> _costQuotes = new(ReferenceEqualityComparer.Instance);

    private WesternEntryPracticeState Practice =>
        _practice ??= new WesternEntryPracticeState { ProblemId = ProblemId };

    public override RelicRarity Rarity => RelicRarity.None;
    public override string PackedIconPath => $"res://STS2Philosophers/images/{ResourceName}.svg";
    protected override string PackedIconOutlinePath => $"res://STS2Philosophers/images/{ResourceName}_outline.svg";
    protected override string BigIconPath => PackedIconPath;
    public override bool ShowCounter => CombatManager.Instance.IsInProgress && Practice.CombatActive;
    public override int DisplayAmount => ProblemId switch
    {
        WesternEntryPracticeState.BeingAndChange => Practice.CurrentCardModelId.Length == 0 ? 0
            : Practice.CanReleaseCurrent ? 2 : 1,
        WesternEntryPracticeState.KnowledgeAndDoubt =>
            Practice.OpeningStage is WesternOpeningStage.Completed or WesternOpeningStage.Failed ? 1 : 0,
        WesternEntryPracticeState.VirtueAndHappiness => Practice.PracticeWaits,
        WesternEntryPracticeState.FreedomAndInstitutions => Practice.LawKind switch
        {
            WesternPracticeCardKind.Attack => 1,
            WesternPracticeCardKind.Skill => 2,
            _ => 0,
        },
        WesternEntryPracticeState.SelfAndOther => Practice.OtherCombatId is null ? 0 : 1,
        WesternEntryPracticeState.LanguageAndMeaning => Practice.CategoryDiscountsRemaining,
        WesternEntryPracticeState.HistoryAndPower =>
            Practice.ConventionCardModelId.Length > 0 && !Practice.ConventionUsed ? 1 : 0,
        _ => 0,
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (ProblemId != WesternEntryPracticeState.BeingAndChange || !IsMutable
                || Owner?.PlayerCombatState is null || !IsSoleDoctrine) return [];
            string key = _currentCard is null ? (Practice.FlowDrawClaimed ? "spent" : "waiting")
                : Practice.CanReleaseCurrent ? "ready" : "held";
            List<IHoverTip> tips = [new HoverTip(new LocString("relics", "HERACLITUS_LYRE.tension.title"),
                new LocString("relics", $"HERACLITUS_LYRE.tension.{key}"))];
            if (_currentCard is not null) tips.Add(new CardHoverTip(_currentCard));
            return tips;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PracticePayload
    {
        get => _practice is null ? string.Empty : WesternEntryPracticeStateCodec.Encode(_practice);
        set
        {
            AssertMutable();
            _practice = WesternEntryPracticeStateCodec.Restore(value, ProblemId);
            ResetRuntimeTracking(restoreTrackedCards: true);
        }
    }

    // Also guards debug grants: a Western carrier cannot add a second major doctrine.
    private bool IsSoleDoctrine => Owner.Relics.Count(relic => relic is WesternPracticeRelic) == 1
        && !Owner.Relics.Any(relic => relic is KongziMuduo or KongziQingYuPei or MengziXiongZhang
            or XunziShengMo or MoziMoSeZhuJian or MoziShouChengTu or LaoziWuWeiShuJian
            or LaoziShuiYu or QinGuliShouChengXie or ZhuangziDaHu or YangzhuQuanShengBi or HuishiLiWuChou);

    public override Task BeforeCombatStart()
    {
        Practice.BeginCombat(ProblemId);
        ResetRuntimeTracking();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player
            && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } state
            && Practice.BeginTurn(state.TurnNumber))
        {
            _callbacks = null;
            _currentCard = null;
            _flowReleasePrepared = null;
            _costQuotes.Clear();
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (fromHandDraw
            && card.Owner == Owner
            && card.Pile is { Type: PileType.Hand } drawnPile
            && IsSoleDoctrine
            && Practice.ObserveDraw(CardId(card), KindOf(card), card.Keywords.Contains(CardKeyword.Exhaust)))
        {
            _currentCard = card;
            // Reuse the native keyword renderer, rather than adding an overlay or popup.
            NCard.FindOnTable(card)?.UpdateVisuals(drawnPile.Type, CardPreviewMode.Normal);
            Flash();
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        return ProblemId == WesternEntryPracticeState.BeingAndChange && IsSoleDoctrine
            && card.Owner == Owner && ReferenceEquals(card, _currentCard) && Practice.CanRetainCurrent
            && card.Pile?.Type == PileType.Hand && keywords.Add(CardKeyword.Retain);
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)
    {
        if (card.Owner != Owner || !IsSoleDoctrine)
            return cardLocation;
        ResolveTrackedCards();
        if (ProblemId == WesternEntryPracticeState.VirtueAndHappiness)
            return !isAutoPlay && ReferenceEquals(card, _practiceCard)
                ? cardLocation with { pileType = PileType.Exhaust } : cardLocation;
        if (ProblemId != WesternEntryPracticeState.BeingAndChange) return cardLocation;
        if (!ReferenceEquals(card, _currentCard)) return cardLocation;
        // The engine freezes the result pile before BeforeCardPlayed, and after moving to Play.
        // Freeze the draw entitlement here too: effects during OnPlay cannot create it retroactively.
        _flowReleasePrepared = !isAutoPlay && Practice.CanReleaseCurrent ? card : null;
        return _flowReleasePrepared is not null ? cardLocation with { pileType = PileType.Exhaust } : cardLocation;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ProblemId == WesternEntryPracticeState.BeingAndChange && ReferenceEquals(card, _currentCard)
            && oldPileType == PileType.Hand && card.Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            Practice.ObserveTrackedCardDeparture(true, false);
            _currentCard = null;
            _flowReleasePrepared = null;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal cost,
        out decimal modifiedCost)
    {
        modifiedCost = cost;
        if (card.Owner != Owner || !IsSoleDoctrine) return false;

        ResolveTrackedCards();
        bool trackedPractice = ReferenceEquals(card, _practiceCard);
        WesternEntryCostChange change = Practice.GetCostChange(
            CardId(card),
            KindOf(card),
            cost,
            card.EnergyCost.CostsX,
            trackedPractice,
            card.CurrentTarget?.CombatId);
        _costQuotes[card] = new(cost, card.EnergyCost.CostsX, change.Effect);
        modifiedCost = change.Apply(cost);
        return change.Effect != WesternEntryCostEffect.None && modifiedCost != cost;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner || !IsSoleDoctrine || Owner.PlayerCombatState is not { } state)
            return;

        _callbacks ??= new(ReferenceEqualityComparer.Instance);
        if (!_callbacks.Add(cardPlay)) return;

        // The card has normally left the hand by this callback. Keep the exact reference
        // captured before play instead of trying to recover a same-model copy from the hand.
        bool trackedCurrent = ReferenceEquals(cardPlay.Card, _currentCard);
        bool trackedPractice = ReferenceEquals(cardPlay.Card, _practiceCard);
        // Distinct same-model copies often have the same PlayIndex and PlayCount.
        // The native CardPlay reference gate above identifies callbacks; the saved sequence
        // keeps subsequent Heraclitus plays distinct after a payload is restored.
        string playId = ProblemId == WesternEntryPracticeState.BeingAndChange
            ? $"Flow:{state.TurnNumber}:{Practice.RecordedPlayIds.Count}"
            : $"{state.TurnNumber}:{cardPlay.PlayIndex}:{cardPlay.PlayCount}:{CardId(cardPlay.Card)}";

        if (cardPlay.IsAutoPlay)
        {
            Practice.RecordAutomaticPlay(playId, trackedCurrent, trackedPractice);
            ClearPlayedTrackedCards(trackedCurrent, trackedPractice);
            _costQuotes.Clear();
            InvokeDisplayAmountChanged();
            return;
        }

        CostQuote quote = _costQuotes.GetValueOrDefault(cardPlay.Card,
            new(cardPlay.Resources.EnergyValue, cardPlay.Card.EnergyCost.CostsX, WesternEntryCostEffect.None));
        Creature? target = cardPlay.Target;
        bool isSingleLivingEnemyTarget = cardPlay.Card.TargetType == TargetType.AnyEnemy
            && target is { IsAlive: true, IsEnemy: true };
        WesternEntryPlayResolution resolution = Practice.RecordManualPlay(
            playId,
            CardId(cardPlay.Card),
            KindOf(cardPlay.Card),
            quote.CostBeforeThisRelic,
            quote.CostsX,
            trackedCurrent,
            trackedPractice,
            target?.CombatId,
            isSingleLivingEnemyTarget,
            quote.Effect);

        ClearPlayedTrackedCards(trackedCurrent, trackedPractice);
        _costQuotes.Clear();
        InvokeDisplayAmountChanged();
        if (!resolution.Counted) return;

        bool flowReleasePrepared = ReferenceEquals(cardPlay.Card, _flowReleasePrepared)
            && cardPlay.ResultPile == PileType.Exhaust;
        if (trackedCurrent) _flowReleasePrepared = null;
        if (resolution.DrawOne && flowReleasePrepared)
        {
            Flash();
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }

        if (resolution.ApplyEnemyStrength && Owner.Creature.CombatState is { } combatState)
        {
            Creature[] livingEnemies = combatState.Enemies.Where(enemy => enemy.IsAlive).ToArray();
            if (livingEnemies.Length > 0)
            {
                Flash();
                await PowerCmd.Apply<StrengthPower>(
                    choiceContext,
                    livingEnemies,
                    1m,
                    Owner.Creature,
                    cardPlay.Card);
            }
        }
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsSoleDoctrine || Owner.PlayerCombatState is not { } combatState) return;

        if (ProblemId is WesternEntryPracticeState.KnowledgeAndDoubt
            or WesternEntryPracticeState.VirtueAndHappiness)
            await ResolveOpeningChoice(choiceContext, combatState);

        if (ProblemId == WesternEntryPracticeState.SelfAndOther
            && Practice.OtherCombatId is uint otherCombatId)
        {
            Creature? other = Owner.Creature.CombatState?.Enemies
                .FirstOrDefault(enemy => enemy.CombatId == otherCombatId && enemy.IsAlive);
            if (other?.Monster is { } monster)
                Practice.ObserveOtherIntent(otherCombatId, monster.IntendsToAttack);
            else
                Practice.ForgetOther(otherCombatId);
            InvokeDisplayAmountChanged();
        }
    }

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsSoleDoctrine || Owner.PlayerCombatState is not { } combatState)
            return Task.CompletedTask;

        ResolveTrackedCards();
        bool currentStillInHand = _currentCard is not null && combatState.Hand.Cards.Contains(_currentCard);
        bool practiceStillInHand = _practiceCard is not null && combatState.Hand.Cards.Contains(_practiceCard);
        WesternEntryRetainResolution resolution = Practice.ResolveRetains(currentStillInHand, practiceStillInHand);
        if (!resolution.Applied) return Task.CompletedTask;

        if (resolution.RetainCurrent && _currentCard is not null)
            CardCmd.ApplySingleTurnRetain(_currentCard);
        else
            _currentCard = null;

        if (resolution.RetainPractice && _practiceCard is not null)
            CardCmd.ApplySingleTurnRetain(_practiceCard);
        else
            _practiceCard = null;

        if (resolution.RetainCurrent || resolution.RetainPractice) Flash();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature) && Practice.CloseTurn())
            InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Practice.EndCombat();
        ResetRuntimeTracking();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    private async Task ResolveOpeningChoice(
        PlayerChoiceContext choiceContext,
        PlayerCombatState combatState)
    {
        if (!Practice.TryBeginOpeningChoice()) return;

        IReadOnlyList<CardModel> eligible = ProblemId == WesternEntryPracticeState.VirtueAndHappiness
            ? combatState.Hand.Cards.Where(card => KindOf(card) is WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill).ToArray()
            : combatState.Hand.Cards.ToArray();
        int maximum = ProblemId == WesternEntryPracticeState.KnowledgeAndDoubt ? 2 : 1;
        string promptKey = ProblemId == WesternEntryPracticeState.KnowledgeAndDoubt
            ? "SOCRATES_QUESTION_CUP.selection_prompt"
            : "SOCRATES_BALANCE_WEIGHT.selection_prompt";

        try
        {
            CardModel[] selected = eligible.Count == 0
                ? []
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    eligible,
                    Owner,
                    new CardSelectorPrefs(new LocString("relics", promptKey), 0, maximum)
                    {
                        Cancelable = true,
                        RequireManualConfirmation = true,
                        ShouldGlowGold = _ => true,
                    })).Take(maximum).ToArray();

            if (!Practice.TryCommitOpeningChoice(selected.Select(CardId)))
            {
                Practice.FailOpeningChoice();
                return;
            }

            if (ProblemId == WesternEntryPracticeState.KnowledgeAndDoubt && selected.Length > 0)
            {
                await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Bottom, this);
                await CardPileCmd.Draw(choiceContext, selected.Length, Owner);
            }
            else if (ProblemId == WesternEntryPracticeState.VirtueAndHappiness)
            {
                _practiceCard = selected.SingleOrDefault();
            }

            Practice.TryCompleteOpeningChoice();
            if (selected.Length > 0) Flash();
        }
        catch
        {
            Practice.FailOpeningChoice();
            throw;
        }
        finally
        {
            InvokeDisplayAmountChanged();
        }
    }

    private void ResolveTrackedCards()
    {
        bool allowRestore = _restoreTrackedCards;
        _currentCard = ResolveTrackedCard(_currentCard, Practice.CurrentCardModelId, allowRestore,
            allowPlayPile: ProblemId == WesternEntryPracticeState.BeingAndChange);
        if (ProblemId == WesternEntryPracticeState.BeingAndChange && _currentCard is null
            && Practice.CurrentCardModelId.Length > 0)
            Practice.ObserveTrackedCardDeparture(true, false);
        _practiceCard = ResolveTrackedCard(_practiceCard, Practice.PracticeCardModelId, allowRestore,
            allowPlayPile: true);
        _restoreTrackedCards = false;
    }

    private CardModel? ResolveTrackedCard(CardModel? tracked, string cardModelId, bool allowRestore,
        bool allowPlayPile = false)
    {
        if (Owner.PlayerCombatState is not { } combatState || string.IsNullOrWhiteSpace(cardModelId))
            return null;
        if (tracked is not null)
            return (combatState.Hand.Cards.Contains(tracked)
                    || (allowPlayPile && tracked.Owner == Owner && tracked.Pile?.Type == PileType.Play))
                && CardId(tracked) == cardModelId
                ? tracked
                : null;
        if (!allowRestore) return null;

        CardModel[] matches = combatState.Hand.Cards.Where(card => CardId(card) == cardModelId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private void ClearPlayedTrackedCards(bool trackedCurrent, bool trackedPractice)
    {
        if (trackedCurrent) _currentCard = null;
        if (trackedPractice) _practiceCard = null;
    }

    private void ResetRuntimeTracking(bool restoreTrackedCards = false)
    {
        _callbacks = null;
        _currentCard = null;
        _practiceCard = null;
        _flowReleasePrepared = null;
        _restoreTrackedCards = restoreTrackedCards;
        _costQuotes.Clear();
    }

    private static string CardId(CardModel card) => card.Id.ToString();

    private static WesternPracticeCardKind KindOf(CardModel card) => card.Type switch
    {
        CardType.Attack => WesternPracticeCardKind.Attack,
        CardType.Skill => WesternPracticeCardKind.Skill,
        CardType.Power => WesternPracticeCardKind.Power,
        _ => WesternPracticeCardKind.Other,
    };
}
