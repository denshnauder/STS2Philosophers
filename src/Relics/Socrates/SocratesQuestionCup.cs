using MegaCrit.Sts2.Core.Commands;
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

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (_invalidAnselmPayload is not null
                || !TryGetAnselmDisposition(out AnselmProofDisposition disposition))
                return base.ExtraHoverTips;
            string branch = disposition.ToString().ToLowerInvariant();
            return base.ExtraHoverTips.Concat([new HoverTip(
                new LocString("relics", $"SOCRATES_QUESTION_CUP.anselm.{branch}.title"),
                new LocString("relics", $"SOCRATES_QUESTION_CUP.anselm.{branch}.description"))]);
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
}
