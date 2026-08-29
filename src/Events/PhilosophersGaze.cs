using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class PhilosophersGaze : EventModel
{
    private const string LocalizationPrefix = "PHILOSOPHERS_GAZE.pages";
    private readonly PhilosophersGazeResolutionGate _resolutionGate = new();

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        GetCurrentActIndex(Owner) == 1
            ? PageDescription(PhilosophersGazePage.Continuation)
            : base.InitialDescription;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return GetCurrentActIndex(Owner) == 1
            ? GenerateContinuationOptions()
            : GenerateActOneInitialOptions();
    }

    private IReadOnlyList<EventOption> GenerateActOneInitialOptions()
    {
        return
        [
            Option(ShowKongziViewpoints, PhilosophersGazePage.Initial, PhilosophersGazeOption.Kongzi),
            Option(ShowMoziViewpoints, PhilosophersGazePage.Initial, PhilosophersGazeOption.Mozi),
            Option(ShowActOneDeclineConfirmation, PhilosophersGazePage.Initial, PhilosophersGazeOption.Decline),
        ];
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

        if (availableOptions != PhilosophersGazeContinuationOption.None)
        {
            options.Add(Option(ShowActTwoDeclineConfirmation, PhilosophersGazePage.Continuation, PhilosophersGazeOption.Decline));
        }

        return options;
    }

    private Task ShowKongziViewpoints()
    {
        if (IsActOne())
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.KongziViewpoints),
                [
                    RouteRelicOption<KongziMuduo>(AcceptKongziMuduo, PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.Muduo),
                    RouteRelicOption<KongziQingYuPei>(AcceptKongziQingYuPei, PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.QingYuPei),
                    Option(DeclineKongzi, PhilosophersGazePage.KongziViewpoints, PhilosophersGazeOption.Decline),
                ]);
        }

        return Task.CompletedTask;
    }

    private Task ShowMoziViewpoints()
    {
        if (IsActOne())
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.MoziViewpoints),
                [
                    RouteRelicOption<MoziMoSeZhuJian>(AcceptMoziMoSeZhuJian, PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.MoSeZhuJian),
                    RouteRelicOption<MoziShouChengTu>(AcceptMoziShouChengTu, PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.ShouChengTu),
                    Option(DeclineMozi, PhilosophersGazePage.MoziViewpoints, PhilosophersGazeOption.Decline),
                ]);
        }

        return Task.CompletedTask;
    }

    private Task ShowActOneDeclineConfirmation()
    {
        if (IsActOne())
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.ActOneDeclineConfirm),
                [Option(ConfirmActOneDecline, PhilosophersGazePage.ActOneDeclineConfirm, PhilosophersGazeOption.Confirm)]);
        }

        return Task.CompletedTask;
    }

    private Task ShowMengziViewpoints()
    {
        if (CanChooseContinuation(PhilosophersGazeContinuationOption.MengziXiongZhang))
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.MengziViewpoints),
                [
                    RouteRelicOption<MengziXiongZhang>(AcceptMengziXiongZhang, PhilosophersGazePage.MengziViewpoints, PhilosophersGazeOption.XiongZhang),
                    Option(DeclineMengzi, PhilosophersGazePage.MengziViewpoints, PhilosophersGazeOption.Decline),
                ]);
        }

        return Task.CompletedTask;
    }

    private Task ShowXunziViewpoints()
    {
        if (CanChooseContinuation(PhilosophersGazeContinuationOption.XunziShengMo))
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.XunziViewpoints),
                [
                    RouteRelicOption<XunziShengMo>(AcceptXunziShengMo, PhilosophersGazePage.XunziViewpoints, PhilosophersGazeOption.ShengMo),
                    Option(DeclineXunzi, PhilosophersGazePage.XunziViewpoints, PhilosophersGazeOption.Decline),
                ]);
        }

        return Task.CompletedTask;
    }

    private Task ShowActTwoDeclineConfirmation()
    {
        if (GetAvailableContinuationOptions() != PhilosophersGazeContinuationOption.None)
        {
            SetEventState(
                PageDescription(PhilosophersGazePage.ActTwoDeclineConfirm),
                [Option(ConfirmActTwoDecline, PhilosophersGazePage.ActTwoDeclineConfirm, PhilosophersGazeOption.Confirm)]);
        }

        return Task.CompletedTask;
    }

    private Task AcceptKongziMuduo() => ObtainActOneRelic<KongziMuduo>(PhilosophersGazePage.KongziMuduo);

    private Task AcceptKongziQingYuPei() => ObtainActOneRelic<KongziQingYuPei>(PhilosophersGazePage.KongziQingYuPei);

    private Task AcceptMoziMoSeZhuJian() => ObtainActOneRelic<MoziMoSeZhuJian>(PhilosophersGazePage.MoziMoSeZhuJian);

    private Task AcceptMoziShouChengTu() => ObtainActOneRelic<MoziShouChengTu>(PhilosophersGazePage.MoziShouChengTu);

    private Task DeclineKongzi() => FinishActOneWithoutRelic(PhilosophersGazePage.KongziDecline);

    private Task DeclineMozi() => FinishActOneWithoutRelic(PhilosophersGazePage.MoziDecline);

    private Task ConfirmActOneDecline() => FinishActOneWithoutRelic(PhilosophersGazePage.Decline);

    private Task AcceptMengziXiongZhang() => ReplaceWithMengziXiongZhang();

    private Task AcceptXunziShengMo() => ReplaceWithXunziShengMo();

    private Task DeclineMengzi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.MengziXiongZhang,
        PhilosophersGazePage.MengziDecline);

    private Task DeclineXunzi() => ResolveContinuationDecline(
        PhilosophersGazeContinuationOption.XunziShengMo,
        PhilosophersGazePage.XunziDecline);

    private Task ConfirmActTwoDecline() => ResolveContinuationDecline(
        GetAvailableContinuationOptions(),
        PhilosophersGazePage.ContinuationDecline);

    private async Task ObtainActOneRelic<TRelic>(PhilosophersGazePage resultPage)
        where TRelic : RelicModel
    {
        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            if (owner is null || !CanGrantActOneRelic())
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

            SetEventFinished(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ReplaceWithMengziXiongZhang()
    {
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

            SetEventFinished(PageDescription(PhilosophersGazePage.MengziXiongZhang));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task ReplaceWithXunziShengMo()
    {
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

            SetEventFinished(PageDescription(PhilosophersGazePage.XunziShengMo));
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
        if (!TryBeginResolution())
        {
            return;
        }

        try
        {
            Player? owner = Owner;
            PhilosophersGazeContinuationOption availableOptions = GetAvailableContinuationOptions();
            if (owner is null
                || choice == PhilosophersGazeContinuationOption.None
                || (availableOptions & choice) == 0)
            {
                Log.Info("[STS2Philosophers] PhilosophersGaze rejected an ineligible continuation decline callback.");
                return;
            }

            RecordContinuation(owner);
            SetEventFinished(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private async Task FinishActOneWithoutRelic(PhilosophersGazePage resultPage)
    {
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

            SetEventFinished(PageDescription(resultPage));
            await SaveRunAfterResolution();
        }
        finally
        {
            EndResolution();
        }
    }

    private bool IsActOne()
    {
        return Owner is { } owner && GetCurrentActIndex(owner) == 0;
    }

    private bool CanGrantActOneRelic()
    {
        return Owner is { } owner
            && GetCurrentActIndex(owner) == 0
            && PhilosophersGazeRelicGrantPolicy.CanGrant(GetOwnership(owner));
    }

    private bool CanChooseContinuation(PhilosophersGazeContinuationOption choice)
    {
        Player? owner = Owner;
        return owner is not null
            && GetCurrentActIndex(owner) == 1
            && PhilosophersGazeContinuationPolicy.CanGrant(
                choice,
                GetOwnership(owner),
                HasContinuationBeenRecorded(owner));
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
            owner?.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) is not null);
    }

    internal static bool HasContinuationBeenRecorded(Player? owner)
    {
        return (owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
                ?.HasResolvedPhilosophersGazeContinuation == true;
    }

    private static ContinuationResolutionSnapshot RecordContinuation(Player owner)
    {
        KongziMuduo? muduo = owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo;
        KongziQingYuPei? qingYuPei = owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei;
        ContinuationResolutionSnapshot snapshot = new(
            muduo?.HasResolvedPhilosophersGazeContinuation ?? false,
            qingYuPei?.HasResolvedPhilosophersGazeContinuation ?? false);
        muduo?.RecordPhilosophersGazeContinuation();
        qingYuPei?.RecordPhilosophersGazeContinuation();
        return snapshot;
    }

    private static void RestoreContinuation(Player owner, ContinuationResolutionSnapshot snapshot)
    {
        (owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
            ?.RestorePhilosophersGazeContinuation(snapshot.MuduoResolved);
        (owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
            ?.RestorePhilosophersGazeContinuation(snapshot.QingYuPeiResolved);
    }

    private static int GetCurrentActIndex(Player? owner) => owner?.RunState.CurrentActIndex ?? -1;

    private bool TryBeginResolution() => _resolutionGate.TryBegin();

    private void EndResolution() => _resolutionGate.End();

    private static async Task SaveRunAfterResolution()
    {
        if (RunManager.Instance.ShouldSave)
        {
            await SaveManager.Instance.SaveRun(null);
        }
    }

    private readonly record struct ContinuationResolutionSnapshot(bool MuduoResolved, bool QingYuPeiResolved);
}
