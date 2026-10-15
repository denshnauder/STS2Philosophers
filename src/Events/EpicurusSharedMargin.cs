using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class EpicurusSharedMargin : EventModel
{
    private const string LocalizationRoot = "EPICURUS_SHARED_MARGIN";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(EpicurusMarginStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EpicurusSharedMarginState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("EpicurusSharedMargin cannot start pre-finished.");
        }

        EpicurusSharedMarginState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(EpicurusMarginAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.EpicurusSharedMargin is not { } state
                || !EpicurusSharedMarginFlow.TryPrepare(state, action)) return;

            await SaveManager.Instance.SaveRun(null);
            if (!EpicurusSharedMarginFlow.TryCommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Epicurus action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == EpicurusMarginAction.Leave) SetEventFinished(PageDescription(state.Stage));
            else SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Epicurus shared margin action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(EpicurusSharedMarginState state)
    {
        if (state.PendingAction != EpicurusMarginAction.None
            && !EpicurusSharedMarginFlow.TryCommitPending(state))
        {
            throw new InvalidDataException("The saved Epicurus action could not be resumed.");
        }
    }

    private void SetPage(EpicurusMarginStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(EpicurusMarginStage stage) =>
        EpicurusSharedMarginFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{EpicurusSharedMarginFlow.StageKey(stage)}.options.{EpicurusSharedMarginFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private EpicurusSharedMarginState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).EpicurusSharedMargin is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException("EpicurusSharedMargin requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(EpicurusMarginStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{EpicurusSharedMarginFlow.StageKey(stage)}.description");
}
