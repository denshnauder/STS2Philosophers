using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PhilosophersGazeSaveRecovery
{
    internal static bool IsResolved(Player owner)
    {
        if (owner.RunState is not RunState run) return false;
        if (run.CurrentActIndex == 1)
            return PhilosophersGaze.HasContinuationBeenRecorded(owner)
                || HasContinuationReplacement(owner);
        if (run.CurrentActIndex != 0) return false;
        if (HasActOneGrantFact(owner)) return true;
        return HasTerminalDecline(owner) && !HasActOneGrantClick(owner);
    }

    internal static bool HasAmbiguousResolution(Player owner) => owner.RunState is RunState { CurrentActIndex: 0 }
        && !HasActOneGrantFact(owner) && HasTerminalDecline(owner) && HasActOneGrantClick(owner);

    private static bool HasActOneGrantFact(Player owner) =>
        !PhilosophersGazeRelicGrantPolicy.CanGrant(PhilosophersGaze.GetOwnership(owner))
        || owner.RunState is RunState run && PhilosophyRunStateService.TryGet(run, out var shared)
            && shared is not null && (shared.CurrentDoctrine is not null || shared.WesternJourney is not null);

    private static bool HasTerminalDecline(Player owner)
    {
        // Native history records a click before asynchronous effects finish.
        // Only these decline handlers finish synchronously before their first await;
        // an overlapping grant could still hold their runtime gate, so that mixed
        // history stays blocked rather than guessing that either callback committed.
        return ChoiceKeys(owner).Any(key => key is
                "PHILOSOPHERS_GAZE.pages.ACT_ONE_DECLINE_CONFIRM.options.CONFIRM.title"
                or "PHILOSOPHERS_GAZE.pages.KONGZI_VIEWPOINTS.options.DECLINE.title"
                or "PHILOSOPHERS_GAZE.pages.MOZI_VIEWPOINTS.options.DECLINE.title"
                or "PHILOSOPHERS_GAZE.pages.LAOZI_VIEWPOINTS.options.DECLINE.title"
                or "PHILOSOPHERS_GAZE.pages.WESTERN.options.DECLINE.title");
    }

    private static bool HasActOneGrantClick(Player owner) => ChoiceKeys(owner).Any(key => key is
        "PHILOSOPHERS_GAZE.pages.KONGZI_VIEWPOINTS.options.MUDUO.title"
        or "PHILOSOPHERS_GAZE.pages.KONGZI_VIEWPOINTS.options.QING_YU_PEI.title"
        or "PHILOSOPHERS_GAZE.pages.MOZI_VIEWPOINTS.options.MO_SE_ZHU_JIAN.title"
        or "PHILOSOPHERS_GAZE.pages.MOZI_VIEWPOINTS.options.SHOU_CHENG_TU.title"
        or "PHILOSOPHERS_GAZE.pages.LAOZI_VIEWPOINTS.options.WU_WEI_SHU_JIAN.title"
        or "PHILOSOPHERS_GAZE.pages.LAOZI_VIEWPOINTS.options.SHUI_YU.title"
        || WesternRouteCatalog.Problems.Any(problem =>
            key == $"PHILOSOPHERS_GAZE.pages.WESTERN.options.{problem.ProblemId}.title"));

    private static IEnumerable<string> ChoiceKeys(Player owner) =>
        owner.RunState is RunState run && run.CurrentMapPointHistoryEntry is { } entry
            ? entry.PlayerStats.Where(stats => stats.PlayerId == owner.NetId)
                .SelectMany(stats => stats.EventChoices).Where(choice => choice.Title.LocTable == "events")
                .Select(choice => choice.Title.LocEntryKey)
            : [];

    internal static void TryRestoreLegacyRoom(RunManager manager, ref AbstractRoom? preFinishedRoom)
    {
        if (preFinishedRoom is not null || !manager.IsInProgress || !manager.ShouldSave
            || manager.NetService?.Type != NetGameType.Singleplayer
            || manager.DebugOnlyGetState() is not { CurrentActIndex: 0, IsGameOver: false } run
            || run.Players.Count != 1 || run.VisitedMapCoords.Count != 1
            || run.MapPointHistory.Count == 0 || run.MapPointHistory[0].Count != 1
            || run.CurrentMapPointHistoryEntry is not { MapPointType: MapPointType.Ancient } entry
            || !entry.Rooms.Any(room => Equals(room.ModelId, ModelDb.GetId<Neow>()))
            || entry.Rooms.Any(room => !Equals(room.ModelId, ModelDb.GetId<Neow>())
                && !Equals(room.ModelId, ModelDb.GetId<PhilosophersGaze>()))
            || !PhilosophyRunStateService.TryGet(run, out var shared) || shared is null
            || !shared.GeneratedCandidates.TryGetValue(PhilosophersGazeActOneCandidatePolicy.GenerationKey, out var candidates)
            || !PhilosophersGazeActOneCandidatePolicy.IsValid(candidates)) return;

        // Candidates were generated only after a finished Neow's Proceed. This is
        // evidence of completed Neow, not a guess based on unrelated player relics.
        EventRoom room = new(ModelDb.Event<PhilosophersGaze>());
        if (IsResolved(run.Players[0])) room.MarkPreFinished();
        preFinishedRoom = room;
    }

    private static bool HasContinuationReplacement(Player owner) => owner.Relics.Any(relic => relic is
        MengziXiongZhang or XunziShengMo or QinGuliShouChengXie or HuishiLiWuChou or ZhuangziDaHu or YangzhuQuanShengBi);
}
