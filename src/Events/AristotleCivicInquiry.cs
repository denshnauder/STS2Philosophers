using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class AristotleCivicInquiry : EventModel
{
    private const string LocalizationRoot = "ARISTOTLE_CIVIC_INQUIRY";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(AristotleCivicInquiryStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        AristotleCivicInquiryState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("AristotleCivicInquiry cannot start pre-finished.");
        }

        AristotleCivicInquiryState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        SetPage(state.Stage);
    }

    private async Task Choose(AristotleCivicInquiryAction action)
    {
        if (Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run) return;
            PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
            if (philosophy.AristotleCivicInquiry is not { } state
                || !AristotleCivicInquiryFlow.TryPrepare(state, action)) return;

            await SaveManager.Instance.SaveRun(null);
            if (!CommitPending(state))
            {
                Log.Error($"[STS2Philosophers] Aristotle civic action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == AristotleCivicInquiryAction.Leave) SetEventFinished(PageDescription(state.Stage));
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

    private bool CommitPending(AristotleCivicInquiryState state) =>
        Owner?.RunState is RunState run
        && PhilosophyRunStateService.GetOrCreate(run).WesternJourney is { } journey
        && AristotleCivicInquiryFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());
    private void ResumePendingInMemory(AristotleCivicInquiryState state)
    {
        if (state.PendingAction != AristotleCivicInquiryAction.None
            && !CommitPending(state))
        {
            throw new InvalidDataException("The saved Aristotle civic action could not be resumed.");
        }
    }

    private void SetPage(AristotleCivicInquiryStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(AristotleCivicInquiryStage stage) =>
        AristotleCivicInquiryFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{AristotleCivicInquiryFlow.StageKey(stage)}.options.{AristotleCivicInquiryFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private AristotleCivicInquiryState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).AristotleCivicInquiry is { } state
            && state.IsUsable()
            && !state.Closed)
        {
            return state;
        }

        throw new InvalidOperationException("AristotleCivicInquiry requires one open saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(AristotleCivicInquiryStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{AristotleCivicInquiryFlow.StageKey(stage)}.description");
}

