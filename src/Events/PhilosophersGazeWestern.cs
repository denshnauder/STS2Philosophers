using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed partial class PhilosophersGaze
{
    private GeneratedCandidates WesternInvitations(RunState run) =>
        WesternActOneCandidatePolicy.GetOrGenerate(PhilosophyRunStateService.GetOrCreate(run), run.Rng.Seed);

    private EventOption WesternOption(Func<Task> action, string key, IEnumerable<IHoverTip>? tips = null) =>
        new(this, action, $"{LocalizationPrefix}.WESTERN.options.{key}", tips ?? Array.Empty<IHoverTip>());

    private Task ShowWesternInvitations()
    {
        if (!IsActOne() || Owner?.RunState is not RunState run) return Task.CompletedTask;
        List<EventOption> options = WesternInvitations(run).CandidateIds
            .Select(id => WesternOption(() => ShowWesternThinker(id), id)).ToList();
        options.Add(WesternOption(ReturnToEastInvitations, "BACK"));
        options.Add(WesternOption(ShowActOneDeclineConfirmation, "DECLINE"));
        SetEventState(L10NLookup($"{LocalizationPrefix}.WESTERN.description"), options);
        return Task.CompletedTask;
    }

    private Task ReturnToEastInvitations()
    {
        if (IsActOne()) SetEventState(PageDescription(PhilosophersGazePage.Initial), GenerateActOneInitialOptions());
        return Task.CompletedTask;
    }

    private Task ShowWesternThinker(string thinkerId)
    {
        if (!IsActOne() || Owner?.RunState is not RunState run
            || !WesternInvitations(run).CandidateIds.Contains(thinkerId)) return Task.CompletedTask;
        List<EventOption> options = [];
        foreach (WesternProblem problem in WesternRouteCatalog.Problems.Where(p => p.EntryThinkerId == thinkerId))
        {
            RelicModel relic = WesternEntryRelic(problem.ProblemId);
            options.Add(WesternOption(() => ObtainWesternEntry(thinkerId, problem.ProblemId),
                problem.ProblemId, relic.HoverTips));
        }
        options.Add(WesternOption(ShowWesternInvitations, "BACK_WESTERN"));
        options.Add(WesternOption(ConfirmActOneDecline, "DECLINE"));
        SetEventState(L10NLookup($"{LocalizationPrefix}.WESTERN_{thinkerId}.description"), options);
        return Task.CompletedTask;
    }

    private async Task ObtainWesternEntry(string thinkerId, string problemId)
    {
        if (!TryBeginResolution()) return;
        try
        {
            if (Owner is not { } owner || owner.RunState is not RunState run) return;
            PhilosophyRunState state = PhilosophyRunStateService.GetOrCreate(run);
            GeneratedCandidates candidates = WesternInvitations(run);
            if (!WesternEntryPolicy.CanChoose(state, candidates, thinkerId, problemId, run.CurrentActIndex,
                IsFinished, !PhilosophersGazeRelicGrantPolicy.CanGrant(GetOwnership(owner)))) return;
            // RelicCmd verifies ownership before the shared doctrine and journey are committed.
            await ObtainWesternRelic(problemId, owner);
            RelicModel relic = WesternEntryRelic(problemId);
            if (owner.GetRelicById(relic.Id) is null) return;
            if (!WesternEntryPolicy.RecordObtained(state, candidates, thinkerId, problemId, relic.Id.Entry)) return;
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
