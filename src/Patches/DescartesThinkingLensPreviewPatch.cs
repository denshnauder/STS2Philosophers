using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace STS2Philosophers;

// Native UpdateVisuals passes its private preview target to damage previews,
// but not to energy-cost hooks. Scope that same target only while rendering.
[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class DescartesThinkingLensPreviewPatch
{
    private static void Prefix(NCard __instance, Creature? ____previewTarget,
        out DescartesThinkingLensTargetContext.PreviewScope? __state)
    {
        __state = __instance.Model is { IsMutable: true } card && ____previewTarget is not null
            ? new DescartesThinkingLensTargetContext.PreviewScope(card, ____previewTarget) : null;
    }
    private static void Finalizer(DescartesThinkingLensTargetContext.PreviewScope? __state) => __state?.Dispose();
}

// Target-specific affordability is checked before enqueueing the native action.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlayTargeting))]
internal static class DescartesThinkingLensAffordabilityPatch
{
    private static void Prefix(CardModel __instance, Creature? target,
        out DescartesThinkingLensTargetContext.PreviewScope __state) =>
        __state = new DescartesThinkingLensTargetContext.PreviewScope(__instance, target);
    private static void Finalizer(DescartesThinkingLensTargetContext.PreviewScope __state) => __state.Dispose();
}
