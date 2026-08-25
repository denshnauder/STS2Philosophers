using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class PhilosophersGaze : EventModel
{
    private const string InitialPage = "INITIAL";
    private const string KongziMuduoEndingKey = "PHILOSOPHERS_GAZE.pages.KONGZI_MUDUO.description";
    private const string KongziQingYuPeiEndingKey = "PHILOSOPHERS_GAZE.pages.KONGZI_QING_YU_PEI.description";
    private const string DeclineEndingKey = "PHILOSOPHERS_GAZE.pages.DECLINE.description";

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
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

    private Task Decline()
    {
        SetEventFinished(L10NLookup(DeclineEndingKey));
        return Task.CompletedTask;
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

        if (owner.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null
            || owner.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null)
        {
            Log.Info("[STS2MinimalMod] PhilosophersGaze skipped relic obtain because the player already owns an MVP blessing.");
            return;
        }

        await RelicCmd.Obtain<TRelic>(owner);
    }
}
