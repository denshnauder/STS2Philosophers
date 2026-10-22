using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class AristotleCategoryTablet : WesternPracticeRelic
{
    protected override string ProblemId => "LANGUAGE_AND_MEANING";
    protected override string ResourceName => "aristotle_category_tablet";
    internal bool CanOfferOckham => IsMutable && Owner is not null && IsSoleDoctrine;

    protected override bool UseNamedCategory
    {
        get
        {
            if (!CanOfferOckham) return false;
            RunManager manager = RunManager.Instance;
            RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
            return run is not null && run.Players.Count == 1 && ReferenceEquals(run.Players[0], Owner)
                && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
                && shared?.WesternJourney is { } journey && shared.GorgiasSpeechInquiry is { } gorgias
                && shared.OckhamNamesInquiry is { Closed: true, Disposition: OckhamNamesDisposition.Named } state
                && OckhamNamesInquiryFlow.IsRouteConsistent(state, journey, gorgias);
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips => UseNamedCategory
        ? base.ExtraHoverTips.Concat([new HoverTip(
            new LocString("relics", "ARISTOTLE_CATEGORY_TABLET.ockham.title"),
            new LocString("relics", "ARISTOTLE_CATEGORY_TABLET.ockham.description"))])
        : base.ExtraHoverTips;
}
