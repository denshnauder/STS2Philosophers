using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class DescartesMethodicReconstruction : EventModel
{
    private const string LocalizationRoot = "DESCARTES_METHODIC_RECONSTRUCTION";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(DescartesReconstructionStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        DescartesMethodicReconstructionState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("DescartesMethodicReconstruction cannot start pre-finished.");
        }

        DescartesMethodicReconstructionState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(DescartesReconstructionAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.DescartesMethodicReconstruction is not { } state
                || !DescartesMethodicReconstructionFlow.TryPrepare(state, action)) return;

            await SaveManager.Instance.SaveRun(null);
            if (!DescartesMethodicReconstructionFlow.TryCommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Descartes action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == DescartesReconstructionAction.Leave) SetEventFinished(PageDescription(state.Stage));
            else SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Descartes methodic reconstruction action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(DescartesMethodicReconstructionState state)
    {
        if (state.PendingAction != DescartesReconstructionAction.None
            && !DescartesMethodicReconstructionFlow.TryCommitPending(state))
        {
            throw new InvalidDataException("The saved Descartes action could not be resumed.");
        }
    }

    private void SetPage(DescartesReconstructionStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(DescartesReconstructionStage stage) =>
        DescartesMethodicReconstructionFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{DescartesMethodicReconstructionFlow.StageKey(stage)}.options.{DescartesMethodicReconstructionFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private DescartesMethodicReconstructionState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).DescartesMethodicReconstruction is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException("DescartesMethodicReconstruction requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(DescartesReconstructionStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{DescartesMethodicReconstructionFlow.StageKey(stage)}.description");
}
