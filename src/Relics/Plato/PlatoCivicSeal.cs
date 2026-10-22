using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
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

    private AugustineCombatPracticeState? _augustinePractice;
    private string? _invalidAugustinePayload;
    // Canonical models are copied to mutable relics. Keep the canonical field
    // empty so separate copies never share a mutable first-card receipt.
    private AquinasCombatPracticeState? _aquinasPractice;
    private string? _invalidAquinasPayload;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AugustinePracticePayload
    {
        get => _invalidAugustinePayload ?? _augustinePractice?.Encode() ?? string.Empty;
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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AquinasPracticePayload
    {
        get => _invalidAquinasPayload ?? _aquinasPractice?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (AquinasCombatPracticeState.TryRestore(value, out AquinasCombatPracticeState restored))
            {
                _aquinasPractice = restored;
                _invalidAquinasPayload = null;
            }
            else _invalidAquinasPayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (_invalidAugustinePayload is null && TryGetAugustineWork(out AugustineWork work))
            {
                string branch = work == AugustineWork.Public ? "public" : "private";
                return base.ExtraHoverTips.Concat([new HoverTip(
                    new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.title"),
                    new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.description"))]);
            }
            if (_invalidAquinasPayload is null && TryGetAquinasRule(out AquinasRule rule))
            {
                string branch = rule == AquinasRule.Published ? "published" : "emergency";
                return base.ExtraHoverTips.Concat([new HoverTip(
                    new LocString("relics", $"PLATO_CIVIC_SEAL.aquinas.{branch}.title"),
                    new LocString("relics", $"PLATO_CIVIC_SEAL.aquinas.{branch}.description"))]);
            }
            return base.ExtraHoverTips;
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (cardPlay.Player != Owner || cardPlay.IsAutoPlay || _invalidAquinasPayload is not null
            || !TryGetAquinasRule(out AquinasRule rule)) return;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom room) return;
        string combatKey = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        bool matches = rule == AquinasRule.Published && cardPlay.Card.Type == CardType.Skill
            || rule == AquinasRule.Emergency && cardPlay.Card.Type == CardType.Attack;
        if (!(_aquinasPractice ??= new()).ObserveFirstManual(combatKey, matches)) return;
        Flash();
        if (rule == AquinasRule.Published)
            await CreatureCmd.GainBlock(Owner.Creature, 5, ValueProp.Unpowered,
                cardPlay: null, fast: true);
        else
            await PlayerCmd.GainEnergy(1m, Owner);
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
        if (!(_augustinePractice ??= new()).TryClaim(combatKey)) return;
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

    private bool TryGetAquinasRule(out AquinasRule rule)
    {
        rule = AquinasRule.None;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex < 2 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.AquinasBridgeNotice is not { EdgeCommitted: true, Closed: true } state
            || state.Rule is not (AquinasRule.Published or AquinasRule.Emergency)
            || !AquinasBridgeNoticeFlow.IsRouteConsistent(state, journey)) return false;
        rule = state.Rule;
        return true;
    }
}
