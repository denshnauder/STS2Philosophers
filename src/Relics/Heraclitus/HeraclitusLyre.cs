using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class HeraclitusLyre : WesternPracticeRelic
{
    internal bool CanOfferCopernicus => IsMutable && Owner is not null && IsSoleDoctrine;
    internal bool CanOfferGalileo => CanOfferNativeQuestion;
    protected override string ProblemId => "BEING_AND_CHANGE";
    protected override string ResourceName => "heraclitus_lyre";

    private GalileoRepeatedPaymentState? _galileo;
    private string? _invalidGalileoPayload;
    private SpinozaCombatPracticeState? _spinozaPractice;
    private string? _invalidSpinozaPayload;
    private HashSet<CardPlay>? _spinozaCallbacks;
    private GalileoRepeatedPaymentState Measurements => _galileo ??= GalileoRepeatedPaymentState.Create();

    private SpinozaCombatPracticeState SpinozaPractice => _spinozaPractice ??= new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string SpinozaPracticePayload
    {
        get => _invalidSpinozaPayload ?? _spinozaPractice?.Encode() ?? string.Empty;
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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string GalileoMeasurementsPayload
    {
        get => _invalidGalileoPayload ?? _galileo?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (_invalidGalileoPayload is not null) return;
            if (string.IsNullOrEmpty(value))
            {
                _galileo = null;
                return;
            }
            if (GalileoRepeatedPaymentState.TryRestore(value, out var restored))
            {
                _galileo = restored;
                _invalidGalileoPayload = null;
            }
            else _invalidGalileoPayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (!HasGalileoProtocol()) return base.ExtraHoverTips;
            List<IHoverTip> tips = [new HoverTip(new LocString("relics", "HERACLITUS_LYRE.galileo.title"),
                new LocString("relics", _invalidGalileoPayload is null
                    ? "HERACLITUS_LYRE.galileo.description" : "HERACLITUS_LYRE.galileo.unavailable"))];
            if (_invalidGalileoPayload is null && _galileo is { SampleTurn: > 0 } sample
                && TryCombatKey(out string key) && sample.CombatKey == key)
            {
                var measured = new LocString("relics", sample.Claimed
                    ? "HERACLITUS_LYRE.galileo.sample.claimed" : "HERACLITUS_LYRE.galileo.sample.pending");
                measured.Add("Energy", sample.SamplePaid); measured.Add("Turn", sample.SampleTurn);
                tips.Add(new HoverTip(new LocString("relics", "HERACLITUS_LYRE.galileo.sample.title"), measured));
                if (sample.SampleModelId.Split('.') is ["CARD", var entry]
                    && ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", entry)) is { } card)
                    tips.Add(new CardHoverTip(card));
            }
            return tips.Concat(base.ExtraHoverTips.OfType<CardHoverTip>());
        }
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (_invalidGalileoPayload is null && HasGalileoProtocol() && TryCombatKey(out string key))
            Measurements.BeginCombat(key);
        if (_invalidSpinozaPayload is null
            && TryGetSpinozaDisposition(out _)
            && TrySpinozaCombatKey(out string spinozaKey)
            && SpinozaPractice.BeginCombat(spinozaKey))
            _spinozaCallbacks = null;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player == Owner && !play.IsAutoPlay && _invalidSpinozaPayload is null
            && TryGetSpinozaDisposition(out SpinozaDisposition disposition)
            && TrySpinozaCombatKey(out string spinozaKey))
        {
            if (SpinozaPractice.BeginCombat(spinozaKey)) _spinozaCallbacks = null;
            if (!(_spinozaCallbacks ??= new HashSet<CardPlay>(ReferenceEqualityComparer.Instance)).Add(play))
            {
                await base.AfterCardPlayed(context, play);
                return;
            }
            await base.AfterCardPlayed(context, play);
            if (!TryGetSpinozaDisposition(out SpinozaDisposition current) || current != disposition
                || !TrySpinozaCombatKey(out string currentKey) || currentKey != spinozaKey
                || Owner is null) return;
            SpinozaPracticeReward reward = SpinozaPractice.Observe(
                spinozaKey, disposition, play.Card.Type == CardType.Attack,
                play.Card.Type == CardType.Skill, play.Card.TargetType == TargetType.AnyEnemy);
            switch (reward)
            {
                case SpinozaPracticeReward.Weak:
                    if (play.Card.TargetType != TargetType.AnyEnemy) break;
                    Creature? target = play.Target;
                    if (target is null || !target.IsEnemy || !target.IsAlive) break;
                    if (Owner.Creature.CombatState is not { } combatState || !combatState.Enemies.Contains(target)) break;
                    Flash();
                    await PowerCmd.Apply<WeakPower>(context, target, 1m, Owner.Creature, play.Card);
                    break;
                case SpinozaPracticeReward.Draw:
                    Flash();
                    await CardPileCmd.Draw(context, 1m, Owner);
                    break;
            }
            return;
        }

        if (play.Player == Owner && _invalidGalileoPayload is null && HasGalileoProtocol()
            && TryCombatKey(out string key) && Owner.PlayerCombatState is { } combat)
            Measurements.ObservePayment(key, play.Card.Id.ToString(), play.Resources.EnergySpent,
                combat.TurnNumber, !play.IsAutoPlay, play.Card.EnergyCost.CostsX,
                play.Card.Type is CardType.Attack or CardType.Skill);
        await base.AfterCardPlayed(context, play);
    }

    protected override Task ResolveFlowRelease(PlayerChoiceContext context, CardPlay play)
    {
        if (!HasGalileoProtocol()) return base.ResolveFlowRelease(context, play);
        // The base invokes this only for its exact tracked card, manual release
        // and native Exhaust result; this replacement deliberately removes draw 1.
        if (_invalidGalileoPayload is not null || !TryCombatKey(out string key)
            || Owner.PlayerCombatState is not { } combat
            || !Measurements.TryClaim(key, play.Card.Id.ToString(), play.Resources.EnergySpent,
                combat.TurnNumber, !play.IsAutoPlay, play.Card.EnergyCost.CostsX,
                play.ResultPile == PileType.Exhaust)) return Task.CompletedTask;
        return CardPileCmd.Draw(context, 3m, Owner);
    }

    private bool HasGalileoProtocol()
    {
        if (!CanOfferGalileo) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        return run is not null && run.CurrentActIndex == 2 && run.Players.Count == 1
            && ReferenceEquals(run.Players[0], Owner)
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.GalileoMeasuredPractice is { Closed: true, Adopted: true } state
            && state.InstanceId == $"GALILEO_MEASURED_PRACTICE:{run.Rng.Seed}:3"
            && shared.WesternJourney is { } journey && shared.DemocritusArrangement is { } democritus
            && shared.EmpedoclesRootsInquiry is { } empedocles
            && GalileoMeasuredPracticeFlow.IsRouteConsistent(state, journey, democritus, empedocles);
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
            || shared.SpinozaCauseNetwork is not { } state
            || journey.CurrentNodeId != "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE"
            // The natural Heraclitus line arrives through 110; Descartes' older
            // save path used 023. The completed edge is recorded on the state, so
            // both are the same settled disposition without rewriting old saves.
            || string.IsNullOrEmpty(state.AdoptEdge)
            || !journey.CompletedEdgeIds.Contains(state.AdoptEdge)
            || !journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
            || journey.LastFixedAct is not (2 or 3)
            || !state.Closed || !state.EdgeCommitted || !state.IsUsable()
            || state.Disposition is not (SpinozaDisposition.Intervened or SpinozaDisposition.Reordered))
            return false;
        disposition = state.Disposition;
        return true;
    }

    private static bool TrySpinozaCombatKey(out string key)
    {
        key = string.Empty;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom room) return false;
        key = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        return true;
    }

    private static bool TryCombatKey(out string key)
    {
        key = string.Empty;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom { Id: int id }) return false;
        key = $"{run.CurrentActIndex}:{run.ActFloor}:{id}";
        return true;
    }
}
