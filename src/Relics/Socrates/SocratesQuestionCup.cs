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

public sealed class SocratesQuestionCup : WesternPracticeRelic
{
    protected override string ProblemId => "KNOWLEDGE_AND_DOUBT";
    protected override string ResourceName => "socrates_question_cup";

    private AnselmCombatPracticeState _anselmPractice = new();
    private string? _invalidAnselmPayload;
    private NewtonCalibrationPracticeState? _newtonPractice;
    private string? _invalidNewtonPayload;
    private HashSet<CardPlay>? _newtonCallbacks;

    private NewtonCalibrationPracticeState NewtonPractice => _newtonPractice ??= new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AnselmPracticePayload
    {
        get => _invalidAnselmPayload ?? _anselmPractice.Encode();
        private set
        {
            AssertMutable();
            if (AnselmCombatPracticeState.TryRestore(value, out AnselmCombatPracticeState restored))
            {
                _anselmPractice = restored;
                _invalidAnselmPayload = null;
            }
            else _invalidAnselmPayload = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string NewtonPracticePayload
    {
        get => _invalidNewtonPayload
            ?? (_newtonPractice ?? new NewtonCalibrationPracticeState()).Encode();
        private set
        {
            AssertMutable();
            if (NewtonCalibrationPracticeState.TryRestore(value, out NewtonCalibrationPracticeState restored))
            {
                _newtonPractice = restored;
                _invalidNewtonPayload = null;
            }
            else _invalidNewtonPayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            IEnumerable<IHoverTip> tips = base.ExtraHoverTips;
            if (_invalidAnselmPayload is null
                && TryGetAnselmDisposition(out AnselmProofDisposition disposition))
            {
                string branch = disposition.ToString().ToLowerInvariant();
                tips = tips.Concat([new HoverTip(
                    new LocString("relics", $"SOCRATES_QUESTION_CUP.anselm.{branch}.title"),
                    new LocString("relics", $"SOCRATES_QUESTION_CUP.anselm.{branch}.description"))]);
            }
            if (_invalidNewtonPayload is null && TryGetNewtonSite(out NewtonSite site))
            {
                string branch = site.ToString().ToLowerInvariant();
                tips = tips.Concat([new HoverTip(
                    new LocString("relics", $"SOCRATES_QUESTION_CUP.newton.{branch}.title"),
                    new LocString("relics", $"SOCRATES_QUESTION_CUP.newton.{branch}.description"))]);
            }
            return tips;
        }
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStartLate(choiceContext, player);
        if (player != Owner || _invalidAnselmPayload is not null
            || !TryGetAnselmDisposition(out AnselmProofDisposition disposition)) return;
        int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (disposition == AnselmProofDisposition.Withdrawn ? turn != 2 : turn != 1) return;
        RunState run = RunManager.Instance.DebugOnlyGetState()!;
        if (run.CurrentRoom is not CombatRoom room) return;
        string combatKey = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        if (!_anselmPractice.TryClaim(combatKey)) return;
        Flash();
        switch (disposition)
        {
            case AnselmProofDisposition.Published:
                await PlayerCmd.GainEnergy(1m, Owner);
                break;
            case AnselmProofDisposition.Revised:
                await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Unpowered,
                    cardPlay: null, fast: true);
                break;
            case AnselmProofDisposition.Withdrawn:
                await CreatureCmd.GainBlock(Owner.Creature, 8, ValueProp.Unpowered,
                    cardPlay: null, fast: true);
                break;
        }
    }

    private bool TryGetAnselmDisposition(out AnselmProofDisposition disposition)
    {
        disposition = AnselmProofDisposition.None;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex != 2 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.PyrrhoReportedCrossing is not { } pyrrho
            || shared.AnselmUnfinishedProof is not { Closed: true } state
            || state.Disposition is not (AnselmProofDisposition.Published
                or AnselmProofDisposition.Revised or AnselmProofDisposition.Withdrawn)
            || !AnselmUnfinishedProofFlow.IsRouteConsistent(state, journey, pyrrho)) return false;
        disposition = state.Disposition;
        return true;
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (_invalidNewtonPayload is not null
            || !TryGetNewtonSite(out _)
            || !TryNewtonCombatKey(out string key)) return;
        if (NewtonPractice.StartCombat(key))
            _newtonCallbacks = null;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature)) return;
        int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (turn <= 0 || _invalidNewtonPayload is not null
            || !TryGetNewtonSite(out _)
            || !TryNewtonCombatKey(out string key)) return;
        if (NewtonPractice.StartTurn(key, turn))
            _newtonCallbacks = null;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (!ReferenceEquals(cardPlay.Player, Owner) || cardPlay.IsAutoPlay) return;
        int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (turn <= 0 || _invalidNewtonPayload is not null
            || !TryGetNewtonSite(out _)
            || !TryNewtonCombatKey(out string key)) return;
        if (!(_newtonCallbacks ??= new HashSet<CardPlay>(ReferenceEqualityComparer.Instance))
            .Add(cardPlay)) return;
        NewtonCalibrationCardKind kind = cardPlay.Card.Type switch
        {
            CardType.Attack => NewtonCalibrationCardKind.Attack,
            CardType.Skill => NewtonCalibrationCardKind.Skill,
            _ => NewtonCalibrationCardKind.Other,
        };
        NewtonPractice.RecordManualCard(key, turn, kind);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature)) return;
        int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (turn <= 0 || _invalidNewtonPayload is not null
            || !TryGetNewtonSite(out NewtonSite site)
            || !TryNewtonCombatKey(out string key)) return;
        if (!NewtonPractice.TryClaim(key, turn, site)) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Unpowered,
            cardPlay: null, fast: true);
    }

    private bool TryGetNewtonSite(out NewtonSite site)
    {
        site = NewtonSite.None;
        if (!IsMutable || Owner is null
            || !Owner.Relics.Any(relic => ReferenceEquals(relic, this))) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex != 2 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.SextusOpposedAppearances is not { } sextus
            || shared.DescartesMethodicReconstruction is not { } descartes
            || shared.NewtonTwoSites is not { } state
            || !state.Closed || !state.EdgeCommitted
            || state.Disposition is not (NewtonDisposition.Conditional
                or NewtonDisposition.Rechecked)
            || state.CalibrationSite is not (NewtonSite.Drop or NewtonSite.Tide)
            || !state.IsUsable()
            || !NewtonTwoSitesFlow.IsRouteConsistent(state, journey, sextus, descartes))
            return false;
        site = state.CalibrationSite;
        return true;
    }

    private bool TryNewtonCombatKey(out string key)
    {
        key = string.Empty;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom room) return false;
        key = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        return true;
    }
}
