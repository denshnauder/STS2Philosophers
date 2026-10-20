using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class SocratesBalanceWeight : WesternPracticeRelic
{
    protected override string ProblemId => "VIRTUE_AND_HAPPINESS";
    protected override string ResourceName => "socrates_balance_weight";

    private PlotinusCombatPracticeState _plotinusPractice = new();
    private string? _invalidPlotinusPayload;
    private HashSet<object>? _plotinusCallbacks;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PlotinusPracticePayload
    {
        get => _invalidPlotinusPayload ?? _plotinusPractice.Encode();
        private set
        {
            AssertMutable();
            if (PlotinusCombatPracticeState.TryRestore(value, out PlotinusCombatPracticeState restored))
            {
                _plotinusPractice = restored;
                _invalidPlotinusPayload = null;
            }
            else _invalidPlotinusPayload = value;
            _plotinusCallbacks = null;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsPlotinusRoute() && _invalidPlotinusPayload is null
            ? base.ExtraHoverTips.Concat([new HoverTip(
                new LocString("relics", "SOCRATES_BALANCE_WEIGHT.plotinus.title"),
                new LocString("relics", "SOCRATES_BALANCE_WEIGHT.plotinus.description"))])
            : base.ExtraHoverTips;

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (_invalidPlotinusPayload is null) _plotinusPractice.BeginCombat();
        _plotinusCallbacks = null;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (_invalidPlotinusPayload is null && side == CombatSide.Player
            && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } playerCombatState
            && _plotinusPractice.BeginTurn(playerCombatState.TurnNumber))
            _plotinusCallbacks = null;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (_invalidPlotinusPayload is not null || !IsPlotinusRoute()
            || cardPlay.Player != Owner || Owner.PlayerCombatState is not { } combatState) return;
        _plotinusCallbacks ??= new(ReferenceEqualityComparer.Instance);
        if (!_plotinusCallbacks.Add(cardPlay)) return;
        WesternPracticeCardKind kind = cardPlay.Card.Type switch
        {
            CardType.Attack => WesternPracticeCardKind.Attack,
            CardType.Skill => WesternPracticeCardKind.Skill,
            CardType.Power => WesternPracticeCardKind.Power,
            _ => WesternPracticeCardKind.Other,
        };
        _plotinusPractice.RecordPlay(combatState.TurnNumber, cardPlay.Card.Id.ToString(), kind,
            cardPlay.IsAutoPlay);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        Creature[] members = participants.ToArray();
        await base.AfterSideTurnEnd(choiceContext, side, members);
        if (_invalidPlotinusPayload is not null || side != CombatSide.Player
            || !members.Contains(Owner.Creature)
            || Owner.PlayerCombatState is not { } combatState) return;
        int block = _plotinusPractice.CloseTurn(combatState.TurnNumber);
        if (block != 5 || !IsPlotinusRoute()) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered,
            cardPlay: null, fast: true);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await base.AfterCombatEnd(room);
        if (_invalidPlotinusPayload is null) _plotinusPractice.BeginCombat();
        _plotinusCallbacks = null;
    }

    private bool IsPlotinusRoute()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        return run is { CurrentActIndex: 2 } && run.Players.Count == 1
            && ReferenceEquals(run.Players[0], Owner) && IsSoleDoctrine
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.WesternJourney is { } journey
            && shared.PlotinusCommonGround is { EdgeCommitted: true, Closed: true } state
            && PlotinusCommonGroundFlow.IsRouteConsistent(state, journey);
    }
}
