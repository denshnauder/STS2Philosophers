using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed partial class PhilosophersGaze
{
    private EventOption WesternOption(Func<Task> action, string key, IEnumerable<IHoverTip>? tips = null) =>
        new(this, action, $"{LocalizationPrefix}.WESTERN.options.{key}", tips ?? Array.Empty<IHoverTip>());

    private Task ReturnToUnifiedInvitations()
    {
        if (IsActOne()) SetEventState(PageDescription(PhilosophersGazePage.Initial), GenerateActOneInitialOptions());
        return Task.CompletedTask;
    }

    private Task ShowWesternThinker(string thinkerId)
    {
        if (!IsActOne()
            || !PhilosophersGazeActOneCandidatePolicy.Contains(GetActOneCandidates(), thinkerId)
            || !WesternRouteCatalog.EntryThinkerIds.Contains(thinkerId, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        List<EventOption> options = [];
        foreach (WesternProblem problem in WesternRouteCatalog.Problems.Where(p => p.EntryThinkerId == thinkerId))
        {
            RelicModel relic = WesternEntryRelic(problem.ProblemId);
            string entryNode = $"{thinkerId}__{problem.ProblemId}__CORE";
            WesternRouteDefinition[] routes = WesternRouteTable.Instance.Routes
                .Where(route => route.EntryNodeId == entryNode)
                .ToArray();
            if (routes.Length <= 1)
            {
                options.Add(WesternOption(() => ObtainWesternEntry(thinkerId, problem.ProblemId,
                    routes.FirstOrDefault()?.RouteId), problem.ProblemId, relic.HoverTips));
                continue;
            }
            // Several lines share this entry. The player chooses the default
            // mainline here, before the first act, so a run that never sees an
            // extra question still has a playable line; this spends no fixed
            // event and no question point.
            foreach (WesternRouteDefinition route in routes)
            {
                options.Add(WesternOption(() => ObtainWesternEntry(thinkerId, problem.ProblemId, route.RouteId),
                    $"{problem.ProblemId}.{route.RouteId}", relic.HoverTips));
            }
        }
        options.Add(WesternOption(ReturnToUnifiedInvitations, "BACK_UNIFIED"));
        options.Add(WesternOption(ConfirmActOneDecline, "DECLINE"));
        SetEventState(L10NLookup($"{LocalizationPrefix}.WESTERN_{thinkerId}.description"), options);
        return Task.CompletedTask;
    }

    private async Task ObtainWesternEntry(string thinkerId, string problemId, string? routeId = null)
    {
        if (!TryBeginResolution()) return;
        try
        {
            if (Owner is not { } owner || owner.RunState is not RunState run) return;
            if (!PhilosophyRunStateService.TryGet(run, out var state) || state is null) return;
            GeneratedCandidates candidates = GetActOneCandidates();
            if (!WesternEntryPolicy.CanChoose(state, candidates, thinkerId, problemId, run.CurrentActIndex,
                IsFinished, !PhilosophersGazeRelicGrantPolicy.CanGrant(GetOwnership(owner)))) return;
            // RelicCmd verifies ownership before the shared doctrine and journey are committed.
            await ObtainWesternRelic(problemId, owner);
            RelicModel relic = WesternEntryRelic(problemId);
            if (owner.GetRelicById(relic.Id) is null) return;
            if (!WesternEntryPolicy.RecordObtained(state, candidates, thinkerId, problemId, relic.Id.Entry, routeId)) return;
            SetEventFinished(L10NLookup($"{LocalizationPrefix}.WESTERN_{problemId}.result"));
            await SaveRunAfterResolution();
        }
        finally { EndResolution(); }
    }

    private static Task ObtainWesternRelic(string problemId, Player owner) => problemId switch
    {
        "BEING_AND_CHANGE" => RelicCmd.Obtain<HeraclitusLyre>(owner),
        "KNOWLEDGE_AND_DOUBT" => RelicCmd.Obtain<SocratesQuestionCup>(owner),
        "VIRTUE_AND_HAPPINESS" => RelicCmd.Obtain<SocratesBalanceWeight>(owner),
        "FREEDOM_AND_INSTITUTIONS" => RelicCmd.Obtain<PlatoCivicSeal>(owner),
        "SELF_AND_OTHER" => RelicCmd.Obtain<DescartesThinkingLens>(owner),
        "LANGUAGE_AND_MEANING" => RelicCmd.Obtain<AristotleCategoryTablet>(owner),
        "HISTORY_AND_POWER" => RelicCmd.Obtain<RousseauUncarvedStone>(owner),
        _ => throw new ArgumentOutOfRangeException(nameof(problemId)),
    };

    private static RelicModel WesternEntryRelic(string problemId) => problemId switch
    {
        "BEING_AND_CHANGE" => ModelDb.Relic<HeraclitusLyre>(),
        "KNOWLEDGE_AND_DOUBT" => ModelDb.Relic<SocratesQuestionCup>(),
        "VIRTUE_AND_HAPPINESS" => ModelDb.Relic<SocratesBalanceWeight>(),
        "FREEDOM_AND_INSTITUTIONS" => ModelDb.Relic<PlatoCivicSeal>(),
        "SELF_AND_OTHER" => ModelDb.Relic<DescartesThinkingLens>(),
        "LANGUAGE_AND_MEANING" => ModelDb.Relic<AristotleCategoryTablet>(),
        "HISTORY_AND_POWER" => ModelDb.Relic<RousseauUncarvedStone>(),
        _ => throw new ArgumentOutOfRangeException(nameof(problemId)),
    };
}
