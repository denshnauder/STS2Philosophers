using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2MinimalMod;

public sealed class PhilosophersGaze : EventModel
{
    private const string InitialPage = "INITIAL";
    private const string KongziMuduoEndingKey = "PHILOSOPHERS_GAZE.pages.KONGZI_MUDUO.description";
    private const string KongziQingYuPeiEndingKey = "PHILOSOPHERS_GAZE.pages.KONGZI_QING_YU_PEI.description";
    private const string DeclineEndingKey = "PHILOSOPHERS_GAZE.pages.DECLINE.description";
    private const string ContinuationDescriptionKey = "PHILOSOPHERS_GAZE.pages.CONTINUATION.description";
    private const string MengziXiongZhangEndingKey = "PHILOSOPHERS_GAZE.pages.MENGZI_XIONG_ZHANG.description";
    private const string XunziShengMoEndingKey = "PHILOSOPHERS_GAZE.pages.XUNZI_SHENG_MO.description";
    private const string ContinuationDeclineEndingKey = "PHILOSOPHERS_GAZE.pages.CONTINUATION_DECLINE.description";

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription
    {
        get
        {
            PhilosophersGazeRelicOwnership ownership = GetOwnership(Owner);
            return PhilosophersGazeContinuationPolicy.IsContinuationStage(ownership)
                ? L10NLookup(ContinuationDescriptionKey)
                : base.InitialDescription;
        }
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        PhilosophersGazeRelicOwnership ownership = GetOwnership(Owner);
        if (PhilosophersGazeContinuationPolicy.IsContinuationStage(ownership))
        {
            return GenerateContinuationOptions(ownership);
        }

        return
        [
            RelicOption<KongziMuduo>(AcceptKongziMuduo, InitialPage),
            RelicOption<KongziQingYuPei>(AcceptKongziQingYuPei, InitialPage),
            new EventOption(
                this,
                Decline,
                "PHILOSOPHERS_GAZE.pages.INITIAL.options.DECLINE",
                Array.Empty<IHoverTip>()),
        ];
    }

    private IReadOnlyList<EventOption> GenerateContinuationOptions(
        PhilosophersGazeRelicOwnership ownership)
    {
        bool continuationRecorded = HasContinuationBeenRecorded(Owner);
        PhilosophersGazeContinuationOption availableOptions =
            PhilosophersGazeContinuationPolicy.GetAvailableOptions(
                ownership,
                continuationRecorded);
        List<EventOption> options = [];
        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.MengziXiongZhang))
        {
            options.Add(RelicOption<MengziXiongZhang>(AcceptMengziXiongZhang, "CONTINUATION"));
        }

        if (availableOptions.HasFlag(PhilosophersGazeContinuationOption.XunziShengMo))
        {
            options.Add(RelicOption<XunziShengMo>(AcceptXunziShengMo, "CONTINUATION"));
        }

        string declineOptionKey = availableOptions switch
        {
            PhilosophersGazeContinuationOption.MengziXiongZhang =>
                "PHILOSOPHERS_GAZE.pages.CONTINUATION.options.DECLINE_MENGZI_XIONG_ZHANG",
            PhilosophersGazeContinuationOption.XunziShengMo =>
                "PHILOSOPHERS_GAZE.pages.CONTINUATION.options.DECLINE_XUNZI_SHENG_MO",
            _ => "PHILOSOPHERS_GAZE.pages.CONTINUATION.options.DECLINE_BOTH",
        };
        options.Add(new EventOption(
            this,
            DeclineContinuation,
            declineOptionKey,
            Array.Empty<IHoverTip>()));
        return options;
    }

    private async Task AcceptKongziMuduo()
    {
        await ObtainIfNoMvpBlessing<KongziMuduo>();
        SetEventFinished(L10NLookup(KongziMuduoEndingKey));
    }

    private async Task AcceptKongziQingYuPei()
    {
        await ObtainIfNoMvpBlessing<KongziQingYuPei>();
        SetEventFinished(L10NLookup(KongziQingYuPeiEndingKey));
    }

    private async Task AcceptMengziXiongZhang()
    {
        await AcceptContinuationRelic<MengziXiongZhang>(
            PhilosophersGazeContinuationOption.MengziXiongZhang);
        SetEventFinished(L10NLookup(MengziXiongZhangEndingKey));
    }

    private async Task AcceptXunziShengMo()
    {
        await AcceptContinuationRelic<XunziShengMo>(
            PhilosophersGazeContinuationOption.XunziShengMo);
        SetEventFinished(L10NLookup(XunziShengMoEndingKey));
    }

    private Task Decline()
    {
        SetEventFinished(L10NLookup(DeclineEndingKey));
        return Task.CompletedTask;
    }

    private async Task DeclineContinuation()
    {
        if (Owner is not null)
        {
            RecordContinuation(Owner);
            await SaveContinuationResolution();
        }

        SetEventFinished(L10NLookup(ContinuationDeclineEndingKey));
    }

    private async Task ObtainIfNoMvpBlessing<TRelic>()
        where TRelic : RelicModel
    {
        var owner = Owner;
        if (owner is null)
        {
            Log.Error("[STS2MinimalMod] PhilosophersGaze could not obtain a relic because the event has no owner.");
            return;
        }

        PhilosophersGazeRelicOwnership ownership = GetOwnership(owner);
        if (!PhilosophersGazeRelicGrantPolicy.CanGrant(ownership))
        {
            Log.Info("[STS2MinimalMod] PhilosophersGaze skipped relic obtain because the player already owns a prototype relic.");
            return;
        }

        await RelicCmd.Obtain<TRelic>(owner);
    }

    private async Task AcceptContinuationRelic<TRelic>(
        PhilosophersGazeContinuationOption choice)
        where TRelic : RelicModel
    {
        Player? owner = Owner;
        if (owner is null)
        {
            Log.Error("[STS2MinimalMod] PhilosophersGaze continuation could not obtain a relic because the event has no owner.");
            return;
        }

        PhilosophersGazeRelicOwnership ownership = GetOwnership(owner);
        bool continuationRecorded = HasContinuationBeenRecorded(owner);
        if (!PhilosophersGazeContinuationPolicy.CanGrant(
                choice,
                ownership,
                continuationRecorded))
        {
            Log.Info("[STS2MinimalMod] PhilosophersGaze continuation skipped an ineligible relic obtain.");
            return;
        }

        await RelicCmd.Obtain<TRelic>(owner);
        RecordContinuation(owner);
        await SaveContinuationResolution();
    }

    private static PhilosophersGazeRelicOwnership GetOwnership(Player? owner)
    {
        return new PhilosophersGazeRelicOwnership(
            owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<MengziXiongZhang>()) is not null,
            owner?.GetRelicById(ModelDb.GetId<XunziShengMo>()) is not null);
    }

    private static bool HasContinuationBeenRecorded(Player? owner)
    {
        return (owner?.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (owner?.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
                ?.HasResolvedPhilosophersGazeContinuation == true;
    }

    private static void RecordContinuation(Player owner)
    {
        (owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
            ?.RecordPhilosophersGazeContinuation();
        (owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
            ?.RecordPhilosophersGazeContinuation();
    }

    private static async Task SaveContinuationResolution()
    {
        if (RunManager.Instance.ShouldSave)
        {
            await SaveManager.Instance.SaveRun(null);
        }
    }
}
