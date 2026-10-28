using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class MachiavelliRealistChallenge : EventModel
{
    private const string LocalizationRoot = "MACHIAVELLI_REALIST_CHALLENGE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_QUESTION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_ARISTOTLE"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (Owner?.RunState is not RunState run
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("MachiavelliRealistChallenge run state is unavailable.");
            return;
        }
        if (IsResolved(journey))
        {
            SetEventFinished(ResultDescription(journey));
            return;
        }
        if (isPreFinished)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("MachiavelliRealistChallenge cannot resume an unresolved finished event.");
            return;
        }
        SetEventState(InitialDescription, LockedOptions());
    }

    public override Task AfterEventStarted() => ResumeAfterEntryAsync();

    private async Task ResumeAfterEntryAsync()
    {
        if (_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
                || shared?.WesternJourney is not { } journey)
            {
                Block("MachiavelliRealistChallenge run state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("MachiavelliRealistChallenge entry persistence is unknown.");
                return;
            }
            if (IsResolved(journey))
            {
                SetEventFinished(ResultDescription(journey));
                return;
            }
            _ready = true;
            SetEventState(InitialDescription, GenerateOptions());
        }
        catch (Exception exception)
        {
            Block($"MachiavelliRealistChallenge entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(bool acceptQuestion)
    {
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
                || shared?.WesternJourney is not { } journey)
            {
                Block("MachiavelliRealistChallenge run state is unavailable.");
                return;
            }
            if (IsResolved(journey))
            {
                SetEventFinished(ResultDescription(journey));
                return;
            }

            _ready = false;
            WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
            bool committed = acceptQuestion
                ? graph.TryApply(journey, MachiavelliRealistChallengeRoute.EdgeId)
                : graph.TryDecline(journey, "Question", MachiavelliRealistChallengeRoute.QuestionAct);
            if (!committed)
            {
                Block($"MachiavelliRealistChallenge could not commit its question: {acceptQuestion}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("MachiavelliRealistChallenge committed question persistence is unknown.");
                return;
            }
            SetEventFinished(ResultDescription(journey));
        }
        catch (Exception exception)
        {
            Block($"MachiavelliRealistChallenge choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool IsResolved(WesternJourneyState journey) =>
        journey.ResolvedQuestionActs.Contains(MachiavelliRealistChallengeRoute.QuestionAct);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions() =>
    [
        new EventOption(this, () => Choose(true),
            $"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_QUESTION", Array.Empty<IHoverTip>()),
        new EventOption(this, () => Choose(false),
            $"{LocalizationRoot}.pages.INVITATION.options.KEEP_ARISTOTLE", Array.Empty<IHoverTip>()),
    ];

    private MegaCrit.Sts2.Core.Localization.LocString ResultDescription(WesternJourneyState journey) =>
        L10NLookup($"{LocalizationRoot}.pages.{(journey.CompletedEdgeIds.Contains(MachiavelliRealistChallengeRoute.EdgeId) ? "RESULT_ACCEPTED" : "RESULT_KEPT")}.description");

    private void Block(string reason)
    {
        _ready = false;
        _blocked = true;
        if (Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            if (!IsFinished) SetEventState(Description ?? InitialDescription, LockedOptions());
            RouteEventMapBoundary.Freeze(run, reason);
        }
        else Log.Error($"[STS2Philosophers] {reason}");
    }
}
