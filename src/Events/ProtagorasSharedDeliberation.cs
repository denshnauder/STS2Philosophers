using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class ProtagorasSharedDeliberation : EventModel
{
    private const string LocalizationRoot = "PROTAGORAS_SHARED_DELIBERATION";
    private int _resolving;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(ProtagorasDeliberationStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        ProtagorasSharedDeliberationState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        return state.Closed ? Array.Empty<EventOption>() : GenerateOptions(state.Stage);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        ProtagorasSharedDeliberationState state = RestoreStateOrThrow();
        ResumePendingInMemory(state);
        if (isPreFinished && !state.Closed)
        {
            throw new InvalidDataException("An open ProtagorasSharedDeliberation cannot be pre-finished.");
        }
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetPage(state.Stage);
    }

    private async Task Choose(ProtagorasDeliberationAction action)
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
            if (philosophy.ProtagorasSharedDeliberation is not { } state
                || philosophy.WesternJourney is not { } journey
                || !ProtagorasSharedDeliberationFlow.TryPrepare(state, action))
            {
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (!ProtagorasSharedDeliberationFlow.TryCommitPending(
                    state,
                    journey,
                    WesternRouteGraph.LoadEmbedded()))
            {
                Log.Error(
                    $"[STS2Philosophers] Protagoras action could not commit: {action}.");
                return;
            }

            await SaveManager.Instance.SaveRun(null);
            if (action == ProtagorasDeliberationAction.Leave)
            {
                SetEventFinished(PageDescription(state.Stage));
                return;
            }

            SetPage(state.Stage);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[STS2Philosophers] Protagoras shared deliberation action failed without opening another branch: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private void ResumePendingInMemory(ProtagorasSharedDeliberationState state)
    {
        if (state.PendingAction == ProtagorasDeliberationAction.None
            || Owner?.RunState is not RunState run)
        {
            return;
        }

        PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
        if (philosophy.WesternJourney is not { } journey
            || !ProtagorasSharedDeliberationFlow.TryCommitPending(
                state,
                journey,
                WesternRouteGraph.LoadEmbedded()))
        {
            throw new InvalidDataException(
                "The saved Protagoras action could not be resumed from its stable state.");
        }
    }

    private void SetPage(ProtagorasDeliberationStage stage) =>
        SetEventState(PageDescription(stage), GenerateOptions(stage));

    private IReadOnlyList<EventOption> GenerateOptions(ProtagorasDeliberationStage stage) =>
        ProtagorasSharedDeliberationFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(action),
                $"{LocalizationRoot}.pages.{ProtagorasSharedDeliberationFlow.StageKey(stage)}.options.{ProtagorasSharedDeliberationFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();

    private ProtagorasSharedDeliberationState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).ProtagorasSharedDeliberation is { } state
            && state.IsUsable())
        {
            return state;
        }

        throw new InvalidOperationException(
            "ProtagorasSharedDeliberation requires one valid saved instance.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(
        ProtagorasDeliberationStage stage) =>
        L10NLookup(
            $"{LocalizationRoot}.pages.{ProtagorasSharedDeliberationFlow.StageKey(stage)}.description");
}
