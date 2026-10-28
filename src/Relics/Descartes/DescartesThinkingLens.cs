using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class DescartesThinkingLens : WesternPracticeRelic
{
    protected override string ProblemId => "SELF_AND_OTHER";
    protected override string ResourceName => "descartes_thinking_lens";

    private SpinozaCombatPracticeState? _spinozaPractice;
    private string? _invalidSpinozaPayload;
    private HashSet<CardPlay>? _spinozaCallbacks;
    private LeibnizCombatPracticeState? _leibnizPractice;
    private string? _invalidLeibnizPayload;
    private HashSet<CardPlay>? _leibnizCallbacks;

    private SpinozaCombatPracticeState SpinozaPractice => _spinozaPractice ??= new();

    private LeibnizCombatPracticeState LeibnizPractice =>
        _leibnizPractice ??= LeibnizCombatPracticeState.CreateDefault();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LeibnizPracticePayload
    {
        get => _invalidLeibnizPayload
            ?? _leibnizPractice?.Encode() ?? LeibnizCombatPracticeState.CreateDefault().Encode();
        private set
        {
            AssertMutable();
            if (LeibnizCombatPracticeState.TryRestore(value, out LeibnizCombatPracticeState restored))
            {
                _leibnizPractice = restored;
                _invalidLeibnizPayload = null;
            }
            else _invalidLeibnizPayload = value;
        }
    }

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
            if (_invalidLeibnizPayload is null
                && TryGetLeibnizDisposition(out LeibnizDisposition leibnizDisposition))
            {
                string branch = leibnizDisposition.ToString().ToLowerInvariant();
                tips = tips.Concat([new HoverTip(
                    new LocString("relics", $"DESCARTES_THINKING_LENS.leibniz.{branch}.title"),
                    new LocString("relics", $"DESCARTES_THINKING_LENS.leibniz.{branch}.description"))]);
            }
            return tips;
        }
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (_invalidSpinozaPayload is null
            && TryGetSpinozaDisposition(out _)
            && TrySpinozaCombatKey(out string key)
            && SpinozaPractice.BeginCombat(key))
            _spinozaCallbacks = null;

        if (_invalidLeibnizPayload is null
            && TryGetLeibnizDisposition(out _)
            && TrySpinozaCombatKey(out string leibnizKey)
            && LeibnizPractice.BeginCombat(leibnizKey))
            _leibnizCallbacks = null;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (side != CombatSide.Player
            || Owner is not { } owner
            || !participants.Contains(owner.Creature)
            || owner.PlayerCombatState is not { } state
            || state.TurnNumber <= 0
            || _invalidLeibnizPayload is not null
            || !TryGetLeibnizDisposition(out _)
            || !TrySpinozaCombatKey(out string key)) return;
        if (LeibnizPractice.BeginCombat(key)) _leibnizCallbacks = null;
        LeibnizPractice.StartTurn(key, state.TurnNumber);
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStartLate(choiceContext, player);
        if (!ReferenceEquals(player, Owner)
            || Owner?.PlayerCombatState is not { TurnNumber: 2 } state
            || _invalidLeibnizPayload is not null
            || !TryGetLeibnizDisposition(out LeibnizDisposition disposition)
            || !TrySpinozaCombatKey(out string key)) return;
        if (!LeibnizPractice.TryClaimEnergy(key, state.TurnNumber, disposition)) return;
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner is null || !ReferenceEquals(cardPlay.Player, Owner) || cardPlay.IsAutoPlay)
        {
            await base.AfterCardPlayed(choiceContext, cardPlay);
            return;
        }

        if (_invalidLeibnizPayload is null
            && TryGetLeibnizDisposition(out LeibnizDisposition leibnizDisposition)
            && TrySpinozaCombatKey(out string leibnizKey)
            && Owner?.PlayerCombatState is { TurnNumber: > 0 } leibnizState)
        {
            LeibnizCombatPracticeState practice = LeibnizPractice;
            if (practice.BeginCombat(leibnizKey)) _leibnizCallbacks = null;
            if (!(_leibnizCallbacks ??= new HashSet<CardPlay>(ReferenceEqualityComparer.Instance))
                .Add(cardPlay))
            {
                await base.AfterCardPlayed(choiceContext, cardPlay);
                return;
            }
            int turn = leibnizState.TurnNumber;
            await base.AfterCardPlayed(choiceContext, cardPlay);
            if (_invalidLeibnizPayload is not null || !ReferenceEquals(_leibnizPractice, practice)
                || !TryGetLeibnizDisposition(out LeibnizDisposition currentLeibniz)
                || currentLeibniz != leibnizDisposition
                || !TrySpinozaCombatKey(out string currentLeibnizKey)
                || currentLeibnizKey != leibnizKey
                || Owner?.PlayerCombatState?.TurnNumber != turn) return;

            LeibnizPracticeCardKind kind = cardPlay.Card.Type switch
            {
                CardType.Attack => LeibnizPracticeCardKind.Attack,
                CardType.Skill => LeibnizPracticeCardKind.Skill,
                _ => LeibnizPracticeCardKind.Other,
            };
            LeibnizPracticeReward leibnizReward = practice.ObserveManual(
                leibnizKey, turn, leibnizDisposition, kind);
            switch (leibnizReward)
            {
                case LeibnizPracticeReward.Block4:
                    Flash();
                    await CreatureCmd.GainBlock(Owner.Creature, 4, ValueProp.Unpowered,
                        cardPlay: null, fast: true);
                    break;
                case LeibnizPracticeReward.Draw1:
                    Flash();
                    await CardPileCmd.Draw(choiceContext, 1m, Owner);
                    break;
            }
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
        Player? owner = Owner;
        if (owner is null) return;

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
                ICombatState? combatState = owner.Creature.CombatState;
                if (combatState is null || !combatState.Enemies.Contains(target)) break;
                Flash();
                await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m,
                    owner.Creature, cardPlay.Card);
                break;
            case SpinozaPracticeReward.Draw:
                Flash();
                await CardPileCmd.Draw(choiceContext, 1m, owner);
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

    private bool TryGetLeibnizDisposition(out LeibnizDisposition disposition)
    {
        disposition = LeibnizDisposition.None;
        if (!IsMutable || Owner is null
            || !Owner.Relics.Any(relic => ReferenceEquals(relic, this))) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex != 2 || run.Players.Count != 1
            || manager.NetService?.Type != NetGameType.Singleplayer
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.CurrentDoctrine is not { ThinkerId: "DESCARTES", DoctrineId: "DESCARTES_THINKING_LENS" }
            || shared.WesternJourney is not { } journey
            || journey.CurrentNodeId != LeibnizHarmonyState.AdoptedNodeId
            || !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
            || !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.PredecessorEdgeId)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_024")
            || journey.LastFixedAct != 3
            || journey.RetainedEnding
            || shared.LeibnizHarmony is not { } state
            || !state.Closed || !state.EdgeCommitted || !state.IsUsable()
            || state.InstanceId != LeibnizHarmonyState.BuildInstanceId(run.Rng.Seed)
            || state.SpinozaInstanceId != $"SPINOZA_CAUSE_NETWORK:{run.Rng.Seed}:2"
            || state.Disposition is not (LeibnizDisposition.Independent or LeibnizDisposition.Coordinated)
            || shared.SpinozaCauseNetwork is not { } spinoza
            || !LeibnizHarmonyFlow.IsRouteConsistent(state, journey, spinoza))
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
