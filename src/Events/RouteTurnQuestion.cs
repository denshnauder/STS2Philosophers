using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// The first act's question at the entry. A run that answers it turns onto the
/// line the answer names; a run that stays keeps the line it was given. The
/// question costs no fixed slot and moves no act: it only decides where the run
/// goes next.
/// </summary>
public sealed class RouteTurnQuestion : EventModel
{
    private const string LocalizationRoot = RouteTurnQuestionOptions.LocalizationRoot;
    private int _resolving;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        RouteTurnQuestionOptions.AllKeys.Select(key =>
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.{key}"));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => Options();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        SetEventState(InitialDescription, Options());
    }

    public override Task AfterEventStarted() => Task.CompletedTask;

    private IReadOnlyList<EventOption> Options()
    {
        if (TryGetRun() is not { } pair) return Locked();
        return RouteTurnQuestionOptions.For(pair.Journey, pair.Graph)
            .Select(choice => new EventOption(this,
                () => Choose(pair.Journey, choice),
                RouteTurnQuestionOptions.OptionKey(choice),
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private static IReadOnlyList<EventOption> Locked() => [];

    private (RunState Run, PhilosophyRunState Shared, WesternJourneyState Journey, WesternRouteGraph Graph)? TryGetRun()
    {
        if (Owner?.RunState is not RunState run) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        if (shared.WesternJourney is not { } journey) return null;
        return (run, shared, journey, WesternRouteGraph.LoadEmbedded());
    }

    private async Task Choose(WesternJourneyState journey, RouteTurnQuestionOptions.Choice choice)
    {
        if (_blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
            if (!ReferenceEquals(shared.WesternJourney, journey)) return;
            WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
            if (choice.EdgeId is { } edgeId)
            {
                if (!graph.TryApply(journey, edgeId))
                {
                    Block($"RouteTurnQuestion could not take {edgeId}.");
                    return;
                }
                if (choice.RouteId is { } route && !graph.TryNameRoute(journey, route))
                {
                    Block($"RouteTurnQuestion could not name {route}.");
                    return;
                }
            }
            // Staying is an answer: the act's question is spent either way, or the
            // next question node would ask the same fork again.
            if (choice.EdgeId is null) journey.ResolvedQuestionActs.Add(1);
            if (shared.RouteTurnQuestion is { } record) record.Closed = true;
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("RouteTurnQuestion could not persist its answer.");
                return;
            }
            SetEventFinished(L10NLookup($"{LocalizationRoot}.pages.RESULT.description"));
        }
        catch (Exception exception)
        {
            Block($"RouteTurnQuestion failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void Block(string reason)
    {
        _blocked = true;
        Log.Error($"[STS2Philosophers] {reason}");
        SetEventFinished(L10NLookup($"{LocalizationRoot}.pages.RESULT.description"));
    }
}
