using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed partial class PhilosophersGaze : EventModel
{
    private const string LocalizationPrefix = "PHILOSOPHERS_GAZE.pages";
    private readonly PhilosophersGazeResolutionGate _resolutionGate = new();
    private Task? _completionSaveTask;
    private PhilosophersGazePage _currentPage = PhilosophersGazePage.Initial;
    internal bool HasConfirmedCompletion { get; private set; }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        // Ordinary native events cannot call the base implementation with true.
        // Inventory/business facts also close legacy checkpoints saved mid-resolution.
        if (isPreFinished || Owner is { } owner && PhilosophersGazeSaveRecovery.IsResolved(owner))
        {
            _currentPage = PhilosophersGazePage.Initial;
            SetFlowInProgress(false);
            SetEventFinished(L10NLookup($"{LocalizationPrefix}.RESTORED_RESOLVED.description"));
            return;
        }
        if (Owner is { } flowOwner
            && TryGetSavedFlowPage(flowOwner, out PhilosophersGazePage savedPage))
        {
            _currentPage = savedPage;
            SetEventState(PageDescription(savedPage), BuildEastOptions(savedPage));
            return;
        }
        if (Owner?.RunState is RunState staleRun
            && PhilosophyRunStateService.TryGet(staleRun, out PhilosophyRunState? staleShared)
            && staleShared?.PhilosophersGazeFlowInProgress == true)
        {
            // A page from the other act, or one with no renderable options, is
            // not a resumable East page. Do not let it reopen the event.
            SetFlowInProgress(false);
        }
        if (Owner is { } ambiguousOwner && PhilosophersGazeSaveRecovery.HasAmbiguousResolution(ambiguousOwner))
        {
            _currentPage = PhilosophersGazePage.Initial;
            SetEventState(L10NLookup($"{LocalizationPrefix}.RECOVERY_BLOCKED.description"),
                [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())]);
            return;
        }
        base.SetInitialEventState(false);
        _currentPage = GetCurrentActIndex(Owner) == 1
            ? GetContinuationIntroPage() ?? PhilosophersGazePage.Initial
            : PhilosophersGazePage.Initial;
    }

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription
    {
        get
        {
            if (GetCurrentActIndex(Owner) != 1)
            {
                return base.InitialDescription;
            }

            return PageDescription(GetContinuationIntroPage() ?? PhilosophersGazePage.Continuation);
        }
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (GetCurrentActIndex(Owner) != 1)
        {
            return GenerateActOneInitialOptions();
        }

        return GetContinuationIntroPage() is { } introPage
            ? BuildEastOptions(introPage)
            : GenerateContinuationOptions();
    }

    private IReadOnlyList<EventOption> GenerateActOneInitialOptions()
    {
        GeneratedCandidates candidates = GetActOneCandidates();
        List<EventOption> options = [];
        foreach (string thinkerId in candidates.CandidateIds)
        {
            options.Add(thinkerId switch
            {
                PhilosophersGazeActOneCandidatePolicy.Kongzi =>
                    Option(ShowKongziViewpoints, PhilosophersGazePage.Initial, PhilosophersGazeOption.Kongzi),
                PhilosophersGazeActOneCandidatePolicy.Mozi =>
                    Option(ShowMoziViewpoints, PhilosophersGazePage.Initial, PhilosophersGazeOption.Mozi),
                PhilosophersGazeActOneCandidatePolicy.Laozi =>
                    Option(ShowLaoziViewpoints, PhilosophersGazePage.Initial, PhilosophersGazeOption.Laozi),
                _ when WesternRouteCatalog.EntryThinkerIds.Contains(thinkerId, StringComparer.Ordinal) =>
                    WesternOption(() => ShowWesternThinker(thinkerId), thinkerId),
                _ => throw new InvalidDataException($"Unknown act-one thinker candidate: {thinkerId}"),
            });
        }

        options.Add(Option(ShowActOneDeclineConfirmation, PhilosophersGazePage.Initial, PhilosophersGazeOption.Decline));
        return options;
    }

    private IReadOnlyList<EventOption> GenerateContinuationOptions()
    {
        PhilosophersGazeContinuationOption availableOptions = GetAvailableContinuationOptions();
        List<EventOption> options = [];
        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.MengziXiongZhang))
        {
            options.Add(Option(ShowMengziViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Mengzi));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.XunziShengMo))
        {
            options.Add(Option(ShowXunziViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Xunzi));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.QinGuliShouChengXie))
        {
            options.Add(Option(ShowQinGuliViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.QinGuli));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.HuishiLiWuChou))
        {
            options.Add(Option(ShowHuishiViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Huishi));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.ZhuangziDaHu))
        {
            options.Add(Option(ShowZhuangziViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Zhuangzi));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.YangzhuQuanShengBi))
        {
            options.Add(Option(ShowYangzhuViewpoints, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Yangzhu));
        }

        if (availableOptions != PhilosophersGazeContinuationOption.None)
        {
            options.Add(Option(ShowActTwoDeclineConfirmation, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Decline));
        }

        return options;
    }

    private Task ShowKongziViewpoints() => ShowEastPage(PhilosophersGazePage.KONGZI);

    private Task ShowMoziViewpoints() => ShowEastPage(PhilosophersGazePage.MOZI);

    private Task ShowLaoziViewpoints() => ShowEastPage(PhilosophersGazePage.LAOZI);

    private Task ShowActOneDeclineConfirmation()
    {
        if (IsActiveEventRoom() && IsActOne())
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.ActOneDeclineConfirm),
                [Option(ConfirmActOneDecline, PhilosophersGazePage.ActOneDeclineConfirm, PhilosophersGazeOption.Confirm)]);
        }

        return Task.CompletedTask;
    }

    private Task ShowMengziViewpoints() => ShowEastPage(PhilosophersGazePage.MENGZI_INTRO);

    private Task ShowXunziViewpoints() => ShowEastPage(PhilosophersGazePage.XUNZI_INTRO);

    private Task ShowQinGuliViewpoints() => ShowEastPage(PhilosophersGazePage.QIN_INTRO);

    private Task ShowZhuangziViewpoints() => ShowEastPage(PhilosophersGazePage.ZHUANGZI_INTRO);

    private Task ShowHuishiViewpoints() => ShowEastPage(PhilosophersGazePage.HUISHI_INTRO);

    private Task ShowYangzhuViewpoints() => ShowEastPage(PhilosophersGazePage.YANGZHU_INTRO);

    private Task ShowActTwoDeclineConfirmation()
    {
        if (IsActiveEventRoom()
            && GetAvailableContinuationOptions() != PhilosophersGazeContinuationOption.None)
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.ActTwoDeclineConfirm),
                [Option(ConfirmActTwoDecline, PhilosophersGazePage.ActTwoDeclineConfirm, PhilosophersGazeOption.Confirm)]);
        }

        return Task.CompletedTask;
    }

    private async Task ShowEastPage(PhilosophersGazePage page)
    {
        if (!IsActiveEventRoom() || !CanShowEastPage(page))
        {
            Log.Info($"[STS2Philosophers] PhilosophersGaze ignored a stale or ineligible East page {page}.");
            return;
        }

        List<EventOption> options = [];
        foreach (PhilosophersGazeOption option in PhilosophersGazeFlowPolicy.GetOptions(page))
        {
            options.Add(Option(() => AdvanceEastPage(page, option), page, option));
        }

        _currentPage = page;
        SetFlowInProgress(true, page);
        SetEventState(PageDescription(page), options);
        await PersistCurrentEventAsync();
    }

    private async Task AdvanceEastPage(PhilosophersGazePage page, PhilosophersGazeOption option)
    {
        // A rendered button survives navigation and room exit. The captured
        // page, event room, and completion state must all still match before a
        // click is allowed to write shared progress.
        if (!IsActiveEventRoom() || _currentPage != page)
        {
            Log.Info($"[STS2Philosophers] PhilosophersGaze ignored a stale callback {page}.{option}.");
            return;
        }

        if (!PhilosophersGazeFlowPolicy.TryGetTransition(page, option, out PhilosophersGazeTransition transition))
        {
            Log.Info($"[STS2Philosophers] PhilosophersGaze ignored an unknown transition {page}.{option}.");
            return;
        }

        if (transition.IsNavigation)
        {
            await ShowEastPage(transition.Destination);
            return;
        }

        await ApplyFinishEffect(page, transition);
    }

    private async Task ApplyFinishEffect(PhilosophersGazePage page, PhilosophersGazeTransition transition)
    {
        switch (transition.Effect)
        {
            case PhilosophersGazeEffect.ObtainKongziMuduo:
                await ObtainActOneRelic<KongziMuduo>(transition.Destination, ThinkerProposalCatalog.KongziLi);
                break;
            case PhilosophersGazeEffect.ObtainKongziQingYuPei:
                await ObtainActOneRelic<KongziQingYuPei>(transition.Destination, ThinkerProposalCatalog.KongziRen);
                break;
            case PhilosophersGazeEffect.ObtainMoziMoSeZhuJian:
                await ObtainActOneRelic<MoziMoSeZhuJian>(transition.Destination, ThinkerProposalCatalog.MoziJianAi);
                break;
            case PhilosophersGazeEffect.ObtainMoziShouChengTu:
                await ObtainActOneRelic<MoziShouChengTu>(transition.Destination, ThinkerProposalCatalog.MoziFeiGong);
                break;
            case PhilosophersGazeEffect.ObtainLaoziWuWeiShuJian:
                await ObtainActOneRelic<LaoziWuWeiShuJian>(transition.Destination, ThinkerProposalCatalog.LaoziWuWei);
                break;
            case PhilosophersGazeEffect.ObtainLaoziShuiYu:
                await ObtainActOneRelic<LaoziShuiYu>(transition.Destination, ThinkerProposalCatalog.LaoziRuoShui);
                break;
            case PhilosophersGazeEffect.ObtainPlatoCivicSealB:
                await ObtainWesternEntry("PLATO", "FREEDOM_AND_INSTITUTIONS",
                    "CHECKS_AND_JUSTICE", PhilosophersGazePage.PLATO_B);
                break;
            case PhilosophersGazeEffect.ObtainPlatoCivicSealC:
                await ObtainWesternEntry("PLATO", "FREEDOM_AND_INSTITUTIONS",
                    "SYMPATHY_AND_PUBLIC_ACTION", PhilosophersGazePage.PLATO_C);
                break;
            case PhilosophersGazeEffect.ReplaceWithMengziXiongZhang:
                await AcceptMengziXiongZhang();
                break;
            case PhilosophersGazeEffect.ReplaceWithXunziShengMo:
                await AcceptXunziShengMo();
                break;
            case PhilosophersGazeEffect.ReplaceWithQinGuliShouChengXie:
                await AcceptQinGuliShouChengXie();
                break;
            case PhilosophersGazeEffect.ReplaceWithHuishiLiWuChou:
                await AcceptHuishiLiWuChou();
                break;
            case PhilosophersGazeEffect.ReplaceWithZhuangziDaHu:
                await AcceptZhuangziDaHu();
                break;
            case PhilosophersGazeEffect.ReplaceWithYangzhuQuanShengBi:
                await AcceptYangzhuQuanShengBi();
                break;
            case PhilosophersGazeEffect.RecordContinuationDecline:
                await DeclineForPage(page);
                break;
            default:
                await FinishActOneWithoutRelic(transition.Destination);
                break;
        }
    }

    private Task DeclineForPage(PhilosophersGazePage page) => page switch
    {
        PhilosophersGazePage.MENGZI_RESULT_WHO or PhilosophersGazePage.MENGZI_RESULT_FISH => DeclineMengzi(),
        PhilosophersGazePage.XUNZI_RESULT_EDGE or PhilosophersGazePage.XUNZI_RESULT_LINE => DeclineXunzi(),
        PhilosophersGazePage.QIN_RESULT_PUSH or PhilosophersGazePage.QIN_RESULT_MAP => DeclineQinGuli(),
        PhilosophersGazePage.HUISHI_RESULT_HAND => DeclineHuishi(),
        PhilosophersGazePage.ZHUANGZI_RESULT_WALK => DeclineZhuangzi(),
        PhilosophersGazePage.YANGZHU_RESULT_COIL => DeclineYangzhu(),
        PhilosophersGazePage.ActTwoDeclineConfirm => ConfirmActTwoDecline(),
        _ => FinishActOneWithoutRelic(PhilosophersGazePage.Decline),
    };

    private bool CanShowEastPage(PhilosophersGazePage page) => page switch
    {
        PhilosophersGazePage.KONGZI => IsActOneCandidate(PhilosophersGazeActOneCandidatePolicy.Kongzi),
        PhilosophersGazePage.PLATO_A => IsActOneCandidate("PLATO"),
        PhilosophersGazePage.MOZI => IsActOneCandidate(PhilosophersGazeActOneCandidatePolicy.Mozi),
        PhilosophersGazePage.LAOZI => IsActOneCandidate(PhilosophersGazeActOneCandidatePolicy.Laozi),
        PhilosophersGazePage.MENGZI_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.MengziXiongZhang),
        PhilosophersGazePage.XUNZI_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.XunziShengMo),
        PhilosophersGazePage.QIN_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.QinGuliShouChengXie),
        PhilosophersGazePage.HUISHI_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.HuishiLiWuChou),
        PhilosophersGazePage.ZHUANGZI_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.ZhuangziDaHu),
        PhilosophersGazePage.YANGZHU_INTRO => CanChooseContinuation(
            PhilosophersGazeContinuationOption.YangzhuQuanShengBi),
        _ => true,
    };

    private IReadOnlyList<EventOption> BuildEastOptions(PhilosophersGazePage page)
    {
        List<EventOption> options = [];
        foreach (PhilosophersGazeOption option in PhilosophersGazeFlowPolicy.GetOptions(page))
        {
            options.Add(Option(() => AdvanceEastPage(page, option), page, option));
        }

        return options;
    }

    private void SetFlowInProgress(bool value, PhilosophersGazePage? page = null)
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared is not null)
        {
            shared.PhilosophersGazeFlowInProgress = value;
            shared.PhilosophersGazeFlowPage = value && page is { } current
                ? current.ToString()
                : null;
        }
    }

    private bool TryGetSavedFlowPage(Player owner, out PhilosophersGazePage page)
    {
        page = default;
        return owner.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.PhilosophersGazeFlowInProgress == true
            && !string.IsNullOrWhiteSpace(shared.PhilosophersGazeFlowPage)
            && Enum.TryParse(shared.PhilosophersGazeFlowPage, out page)
            && IsPageValidForCurrentAct(page)
            && PhilosophersGazeFlowPolicy.GetOptions(page).Length > 0;
    }

    private void FinishEvent(MegaCrit.Sts2.Core.Localization.LocString description)
    {
        SetFlowInProgress(false);
        SetEventFinished(description);
    }

    private async Task PersistCurrentEventAsync()
    {
        if (Owner?.RunState is not RunState run
            || run.CurrentRoom is not EventRoom room
            || !ReferenceEquals(room.LocalMutableEvent, this)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared is null)
        {
            return;
        }

        if (!await RouteEventMapBoundary.PersistEventAsync(run, shared, room))
        {
            Log.Error("[STS2Philosophers] PhilosophersGaze could not persist the in-progress East page.");
        }
    }

    private Task AcceptKongziMuduo() => ObtainActOneRelic<KongziMuduo>(
        PhilosophersGazePage.KongziMuduo,
        ThinkerProposalCatalog.KongziLi);

    private Task AcceptKongziQingYuPei() => ObtainActOneRelic<KongziQingYuPei>(
        PhilosophersGazePage.KongziQingYuPei,
        ThinkerProposalCatalog.KongziRen);

    private Task AcceptMoziMoSeZhuJian() => ObtainActOneRelic<MoziMoSeZhuJian>(
        PhilosophersGazePage.MoziMoSeZhuJian,
        ThinkerProposalCatalog.MoziJianAi);

    private Task AcceptMoziShouChengTu() => ObtainActOneRelic<MoziShouChengTu>(
        PhilosophersGazePage.MoziShouChengTu,
        ThinkerProposalCatalog.MoziFeiGong);

    private Task AcceptLaoziWuWeiShuJian() => ObtainActOneRelic<LaoziWuWeiShuJian>(
        PhilosophersGazePage.LaoziWuWeiShuJian,
        ThinkerProposalCatalog.LaoziWuWei);

    private Task AcceptLaoziShuiYu() => ObtainActOneRelic<LaoziShuiYu>(
        PhilosophersGazePage.LaoziShuiYu,
        ThinkerProposalCatalog.LaoziRuoShui);

    private Task DeclineKongzi() => FinishActOneWithoutRelic(PhilosophersGazePage.KongziDecline);

    private Task DeclineMozi() => FinishActOneWithoutRelic(PhilosophersGazePage.MOZI_DECLINE);

    private Task DeclineLaozi() => FinishActOneWithoutRelic(PhilosophersGazePage.LAOZI_DECLINE);

    private Task ConfirmActOneDecline() => FinishActOneWithoutRelic(PhilosophersGazePage.Decline);

    private Task AcceptMengziXiongZhang() => ReplaceWithMengziXiongZhang();

    private Task AcceptXunziShengMo() => ReplaceWithXunziShengMo();

    private Task AcceptQinGuliShouChengXie() => ReplaceContinuationRelic<MoziShouChengTu, QinGuliShouChengXie>(
        PhilosophersGazeContinuationOption.QinGuliShouChengXie,
        PhilosophersGazePage.QinGuliShouChengXie,
        "City Defense Diagram",
        "City Defense Machinery");

    private Task AcceptHuishiLiWuChou() => ReplaceContinuationRelic<MoziMoSeZhuJian, HuishiLiWuChou>(
        PhilosophersGazeContinuationOption.HuishiLiWuChou,
        PhilosophersGazePage.HuishiLiWuChou,
        "Ink Bamboo Slips",
        "Enumeration Counters");

    private Task AcceptZhuangziDaHu() => ReplaceContinuationRelic<LaoziWuWeiShuJian, ZhuangziDaHu>(
        PhilosophersGazeContinuationOption.ZhuangziDaHu,
        PhilosophersGazePage.ZhuangziDaHu,
        "Wu Wei Slips",
        "Great Gourd");

    private Task AcceptYangzhuQuanShengBi() => ReplaceContinuationRelic<LaoziShuiYu, YangzhuQuanShengBi>(
        PhilosophersGazeContinuationOption.YangzhuQuanShengBi,
        PhilosophersGazePage.YangzhuQuanShengBi,
        "Water Jade",
        "Whole Life Bi");

    private Task DeclineMengzi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        PhilosophersGazePage.MengziDecline);

    private Task DeclineXunzi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.XunziShengMo,
        PhilosophersGazePage.XunziDecline);

    private Task DeclineQinGuli() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.QinGuliShouChengXie,
        PhilosophersGazePage.QinGuliDecline);

    private Task DeclineHuishi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.HuishiLiWuChou,
        PhilosophersGazePage.HuishiDecline);

    private Task DeclineZhuangzi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.ZhuangziDaHu,
        PhilosophersGazePage.ZhuangziDecline);

    private Task DeclineYangzhu() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.YangzhuQuanShengBi,
        PhilosophersGazePage.YangzhuDecline);

    private Task ConfirmActTwoDecline() => ResolveContinuationDecline(
        GetAvailableContinuationOptions(),
        PhilosophersGazePage.ContinuationDecline);

    private async Task ObtainActOneRelic<TRelic>(
        PhilosophersGazePage resultPage,
        string proposalId)
        where TRelic : RelicModel
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            ThinkerProposal proposal = ThinkerProposalCatalog.Get(proposalId);
            Player? owner = Owner;
            if (owner is null || !CanGrantActOneRelic(proposal.ThinkerId))
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible act one relic callback.");
                return;
            }

            await RelicCmd.Obtain<TRelic>(owner);
            if (owner.GetRelicById(ModelDb.GetId<TRelic>()) is null)
            {
                Log.Error("[STS2Philosophers] PhilosophersGaze did not finish because the act one relic was not obtained.");
                return;
            }

            if (owner.RunState is RunState runState)
            {
                PhilosophyRunStateService.RecordCurrentDoctrine(
                    runState,
                    proposal);
            }

            FinishEvent(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ReplaceWithMengziXiongZhang()
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            KongziQingYuPei? original = owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei;
            if (owner is null
                || original is null
                || !CanChooseContinuation(PhilosophersGazeContinuationOption.MengziXiongZhang))
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible Bear Paw callback.");
                return;
            }

            int inheritedVirtue = PhilosophersGazeReplacementPolicy.CaptureInheritedVirtue(original.Virtue);
            ContinuationResolutionSnapshot snapshot = RecordContinuation(owner);
            MengziXiongZhang replacement = (MengziXiongZhang)ModelDb.Relic<MengziXiongZhang>().ToMutable();
            replacement.SetInheritedVirtue(inheritedVirtue);

            try
            {
                await RelicCmd.Replace(original, replacement);
            }
            catch (Exception exception)
            {
                RestoreContinuation(owner, snapshot);
                Log.Error($"[STS2Philosophers] PhilosophersGaze failed to replace Green Jade Pendant with Bear Paw: {exception}");
                return;
            }

            MengziXiongZhang? obtained = owner.GetRelicById(ModelDb.GetId<MengziXiongZhang>()) as MengziXiongZhang;
            if (!PhilosophersGazeReplacementPolicy.IsMengziReplacementVerified(
                    owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null,
                    obtained is not null,
                    inheritedVirtue,
                    obtained?.InheritedVirtue ?? -1))
            {
                RestoreContinuation(owner, snapshot);
                Log.Error("[STS2Philosophers] PhilosophersGaze did not finish because the Bear Paw replacement could not be verified.");
                return;
            }

            FinishEvent(PageDescription(PhilosophersGazePage.MengziXiongZhang));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ReplaceWithXunziShengMo()
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            KongziMuduo? original = owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo;
            if (owner is null
                || original is null
                || !CanChooseContinuation(PhilosophersGazeContinuationOption.XunziShengMo))
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible Ink Line callback.");
                return;
            }

            ContinuationResolutionSnapshot snapshot = RecordContinuation(owner);
            XunziShengMo replacement = (XunziShengMo)ModelDb.Relic<XunziShengMo>().ToMutable();
            try
            {
                await RelicCmd.Replace(original, replacement);
            }
            catch (Exception exception)
            {
                RestoreContinuation(owner, snapshot);
                Log.Error($"[STS2Philosophers] PhilosophersGaze failed to replace Muduo with Ink Line: {exception}");
                return;
            }

            if (!PhilosophersGazeReplacementPolicy.IsXunziReplacementVerified(
                    owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null,
                    owner.GetRelicById(ModelDb.GetId<XunziShengMo>()) is not null))
            {
                RestoreContinuation(owner, snapshot);
                Log.Error("[STS2Philosophers] PhilosophersGaze did not finish because the Ink Line replacement could not be verified.");
                return;
            }

            FinishEvent(PageDescription(PhilosophersGazePage.XunziShengMo));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ReplaceContinuationRelic<TOriginal, TReplacement>(
        PhilosophersGazeContinuationOption choice,
        PhilosophersGazePage resultPage,
        string originalName,
        string replacementName)
        where TOriginal : RelicModel
        where TReplacement : RelicModel
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            TOriginal? original = owner?.GetRelicById(ModelDb.GetId<TOriginal>()) as TOriginal;
            if (owner is null
                || original is null
                || !CanChooseContinuation(choice))
            {
                Log.Info($"[STS2Philosophers] PhilosophersGaze rejected an ineligible {replacementName} callback.");
                return;
            }

            ContinuationResolutionSnapshot snapshot = RecordContinuation(owner);
            TReplacement replacement = (TReplacement)ModelDb.Relic<TReplacement>().ToMutable();
            try
            {
                await RelicCmd.Replace(original, replacement);
            }
            catch (Exception exception)
            {
                RestoreContinuation(owner, snapshot);
                Log.Error($"[STS2Philosophers] PhilosophersGaze failed to replace {originalName} with {replacementName}: {exception}");
                return;
            }

            if (!PhilosophersGazeReplacementPolicy.IsSimpleReplacementVerified(
                    owner.GetRelicById(ModelDb.GetId<TOriginal>()) is not null,
                    owner.GetRelicById(ModelDb.GetId<TReplacement>()) is not null))
            {
                RestoreContinuation(owner, snapshot);
                Log.Error($"[STS2Philosophers] PhilosophersGaze did not finish because the {replacementName} replacement could not be verified.");
                return;
            }

            FinishEvent(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ResolveContinuationDecline(
        PhilosophersGazeContinuationOption choice,
        PhilosophersGazePage resultPage)
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            PhilosophersGazeContinuationOption availableOptions = GetSelectableContinuationOptions();
            if (owner is null
                || choice == PhilosophersGazeContinuationOption.None
                || (availableOptions & choice) == 0)
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible continuation decline callback.");
                return;
            }

            RecordContinuation(owner);
            FinishEvent(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task FinishActOneWithoutRelic(PhilosophersGazePage resultPage)
    {
        if (!IsActiveEventRoom())
        {
            Log.Info("[STS2Philosophers] PhilosophersGaze ignored a stale resolution callback outside its active event room.");
            return;
        }

        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            if (!IsActOne())
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible act one decline callback.");
                return;
            }

            FinishEvent(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private bool IsActOne()
    {
        return !IsFinished && Owner is { } owner && GetCurrentActIndex(owner) == 0
            && !PhilosophersGazeSaveRecovery.HasAmbiguousResolution(owner);
    }

    private bool CanGrantActOneRelic(string thinkerId)
    {
        return Owner is { } owner
            && GetCurrentActIndex(owner) == 0
            && IsActOneCandidate(thinkerId)
            && PhilosophersGazeRelicGrantPolicy.CanGrant(GetOwnership(owner));
    }

    private bool IsActOneCandidate(string thinkerId)
    {
        return IsActOne()
            && PhilosophersGazeActOneCandidatePolicy.Contains(
                GetActOneCandidates(),
                thinkerId);
    }

    private GeneratedCandidates GetActOneCandidates()
    {
        if (Owner?.RunState is RunState runState)
        {
            return PhilosophyRunStateService.GetOrGenerateActOneCandidates(runState);
        }

        PhilosophyRunState fallbackState = new();
        return PhilosophersGazeActOneCandidatePolicy.GetOrGenerate(
            fallbackState,
            randomValue: 0);
    }

    private bool CanChooseContinuation(PhilosophersGazeContinuationOption choice)
    {
        Player? owner = Owner;
        if (owner is null || IsFinished)
        {
            return false;
        }

        return GetCurrentActIndex(owner) == 1
            && PhilosophersGazeContinuationPolicy.CanGrant(
                choice,
                GetOwnership(owner),
                HasContinuationBeenRecorded(owner));
    }

    private bool IsPageValidForCurrentAct(PhilosophersGazePage page)
    {
        int actIndex = GetCurrentActIndex(Owner);
        return actIndex switch
        {
            0 => page is PhilosophersGazePage.KONGZI
                or PhilosophersGazePage.MOZI
                or PhilosophersGazePage.LAOZI
                or PhilosophersGazePage.PLATO_A
                or PhilosophersGazePage.PLATO_B
                or PhilosophersGazePage.PLATO_C
                or PhilosophersGazePage.PLATO_D
                or PhilosophersGazePage.MOZI_JIANAI
                or PhilosophersGazePage.MOZI_FEIGONG
                or PhilosophersGazePage.LAOZI_STILL
                or PhilosophersGazePage.LAOZI_LOW
                or PhilosophersGazePage.ActOneDeclineConfirm,
            1 => page is PhilosophersGazePage.Continuation
                or PhilosophersGazePage.XUNZI_INTRO
                or PhilosophersGazePage.MENGZI_INTRO
                or PhilosophersGazePage.HUISHI_INTRO
                or PhilosophersGazePage.QIN_INTRO
                or PhilosophersGazePage.ZHUANGZI_INTRO
                or PhilosophersGazePage.YANGZHU_INTRO
                or PhilosophersGazePage.XUNZI_RESULT_EDGE
                or PhilosophersGazePage.XUNZI_RESULT_LINE
                or PhilosophersGazePage.MENGZI_RESULT_WHO
                or PhilosophersGazePage.MENGZI_RESULT_FISH
                or PhilosophersGazePage.HUISHI_RESULT_BAMBOO
                or PhilosophersGazePage.HUISHI_RESULT_EAR
                or PhilosophersGazePage.QIN_RESULT_PUSH
                or PhilosophersGazePage.QIN_RESULT_MAP
                or PhilosophersGazePage.ZHUANGZI_RESULT_LIFT
                or PhilosophersGazePage.ZHUANGZI_RESULT_ROPE
                or PhilosophersGazePage.YANGZHU_RESULT_OUTSIDE
                or PhilosophersGazePage.YANGZHU_RESULT_INSIDE
                or PhilosophersGazePage.HUISHI_RESULT_FIST
                or PhilosophersGazePage.HUISHI_RESULT_HAND
                or PhilosophersGazePage.ZHUANGZI_RESULT_BORROW
                or PhilosophersGazePage.ZHUANGZI_RESULT_WALK
                or PhilosophersGazePage.YANGZHU_RESULT_TAKE
                or PhilosophersGazePage.YANGZHU_RESULT_COIL
                or PhilosophersGazePage.ActTwoDeclineConfirm,
            _ => false,
        };
    }

    private bool IsActiveEventRoom()
    {
        return !IsFinished
            && Owner?.RunState is RunState run
            && run.CurrentRoom is EventRoom room
            && ReferenceEquals(room.LocalMutableEvent, this);
    }

    private PhilosophersGazePage? GetContinuationIntroPage()
    {
        return GetAvailableContinuationOptions() switch
        {
            PhilosophersGazeContinuationOption.MengziXiongZhang => PhilosophersGazePage.MENGZI_INTRO,
            PhilosophersGazeContinuationOption.XunziShengMo => PhilosophersGazePage.XUNZI_INTRO,
            PhilosophersGazeContinuationOption.QinGuliShouChengXie => PhilosophersGazePage.QIN_INTRO,
            PhilosophersGazeContinuationOption.HuishiLiWuChou => PhilosophersGazePage.HUISHI_INTRO,
            PhilosophersGazeContinuationOption.ZhuangziDaHu => PhilosophersGazePage.ZHUANGZI_INTRO,
            PhilosophersGazeContinuationOption.YangzhuQuanShengBi => PhilosophersGazePage.YANGZHU_INTRO,
            _ => null,
        };
    }

    private PhilosophersGazeContinuationOption GetAvailableContinuationOptions()
    {
        Player? owner = Owner;
        if (owner is null || GetCurrentActIndex(owner) != 1)
        {
            return PhilosophersGazeContinuationOption.None;
        }

        return PhilosophersGazeContinuationPolicy.GetAvailableOptions(
            GetOwnership(owner),
            HasContinuationBeenRecorded(owner));
    }

    private PhilosophersGazeContinuationOption GetSelectableContinuationOptions()
    {
        Player? owner = Owner;
        if (owner is null || IsFinished)
        {
            return PhilosophersGazeContinuationOption.None;
        }

        if (GetCurrentActIndex(owner) != 1)
        {
            return PhilosophersGazeContinuationOption.None;
        }

        return PhilosophersGazeContinuationPolicy.GetAvailableOptions(
            GetOwnership(owner),
            HasContinuationBeenRecorded(owner));
    }

    private EventOption Option(
        Func<Task> onChosen,
        PhilosophersGazePage page,
        PhilosophersGazeOption option)
    {
        return new EventOption(
            this,
            onChosen,
            $"{LocalizationPrefix}.{page.ToLocalizationKey()}.options.{option.ToLocalizationKey()}",
            Array.Empty<IHoverTip>());
    }

    private EventOption RouteRelicOption<TRelic>(
        Func<Task> onChosen,
        PhilosophersGazePage page,
        PhilosophersGazeOption option)
        where TRelic : RelicModel
    {
        return new EventOption(
            this,
            onChosen,
            $"{LocalizationPrefix}.{page.ToLocalizationKey()}.options.{option.ToLocalizationKey()}",
            ModelDb.Relic<TRelic>().HoverTips);
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(PhilosophersGazePage page)
    {
        return L10NLookup($"{LocalizationPrefix}.{page.ToLocalizationKey()}.description");
    }

    internal static PhilosophersGazeRelicOwnership GetOwnership(Player? owner)
    {
        return new PhilosophersGazeRelicOwnership(
            owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<MengziXiongZhang>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<XunziShengMo>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<MoziMoSeZhuJian>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<LaoziWuWeiShuJian>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<LaoziShuiYu>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<QinGuliShouChengXie>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<ZhuangziDaHu>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<YangzhuQuanShengBi>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<HuishiLiWuChou>()) is not null,
            owner?.Relics.Any(relic => relic is WesternPracticeRelic) == true);
    }

    internal static bool HasContinuationBeenRecorded(Player? owner)
    {
        return (owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<MoziMoSeZhuJian>()) as MoziMoSeZhuJian)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) as MoziShouChengTu)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<LaoziWuWeiShuJian>()) as LaoziWuWeiShuJian)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<LaoziShuiYu>()) as LaoziShuiYu)
                ?.HasResolvedPhilosophersGazeContinuation == true;
    }

    private static ContinuationResolutionSnapshot RecordContinuation(Player owner)
    {
        KongziMuduo? muduo = owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo;
        KongziQingYuPei? qingYuPei = owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei;
        MoziMoSeZhuJian? moSeZhuJian = owner.GetRelicById(ModelDb.GetId<MoziMoSeZhuJian>()) as MoziMoSeZhuJian;
        MoziShouChengTu? shouChengTu = owner.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) as MoziShouChengTu;
        LaoziWuWeiShuJian? wuWeiShuJian = owner.GetRelicById(ModelDb.GetId<LaoziWuWeiShuJian>()) as LaoziWuWeiShuJian;
        LaoziShuiYu? shuiYu = owner.GetRelicById(ModelDb.GetId<LaoziShuiYu>()) as LaoziShuiYu;
        ContinuationResolutionSnapshot snapshot = new(
            muduo?.HasResolvedPhilosophersGazeContinuation ?? false,
            qingYuPei?.HasResolvedPhilosophersGazeContinuation ?? false,
            moSeZhuJian?.HasResolvedPhilosophersGazeContinuation ?? false,
            shouChengTu?.HasResolvedPhilosophersGazeContinuation ?? false,
            wuWeiShuJian?.HasResolvedPhilosophersGazeContinuation ?? false,
            shuiYu?.HasResolvedPhilosophersGazeContinuation ?? false);
        muduo?.RecordPhilosophersGazeContinuation();
        qingYuPei?.RecordPhilosophersGazeContinuation();
        moSeZhuJian?.RecordPhilosophersGazeContinuation();
        shouChengTu?.RecordPhilosophersGazeContinuation();
        wuWeiShuJian?.RecordPhilosophersGazeContinuation();
        shuiYu?.RecordPhilosophersGazeContinuation();
        return snapshot;
    }

    private static void RestoreContinuation(Player owner, ContinuationResolutionSnapshot snapshot)
    {
        (owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
            ?.RestorePhilosophersGazeContinuation(snapshot.MuduoResolved);
        (owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
            ?.RestorePhilosophersGazeContinuation(snapshot.QingYuPeiResolved);
        (owner.GetRelicById(ModelDb.GetId<MoziMoSeZhuJian>()) as MoziMoSeZhuJian)
            ?.RestorePhilosophersGazeContinuation(snapshot.MoSeZhuJianResolved);
        (owner.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) as MoziShouChengTu)
            ?.RestorePhilosophersGazeContinuation(snapshot.ShouChengTuResolved);
        (owner.GetRelicById(ModelDb.GetId<LaoziWuWeiShuJian>()) as LaoziWuWeiShuJian)
            ?.RestorePhilosophersGazeContinuation(snapshot.WuWeiShuJianResolved);
        (owner.GetRelicById(ModelDb.GetId<LaoziShuiYu>()) as LaoziShuiYu)
            ?.RestorePhilosophersGazeContinuation(snapshot.ShuiYuResolved);
    }

    private static int GetCurrentActIndex(Player? owner) => owner?.RunState.CurrentActIndex ?? -1;

    private bool TryBeginResolution() =>
        (Owner?.RunState is not RunState run || PhilosophyRunStateService.IsAvailable(run))
        && (Owner is not { } owner || !PhilosophersGazeSaveRecovery.HasAmbiguousResolution(owner))
        && _resolutionGate.TryBegin();

    private void EndResolution() => _resolutionGate.End();

    private Task SaveRunAfterResolution() => ConfirmCompletionAsync();

    internal Task ConfirmCompletionAsync()
    {
        if (_completionSaveTask is { IsCompleted: false }) return _completionSaveTask;
        return _completionSaveTask = PersistCompletionAsync();
    }

    private async Task PersistCompletionAsync()
    {
        if (!IsFinished || HasConfirmedCompletion) return;
        if (Owner?.RunState is not RunState run
            || run.CurrentRoom is not EventRoom room || !ReferenceEquals(room.LocalMutableEvent, this)) return;
        // Native EventRoom and EventModel completion flags are independent.
        room.MarkPreFinished();
        if (!RunManager.Instance.ShouldSave)
        {
            HasConfirmedCompletion = true;
            return;
        }
        try
        {
            HasConfirmedCompletion = PhilosophyRunStateService.TryGet(run, out var shared) && shared is not null
                && await RouteEventMapBoundary.PersistEventAsync(run, shared, room);
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PhilosophersGaze completion save failed: {exception}");
        }
        if (!HasConfirmedCompletion)
            Log.Error("[STS2Philosophers] PhilosophersGaze completion remains unconfirmed; Proceed retries saving without granting another reward.");
    }

    private readonly record struct ContinuationResolutionSnapshot(
        bool MuduoResolved,
        bool QingYuPeiResolved,
        bool MoSeZhuJianResolved,
        bool ShouChengTuResolved,
        bool WuWeiShuJianResolved,
        bool ShuiYuResolved);
}
