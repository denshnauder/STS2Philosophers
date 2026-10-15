using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class HanFeiPublicStandard : EventModel
{
    private const string LocalizationRoot = "HAN_FEI_PUBLIC_STANDARD";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(HanFeiPublicStandardStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        HanFeiPublicStandardFlow.Options(HanFeiPublicStandardStage.Invitation)
            .Select(action => L10NLookup(
                $"{LocalizationRoot}.pages.INVITATION.options.{HanFeiPublicStandardFlow.ActionKey(action)}"));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        HanFeiPublicStandardState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("HanFeiPublicStandard cannot start pre-finished.");
        }

        HanFeiPublicStandardState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(HanFeiPublicStandardAction action)
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
            if (philosophy.HanFeiPublicStandard is not { } state)
            {
                return;
            }

            if (action == HanFeiPublicStandardAction.Leave)
            {
                if (!HanFeiPublicStandardFlow.TryClose(state))
                {
                    return;
                }

                await SaveManager.Instance.SaveRun(null);
                SetEventFinished(PageDescription(state.Stage));
                return;
            }

            if (!HanFeiPublicStandardFlow.TryPrepare(state, action))
            {
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (!HanFeiPublicStandardFlow.TryCommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Han Fei action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Han Fei public standard action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(HanFeiPublicStandardState state)
    {
        if (state.PendingAction != HanFeiPublicStandardAction.None
            && !HanFeiPublicStandardFlow.TryCommitPending(state))
        {
            throw new InvalidDataException("The saved Han Fei action could not be resumed.");
        }
    }

    private void SetPage(HanFeiPublicStandardStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(HanFeiPublicStandardStage stage) =>
        HanFeiPublicStandardFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{HanFeiPublicStandardFlow.StageKey(stage)}.options.{HanFeiPublicStandardFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private HanFeiPublicStandardState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).HanFeiPublicStandard is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException(
            "HanFeiPublicStandard requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(
        HanFeiPublicStandardStage stage) =>
        L10NLookup(
            $"{LocalizationRoot}.pages.{HanFeiPublicStandardFlow.StageKey(stage)}.description");
}
