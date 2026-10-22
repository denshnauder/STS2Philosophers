using MegaCrit.Sts2.Core.Commands;
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

public sealed class RousseauUncarvedStone : WesternPracticeRelic
{
    protected override string ProblemId => "HISTORY_AND_POWER";
    protected override string ResourceName => "rousseau_uncarved_stone";
    internal bool CanOfferMachiavelli => IsMutable && Owner is not null && IsSoleDoctrine;
    private MachiavelliCombatPreparationState? _preparation;
    private string? _invalidPreparationPayload;
    private MachiavelliCombatPreparationState Preparation => _preparation ??= MachiavelliCombatPreparationState.Create();
    protected override bool UseConventionDiscount => !TryGetPreparation(out _);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MachiavelliPreparationPayload
    {
        get => _invalidPreparationPayload ?? _preparation?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (MachiavelliCombatPreparationState.TryRestore(value, out var restored))
            {
                _preparation = restored; _invalidPreparationPayload = null;
            }
            else _invalidPreparationPayload = value;
        }
    }
    public override int DisplayAmount
    {
        get
        {
            if (!TryGetPreparation(out _) || !TryCombatKey(out string key)) return base.DisplayAmount;
            if (_invalidPreparationPayload is not null) return 0;
            int counter = _preparation?.Counter(key) ?? 2;
            int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
            return turn > 2 || turn > 1 && counter == 2 ? 0 : counter;
        }
    }
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (!TryGetPreparation(out var preparation)) return base.ExtraHoverTips;
            string branch = _invalidPreparationPayload is not null ? "unavailable" : preparation.ToString().ToLowerInvariant();
            return [new HoverTip(new LocString("relics", $"ROUSSEAU_UNCARVED_STONE.machiavelli.{branch}.title"),
                new LocString("relics", $"ROUSSEAU_UNCARVED_STONE.machiavelli.{branch}.description"))];
        }
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);
        if (play.Player != Owner || play.IsAutoPlay || Owner.PlayerCombatState?.TurnNumber != 1
            || _invalidPreparationPayload is not null || !TryGetPreparation(out var preparation)
            || !TryCombatKey(out string key)) return;
        bool matches = preparation == MachiavelliPreparation.Bank
            ? play.Card.Type == CardType.Skill : play.Card.Type == CardType.Attack;
        if (Preparation.ObserveFirstManual(key, matches)) InvokeDisplayAmountChanged();
    }
    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStartLate(choiceContext, player);
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 2 || _invalidPreparationPayload is not null
            || !TryGetPreparation(out var preparation) || !TryCombatKey(out string key)
            || !Preparation.TryClaim(key)) return;
        InvokeDisplayAmountChanged();
        Flash();
        if (preparation == MachiavelliPreparation.Bank)
            await CreatureCmd.GainBlock(Owner.Creature, 8, ValueProp.Unpowered, cardPlay: null, fast: true);
        else await PlayerCmd.GainEnergy(2m, Owner);
    }
    private bool TryGetPreparation(out MachiavelliPreparation preparation)
    {
        preparation = MachiavelliPreparation.None;
        if (!CanOfferMachiavelli) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.Players.Count != 1 || !ReferenceEquals(run.Players[0], Owner)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.MachiavelliFloodPreparation is not { Closed: true, EdgeCommitted: true } state
            || !MachiavelliFloodPreparationFlow.IsRouteConsistent(state, journey)) return false;
        preparation = state.Preparation; return true;
    }
    private bool TryCombatKey(out string key)
    {
        key = string.Empty;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom { Id: int id }) return false;
        key = $"{run.CurrentActIndex}:{run.ActFloor}:{id}"; return true;
    }
}
