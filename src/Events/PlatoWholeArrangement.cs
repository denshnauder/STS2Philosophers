using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class PlatoWholeArrangement : EventModel
{
    private const string LocalizationRoot = "PLATO_WHOLE_ARRANGEMENT";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(PlatoArrangementStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        PlatoWholeArrangementState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("PlatoWholeArrangement cannot start pre-finished.");
        }

        PlatoWholeArrangementState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(PlatoArrangementAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0)
        {
            return;
        }

        try
        {
            if (Owner?.RunState is not RunState run)
            {
                return;
            }

            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.PlatoWholeArrangement is not { } state
                || philosophy.WesternJourney is not { } journey
                || !PlatoWholeArrangementFlow.TryPrepare(state, action))
            {
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (!PlatoWholeArrangementFlow.TryCommitPending(
                    state,
                    journey,
                    WesternRouteGraph.LoadEmbedded()))
            {
                Log.Error($"[STS2Philosophers] Plato action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == PlatoArrangementAction.Leave)
            {
                SetEventFinished(PageDescription(state.Stage));
            }
            else
            {
                SetPage(state.Stage);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Plato whole arrangement action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(PlatoWholeArrangementState state)
    {
        if (state.PendingAction == PlatoArrangementAction.None
            || Owner?.RunState is not RunState run)
        {
            return;
        }

        PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
        if (philosophy.WesternJourney is not { } journey
            || !PlatoWholeArrangementFlow.TryCommitPending(
                state,
                journey,
                WesternRouteGraph.LoadEmbedded()))
        {
            throw new InvalidDataException("The saved Plato action could not be resumed.");
        }

    }

    private void SetPage(PlatoArrangementStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(PlatoArrangementStage stage) =>
        PlatoWholeArrangementFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{PlatoWholeArrangementFlow.StageKey(stage)}.options.{PlatoWholeArrangementFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private PlatoWholeArrangementState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).PlatoWholeArrangement is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException("PlatoWholeArrangement requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(PlatoArrangementStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{PlatoWholeArrangementFlow.StageKey(stage)}.description");
}
