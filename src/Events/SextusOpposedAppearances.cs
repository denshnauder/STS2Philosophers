using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class SextusOpposedAppearances : EventModel
{
    private const string LocalizationRoot = "SEXTUS_OPPOSED_APPEARANCES";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(SextusAppearancesStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        SextusOpposedAppearancesState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return state.Closed ? Array.Empty<EventOption>() : GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        SextusOpposedAppearancesState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        if (isPreFinished && !state.Closed)
        {
            throw new InvalidDataException("An open SextusOpposedAppearances cannot be pre-finished.");
        }
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetPage(state.Stage);
    }

    private async Task Choose(SextusAppearancesAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.SextusOpposedAppearances is not { } state
                || !SextusOpposedAppearancesFlow.TryPrepare(state, action)) return;

            await SaveManager.Instance.SaveRun(null);
            if (!SextusOpposedAppearancesFlow.TryCommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Sextus action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == SextusAppearancesAction.Leave) SetEventFinished(PageDescription(state.Stage));
            else SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Sextus opposed appearances action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(SextusOpposedAppearancesState state)
    {
        if (state.PendingAction != SextusAppearancesAction.None
            && !SextusOpposedAppearancesFlow.TryCommitPending(state))
        {
            throw new InvalidDataException("The saved Sextus action could not be resumed.");
        }
    }

    private void SetPage(SextusAppearancesStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(SextusAppearancesStage stage) =>
        SextusOpposedAppearancesFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{SextusOpposedAppearancesFlow.StageKey(stage)}.options.{SextusOpposedAppearancesFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private SextusOpposedAppearancesState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).SextusOpposedAppearances is { } state
            && state.IsUsable())
        {
            return state;
        }

        throw new InvalidOperationException("SextusOpposedAppearances requires one valid saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(SextusAppearancesStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{SextusOpposedAppearancesFlow.StageKey(stage)}.description");
}
