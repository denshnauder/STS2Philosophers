using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2MinimalMod;

public sealed class KongziQingYuPei : RelicModel
{
    private KongziQingYuPeiState _kongziQingYuPei;
    private CardPile? _observedHand;
    private PlayerCombatState? _observedPlayerCombatState;
    private int _virtue;
    private bool _hasPendingReward;
    private bool _hasResolvedPhilosophersGazeContinuation;

    public override RelicRarity Rarity => RelicRarity.None;

    public override bool ShowCounter => true;

    public override int DisplayAmount => IsMutable ? Virtue : 0;

    public override string PackedIconPath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Virtue
    {
        get => _virtue;
        private set
        {
            AssertMutable();
            _virtue = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasPendingReward
    {
        get => _hasPendingReward;
        private set
        {
            AssertMutable();
            _hasPendingReward = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasResolvedPhilosophersGazeContinuation
    {
        get => _hasResolvedPhilosophersGazeContinuation;
        private set
        {
            AssertMutable();
            _hasResolvedPhilosophersGazeContinuation = value;
        }
    }

    internal void RecordPhilosophersGazeContinuation()
    {
        HasResolvedPhilosophersGazeContinuation = true;
    }

    public static int GetVirtue(Player player)
    {
        return (player.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)?.Virtue ?? 0;
    }

    internal void SetVirtueForDebug(int virtue)
    {
        Virtue = virtue;
        UpdatePresentation();
    }

    public override Task BeforeCombatStart()
    {
        StopObservingNativePlayability();
        _kongziQingYuPei.BeginCombat();
        HasPendingReward = false;
        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        // HasPendingReward is saved, while KongziQingYuPeiState is deliberately combat-local.
        // Treat the saved entitlement as the authoritative lock so a model rebuild,
        // reconnect, or synchronization pass can never reopen a completed result.
        if (HasPendingReward || _kongziQingYuPei.IsResolved)
        {
            StopObservingNativePlayability();
            UpdatePresentation();
            return Task.CompletedTask;
        }

        bool allLivingEnemiesAreNonAttacking = AllLivingEnemiesAreNonAttacking(combatState);
        _kongziQingYuPei.BeginTurn(allLivingEnemiesAreNonAttacking);
        if (allLivingEnemiesAreNonAttacking)
        {
            Log.Info("[STS2MinimalMod] Green Jade Pendant opened an opportunity turn because every living enemy shows a non-Attack intent.");
        }

        StartObservingNativePlayability();
        ObserveNativeAttackOpportunity();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStartLate(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player == Owner && !HasPendingReward && !_kongziQingYuPei.IsResolved)
        {
            ObserveNativeAttackOpportunity();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card.Owner == Owner && !HasPendingReward && !_kongziQingYuPei.IsResolved)
        {
            ObserveNativeAttackOpportunity();
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!HasPendingReward
            && !_kongziQingYuPei.IsResolved
            && side == CombatSide.Player
            && participants.Contains(Owner.Creature))
        {
            ObserveNativeAttackOpportunity();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        if (HasPendingReward || _kongziQingYuPei.HasLockedReward)
        {
            if (cardPlay.Card.Type == CardType.Attack)
            {
                Log.Info("[STS2MinimalMod] Green Jade Pendant ignored a later Attack because this combat's reward is already locked.");
            }

            return Task.CompletedTask;
        }

        if (_kongziQingYuPei.IsResolved)
        {
            return Task.CompletedTask;
        }

        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState is not null
            && _kongziQingYuPei.CancelOpportunityIfEnemiesAttack(
                AllLivingEnemiesAreNonAttacking(combatState)))
        {
            Log.Info("[STS2MinimalMod] Green Jade Pendant canceled a stale opportunity because a living enemy currently shows an Attack intent.");
            UpdatePresentation();
            return Task.CompletedTask;
        }

        ObserveNativeAttackOpportunity();
        if (cardPlay.Card.Type == CardType.Attack && _kongziQingYuPei.RecordAttackPlayed())
        {
            StopObservingNativePlayability();
            UpdatePresentation();
            Log.Info("[STS2MinimalMod] Green Jade Pendant opportunity was lost by playing an Attack before the reward was locked.");
            Flash();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!HasPendingReward
            && !_kongziQingYuPei.IsResolved
            && cardPlay.Card.Owner == Owner)
        {
            ObserveNativeAttackOpportunity();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!HasPendingReward
            && !_kongziQingYuPei.IsResolved
            && side == CombatSide.Player
            && participants.Contains(Owner.Creature))
        {
            RefreshOpportunityFromCurrentIntents();
            ObserveNativeAttackOpportunity();
            ResolveCurrentTurn();
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        StopObservingNativePlayability();
        if (HasPendingReward)
        {
            UpdatePresentation();
            return Task.CompletedTask;
        }

        // BeforeSideTurnEnd is the authoritative resolution point because the
        // player's hand and native CanPlay state are still intact there. This is
        // retained as a fallback for unusual action/replay paths that skip it.
        RefreshOpportunityFromCurrentIntents();
        ObserveNativeAttackOpportunity();
        ResolveCurrentTurn();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        StopObservingNativePlayability();
        _kongziQingYuPei.EndCombat();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override bool TryModifyCardRewardOptions(
        Player player,
        List<CardCreationResult> rewardOptions,
        CardCreationOptions creationOptions)
    {
        if (player != Owner
            || !HasPendingReward
            || creationOptions.Source != CardCreationSource.Encounter
            || !creationOptions.Flags.HasFlag(CardCreationFlags.IsCardReward)
            || !creationOptions.Flags.HasFlag(CardCreationFlags.IsFromCombat)
            || creationOptions.Flags.HasFlag(CardCreationFlags.NoModifyHooks))
        {
            return false;
        }

        HashSet<ModelId> displayedIds = rewardOptions
            .Select(option => option.Card.Id)
            .ToHashSet();

        List<CardModel> eligibleCards = creationOptions
            .GetPossibleCards(player)
            .Where(card => !displayedIds.Contains(card.Id))
            .Where(card => IsAllowedForPlayerCount(player, card))
            .ToList();

        bool hasUncommon = eligibleCards.Any(card => card.Rarity == CardRarity.Uncommon);
        bool hasRare = eligibleCards.Any(card => card.Rarity == CardRarity.Rare);

        if (!hasUncommon && !hasRare)
        {
            Log.Warn("[STS2MinimalMod] Green Jade Pendant could not append a reward card: no distinct Uncommon or Rare candidate exists.");
            return false;
        }

        KongziQingYuPeiRewardRarityDecision decision = KongziQingYuPeiRewardPolicy.RollAndResolveRarity(
            () => RollNativeRarity(player, creationOptions) == CardRarity.Rare,
            hasUncommon,
            hasRare);

        if (!decision.Rarity.HasValue)
        {
            Log.Warn("[STS2MinimalMod] Green Jade Pendant could not append a reward card after its single native rarity roll: no distinct Uncommon fallback exists.");
            return false;
        }

        if (decision.FellBackFromRare)
        {
            Log.Warn("[STS2MinimalMod] Green Jade Pendant rolled Rare but no distinct Rare candidate exists; deterministically falling back to Uncommon.");
        }

        CardRarity targetRarity = decision.Rarity == KongziQingYuPeiBonusCardRarity.Rare
            ? CardRarity.Rare
            : CardRarity.Uncommon;

        Func<CardModel, bool>? originalFilter = creationOptions.CardPoolFilter;
        CardCreationOptions extraOptions = new(
            creationOptions.CardPools,
            creationOptions.Source,
            CardRarityOddsType.Uniform,
            card => (originalFilter is null || originalFilter(card))
                && card.Rarity == targetRarity
                && !displayedIds.Contains(card.Id));

        extraOptions.WithFlags(
            creationOptions.Flags
            | CardCreationFlags.NoModifyHooks
            | CardCreationFlags.NoCardPoolModifications
            | CardCreationFlags.NoRarityModification);

        if (creationOptions.RngOverride is not null)
        {
            extraOptions.WithRngOverride(creationOptions.RngOverride);
        }

        try
        {
            CardModel? extraCard = CardFactory
                .CreateForReward(player, 1, extraOptions)
                .FirstOrDefault()
                ?.Card;

            if (extraCard is null)
            {
                Log.Warn("[STS2MinimalMod] Green Jade Pendant could not append a reward card: native reward generation returned no card.");
                return false;
            }

            CardCreationResult result = new(extraCard);
            result.ModifyCard(extraCard, this);
            rewardOptions.Add(result);
            Log.Info($"[STS2MinimalMod] Green Jade Pendant appended locked bonus reward card {extraCard.Id} at option {rewardOptions.Count}.");
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2MinimalMod] Green Jade Pendant skipped its bonus card because native reward generation failed safely: {exception}");
            return false;
        }
    }

    private static CardRarity RollNativeRarity(Player player, CardCreationOptions options)
    {
        bool changesFutureOdds = options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange)
            || (options.Source == CardCreationSource.Encounter
                && options.RarityOdds is CardRarityOddsType.RegularEncounter
                    or CardRarityOddsType.EliteEncounter
                    or CardRarityOddsType.BossEncounter);

        return changesFutureOdds
            ? player.PlayerOdds.CardRarity.Roll(options.RarityOdds)
            : player.PlayerOdds.CardRarity.RollWithBaseOdds(options.RarityOdds);
    }

    private static bool IsAllowedForPlayerCount(Player player, CardModel card)
    {
        return player.RunState.Players.Count > 1
            ? card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly
            : card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly;
    }

    private static bool AllLivingEnemiesAreNonAttacking(ICombatState combatState)
    {
        List<Creature> livingEnemies = combatState.Enemies
            .Where(enemy => enemy.IsAlive)
            .ToList();

        return livingEnemies.Count > 0
            && livingEnemies.All(enemy => enemy.Monster is { IntendsToAttack: false });
    }

    private void StartObservingNativePlayability()
    {
        StopObservingNativePlayability();
        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState is null)
        {
            return;
        }

        _observedPlayerCombatState = playerCombatState;
        _observedHand = playerCombatState.Hand;
        _observedHand.ContentsChanged += ObserveNativeAttackOpportunity;
        playerCombatState.EnergyChanged += OnResourcesChanged;
        playerCombatState.StarsChanged += OnResourcesChanged;
    }

    private void StopObservingNativePlayability()
    {
        if (_observedHand is not null)
        {
            _observedHand.ContentsChanged -= ObserveNativeAttackOpportunity;
            _observedHand = null;
        }

        if (_observedPlayerCombatState is not null)
        {
            _observedPlayerCombatState.EnergyChanged -= OnResourcesChanged;
            _observedPlayerCombatState.StarsChanged -= OnResourcesChanged;
            _observedPlayerCombatState = null;
        }
    }

    private void OnResourcesChanged(int oldValue, int newValue)
    {
        ObserveNativeAttackOpportunity();
    }

    private void ObserveNativeAttackOpportunity()
    {
        ICombatState? combatState = Owner.Creature.CombatState;
        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        if (combatState is null || playerCombatState is null)
        {
            return;
        }

        bool hasPlayableAttack = playerCombatState.Hand.Cards.Any(card =>
            card.Owner == Owner
            && card.Type == CardType.Attack
            && card.CanPlay()
            && HasLegalTarget(card, combatState));

        if (_kongziQingYuPei.ObserveAttackOpportunity(hasPlayableAttack))
        {
            Log.Info("[STS2MinimalMod] Green Jade Pendant observed a real playable Attack opportunity this turn.");
        }
    }

    private void RefreshOpportunityFromCurrentIntents()
    {
        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState is not null
            && _kongziQingYuPei.CancelOpportunityIfEnemiesAttack(
                AllLivingEnemiesAreNonAttacking(combatState)))
        {
            Log.Info("[STS2MinimalMod] Green Jade Pendant canceled a stale opportunity at turn end because a living enemy shows an Attack intent.");
        }
    }

    private void ResolveCurrentTurn()
    {
        if (_kongziQingYuPei.EndTurn() != KongziQingYuPeiTurnResolution.VirtuousConduct)
        {
            return;
        }

        Virtue++;
        HasPendingReward = true;
        Log.Info($"[STS2MinimalMod] Green Jade Pendant locked this combat's bonus card reward; Virtue is now {Virtue}.");
        Flash();
    }

    private bool HasLegalTarget(CardModel card, ICombatState combatState)
    {
        IReadOnlyList<Creature> livingOpponents = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(creature => creature.IsAlive)
            .ToList();

        if (card.TargetType is TargetType.AllEnemies or TargetType.RandomEnemy)
        {
            return livingOpponents.Count > 0 && card.IsValidTarget(null);
        }

        if (card.IsValidTarget(null))
        {
            return true;
        }

        return combatState.Creatures.Any(card.IsValidTarget);
    }

    private void UpdatePresentation()
    {
        Status = HasPendingReward
            ? RelicStatus.Active
            : _kongziQingYuPei.Outcome switch
        {
            KongziQingYuPeiBattleOutcome.VirtuousConduct => RelicStatus.Active,
            KongziQingYuPeiBattleOutcome.LostOpportunity => RelicStatus.Disabled,
            _ => RelicStatus.Normal,
        };
        InvokeDisplayAmountChanged();
    }
}
