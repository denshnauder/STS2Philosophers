using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class ParmenidesInquiry : EventModel
{
    private const string LocalizationRoot = "PARMENIDES_INQUIRY";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(ParmenidesInquiryStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        ParmenidesInquiryState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("ParmenidesInquiry cannot start pre-finished.");
        }

        ParmenidesInquiryState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(ParmenidesInquiryAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.ParmenidesInquiry is not { } state
                || !ParmenidesInquiryFlow.TryPrepare(state, action)) return;

            await SaveManager.Instance.SaveRun(null);
            if (!CommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Parmenides action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == ParmenidesInquiryAction.Leave) SetEventFinished(PageDescription(state.Stage));
            else SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] Parmenides inquiry action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool CommitPending(ParmenidesInquiryState state) =>
        Owner?.RunState is RunState run
        && PhilosophyRunStateService.GetOrCreate(run).WesternJourney is { } journey
        && ParmenidesInquiryFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());
    private void ResumePendingInMemory(ParmenidesInquiryState state)
    {
        if (state.PendingAction != ParmenidesInquiryAction.None
            && !CommitPending(state))
        {
            throw new InvalidDataException("The saved Parmenides action could not be resumed.");
        }
    }

    private void SetPage(ParmenidesInquiryStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(ParmenidesInquiryStage stage) =>
        ParmenidesInquiryFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{ParmenidesInquiryFlow.StageKey(stage)}.options.{ParmenidesInquiryFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private ParmenidesInquiryState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).ParmenidesInquiry is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException("ParmenidesInquiry requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ParmenidesInquiryStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{ParmenidesInquiryFlow.StageKey(stage)}.description");
}
