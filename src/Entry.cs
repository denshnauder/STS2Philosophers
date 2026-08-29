using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Modding;

namespace STS2Philosophers;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    private const string HarmonyId = "localdeveloper.sts2minimalmod.philosophersgaze";
    private static int _harmonyInitialized;

    public static void Initialize()
    {
        // Register the event relics for unlock/inspection bookkeeping without placing them in
        // the normal shared or character reward grab bags.
        ModHelper.AddModelToPool<EventRelicPool, KongziMuduo>();
        ModHelper.AddModelToPool<EventRelicPool, KongziQingYuPei>();
        ModHelper.AddModelToPool<EventRelicPool, MengziXiongZhang>();
        ModHelper.AddModelToPool<EventRelicPool, XunziShengMo>();
        ModHelper.AddModelToPool<EventRelicPool, MoziMoSeZhuJian>();
        ModHelper.AddModelToPool<EventRelicPool, MoziShouChengTu>();
        ModHelper.AddModelToPool<EventRelicPool, LaoziWuWeiShuJian>();
        ModHelper.AddModelToPool<EventRelicPool, LaoziShuiYu>();

        if (Interlocked.Exchange(ref _harmonyInitialized, 1) == 0)
        {
            new Harmony(HarmonyId).PatchAll(typeof(Entry).Assembly);
        }

        Log.Info("[STS2Philosophers] Initialized successfully. Confucian, Mohist, and Daoist relic prototypes are available for singleplayer testing.");
    }
}
