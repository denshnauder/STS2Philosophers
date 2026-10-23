using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public sealed class HeraclitusLyre : WesternPracticeRelic
{
    internal bool CanOfferCopernicus => IsMutable && Owner is not null && IsSoleDoctrine;
    internal bool CanOfferGalileo => CanOfferNativeQuestion;
    protected override string ProblemId => "BEING_AND_CHANGE";
    protected override string ResourceName => "heraclitus_lyre";

    private GalileoRepeatedPaymentState? _galileo;
    private string? _invalidGalileoPayload;
    private GalileoRepeatedPaymentState Measurements => _galileo ??= GalileoRepeatedPaymentState.Create();

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
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
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
