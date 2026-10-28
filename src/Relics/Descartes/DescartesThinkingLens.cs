using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public sealed class DescartesThinkingLens : WesternPracticeRelic
{
    protected override string ProblemId => "SELF_AND_OTHER";
    protected override string ResourceName => "descartes_thinking_lens";

    private SpinozaCombatPracticeState? _spinozaPractice;
    private string? _invalidSpinozaPayload;
    private HashSet<CardPlay>? _spinozaCallbacks;

    private SpinozaCombatPracticeState SpinozaPractice => _spinozaPractice ??= new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string SpinozaPracticePayload
    {
        get => _invalidSpinozaPayload
            ?? _spinozaPractice?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (SpinozaCombatPracticeState.TryRestore(value, out SpinozaCombatPracticeState restored))
            {
                _spinozaPractice = restored;
                _invalidSpinozaPayload = null;
                _spinozaCallbacks = null;
            }
            else _invalidSpinozaPayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            IEnumerable<IHoverTip> tips = base.ExtraHoverTips;
            if (_invalidSpinozaPayload is null
                && TryGetSpinozaDisposition(out SpinozaDisposition disposition))
            {
                string branch = disposition.ToString().ToLowerInvariant();
                tips = tips.Concat([new HoverTip(
                    new LocString("relics", $"DESCARTES_THINKING_LENS.spinoza.{branch}.title"),
                    new LocString("relics", $"DESCARTES_THINKING_LENS.spinoza.{branch}.description"))]);
            }
            return tips;
        }
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (_invalidSpinozaPayload is not null
            || !TryGetSpinozaDisposition(out _)
            || !TrySpinozaCombatKey(out string key)) return;
        if (SpinozaPractice.BeginCombat(key))
            _spinozaCallbacks = null;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Player, Owner) || cardPlay.IsAutoPlay)
        {
            await base.AfterCardPlayed(choiceContext, cardPlay);
            return;
        }

        if (_invalidSpinozaPayload is not null
            || !TryGetSpinozaDisposition(out SpinozaDisposition disposition)
            || !TrySpinozaCombatKey(out string key))
        {
            await base.AfterCardPlayed(choiceContext, cardPlay);
            return;
        }

        if (SpinozaPractice.BeginCombat(key)) _spinozaCallbacks = null;
        if (!(_spinozaCallbacks ??= new HashSet<CardPlay>(ReferenceEqualityComparer.Instance))
            .Add(cardPlay))
        {
            await base.AfterCardPlayed(choiceContext, cardPlay);
            return;
        }

        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (!TryGetSpinozaDisposition(out SpinozaDisposition current) || current != disposition
            || !TrySpinozaCombatKey(out string currentKey) || currentKey != key) return;

        bool isAttack = cardPlay.Card.Type == CardType.Attack;
        bool isSkill = cardPlay.Card.Type == CardType.Skill;
        bool isSingleEnemyTarget = cardPlay.Card.TargetType == TargetType.AnyEnemy;

        SpinozaPracticeReward reward = SpinozaPractice.Observe(
            key, disposition, isAttack, isSkill, isSingleEnemyTarget);

        switch (reward)
        {
            case SpinozaPracticeReward.Weak:
                if (cardPlay.Card.TargetType != TargetType.AnyEnemy) break;
                Creature? target = cardPlay.Target;
                if (target is null || !target.IsEnemy || !target.IsAlive) break;
                ICombatState? combatState = Owner.Creature.CombatState;
                if (combatState is null || !combatState.Enemies.Contains(target)) break;
                Flash();
                await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m,
                    Owner.Creature, cardPlay.Card);
                break;
            case SpinozaPracticeReward.Draw:
                Flash();
                await CardPileCmd.Draw(choiceContext, 1m, Owner);
                break;
        }
    }

    private bool TryGetSpinozaDisposition(out SpinozaDisposition disposition)
    {
        disposition = SpinozaDisposition.None;
        if (!IsMutable || Owner is null
            || !Owner.Relics.Any(relic => ReferenceEquals(relic, this))) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex is not (1 or 2) || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || journey.CurrentNodeId != "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE"
            || !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_023")
            || !journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
            || journey.LastFixedAct is not (2 or 3)
            || shared.SpinozaCauseNetwork is not { } state
            || !state.Closed || !state.EdgeCommitted || !state.IsUsable()
            || state.Disposition is not (SpinozaDisposition.Intervened
                or SpinozaDisposition.Reordered))
            return false;
        disposition = state.Disposition;
        return true;
    }

    private bool TrySpinozaCombatKey(out string key)
    {
        key = string.Empty;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom room) return false;
        key = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        return true;
    }
}
