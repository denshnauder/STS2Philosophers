using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public sealed class AristotleCategoryTablet : WesternPracticeRelic
{
    protected override string ProblemId => "LANGUAGE_AND_MEANING";
    protected override string ResourceName => "aristotle_category_tablet";
    internal bool CanOfferOckham => IsMutable && Owner is not null && IsSoleDoctrine;
    internal bool CanOfferBacon => CanOfferOckham;
    private BaconCombatComparisonState? _baconComparison;
    private string? _invalidBaconPayload;
    private HashSet<CardPlay>? _baconCallbacks;
    private BaconCombatComparisonState Comparison => _baconComparison ??= BaconCombatComparisonState.Create();
    protected override bool UseCategoryDiscount => !TryGetBaconDisposition(out _);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string BaconComparisonPayload
    {
        get => _invalidBaconPayload ?? _baconComparison?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (BaconCombatComparisonState.TryRestore(value, out BaconCombatComparisonState restored))
            {
                _baconComparison = restored;
                _invalidBaconPayload = null;
            }
            else _invalidBaconPayload = value;
        }
    }

    protected override bool UseNamedCategory
    {
        get
        {
            if (!CanOfferOckham) return false;
            RunManager manager = RunManager.Instance;
            RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
            if (run is null || run.Players.Count != 1 || !ReferenceEquals(run.Players[0], Owner)
                || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
                || shared?.WesternJourney is not { } journey || shared.GorgiasSpeechInquiry is not { } gorgias
                || shared.OckhamNamesInquiry is not { Closed: true, Disposition: OckhamNamesDisposition.Named } state)
                return false;
            if (shared.BaconSampleLedger is { Closed: true, FixedSlotResolved: true, EdgeCommitted: false } bacon
                && BaconSampleLedgerFlow.IsRouteConsistent(bacon, journey, state, gorgias))
                journey = BaconSampleLedgerFlow.OckhamHistoryJourney(journey);
            return OckhamNamesInquiryFlow.IsRouteConsistent(state, journey, gorgias);
        }
    }

    public override int DisplayAmount
    {
        get
        {
            if (!TryGetBaconDisposition(out BaconReportDisposition disposition)) return base.DisplayAmount;
            if (_invalidBaconPayload is not null || !TryCombatKey(out string key)) return 0;
            return Comparison.Counter(key, disposition == BaconReportDisposition.Limited ? 1 : 2,
                Owner.PlayerCombatState?.TurnNumber ?? 0);
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (TryGetBaconDisposition(out BaconReportDisposition disposition))
            {
                string branch = _invalidBaconPayload is not null ? "unavailable"
                    : disposition == BaconReportDisposition.Limited ? "limited" : "control";
                return [new HoverTip(new LocString("relics", $"ARISTOTLE_CATEGORY_TABLET.bacon.{branch}.title"),
                    new LocString("relics", $"ARISTOTLE_CATEGORY_TABLET.bacon.{branch}.description"))];
            }
            return UseNamedCategory ? base.ExtraHoverTips.Concat([new HoverTip(
                new LocString("relics", "ARISTOTLE_CATEGORY_TABLET.ockham.title"),
                new LocString("relics", "ARISTOTLE_CATEGORY_TABLET.ockham.description"))]) : base.ExtraHoverTips;
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);
        if (play.Player != Owner || play.IsAutoPlay || _invalidBaconPayload is not null
            || !TryGetBaconDisposition(out BaconReportDisposition disposition)
            || !TryCombatKey(out string key) || Owner.PlayerCombatState is not { } combat) return;
        int targetTurn = disposition == BaconReportDisposition.Limited ? 1 : 2;
        if (combat.TurnNumber != targetTurn || Comparison.Counter(key, targetTurn, combat.TurnNumber) == 0) return;
        _baconCallbacks ??= new(ReferenceEqualityComparer.Instance);
        if (!_baconCallbacks.Add(play)) return;
        BaconComparisonKind kind = play.Card.Type switch
        {
            CardType.Attack => BaconComparisonKind.Attack,
            CardType.Skill => BaconComparisonKind.Skill,
            CardType.Power => BaconComparisonKind.Power,
            _ => BaconComparisonKind.Other,
        };
        bool draw = Comparison.ObserveManual(key, targetTurn, combat.TurnNumber, kind);
        InvokeDisplayAmountChanged();
        if (!draw) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, disposition == BaconReportDisposition.Limited ? 2m : 3m, Owner);
    }

    private bool TryGetBaconDisposition(out BaconReportDisposition disposition)
    {
        disposition = BaconReportDisposition.None;
        if (!CanOfferBacon) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex != 2 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey || shared.OckhamNamesInquiry is not { } ockham
            || shared.GorgiasSpeechInquiry is not { } gorgias
            || shared.BaconSampleLedger is not { Closed: true, EdgeCommitted: true } state
            || !BaconSampleLedgerFlow.IsRouteConsistent(state, journey, ockham, gorgias)
            || state.Disposition is not (BaconReportDisposition.Limited or BaconReportDisposition.Control)) return false;
        disposition = state.Disposition;
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
