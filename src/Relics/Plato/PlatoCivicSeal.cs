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

public sealed class PlatoCivicSeal : WesternPracticeRelic
{
    protected override string ProblemId => "FREEDOM_AND_INSTITUTIONS";
    protected override string ResourceName => "plato_civic_seal";

    private AugustineCombatPracticeState _augustinePractice = new();
    private string? _invalidAugustinePayload;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AugustinePracticePayload
    {
        get => _invalidAugustinePayload ?? _augustinePractice.Encode();
        private set
        {
            AssertMutable();
            if (AugustineCombatPracticeState.TryRestore(value, out AugustineCombatPracticeState restored))
            {
                _augustinePractice = restored;
                _invalidAugustinePayload = null;
            }
            else _invalidAugustinePayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (_invalidAugustinePayload is not null || !TryGetAugustineWork(out AugustineWork work))
                return base.ExtraHoverTips;
            string branch = work == AugustineWork.Public ? "public" : "private";
            return base.ExtraHoverTips.Concat([new HoverTip(
                new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.title"),
                new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.description"))]);
        }
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStartLate(choiceContext, player);
        if (player != Owner || _invalidAugustinePayload is not null
            || Owner.PlayerCombatState?.TurnNumber != 1
            || !TryGetAugustineWork(out AugustineWork work)) return;
        RunState run = RunManager.Instance.DebugOnlyGetState()!;
        if (run.CurrentRoom is not CombatRoom room) return;
        string combatKey = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        if (!_augustinePractice.TryClaim(combatKey)) return;
        Flash();
        if (work == AugustineWork.Public)
            await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Unpowered,
                cardPlay: null, fast: true);
        else
            await PlayerCmd.GainEnergy(1m, Owner);
    }

    private bool TryGetAugustineWork(out AugustineWork work)
    {
        work = AugustineWork.None;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex < 1 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.AugustinePromise is not { EdgeCommitted: true, Closed: true } state
            || state.Work is not (AugustineWork.Public or AugustineWork.Private)
            || !AugustinePromiseFlow.IsRouteConsistent(state, journey)) return false;
        work = state.Work;
        return true;
    }
}
