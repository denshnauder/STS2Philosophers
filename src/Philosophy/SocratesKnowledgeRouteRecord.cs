namespace STS2Philosophers;

internal enum SocratesKnowledgeScene { FirstDay, ChangedBridge }
internal enum SocratesKnowledgeStatement
{
    Unstated, SingleCorrectPerformance, DistinguishExplanation,
    CorrectAndResponsiveExplanation, ActionWithoutKnowledge, NoPositiveDefinition,
}
internal enum SocratesKnowledgeChoice
{
    Keep, AddExplanation, Withdraw, LimitScope, RequireCurrentFacts,
    ProposeCandidate, StayOpen, JudgeActionOnly, Leave,
}
internal enum SocratesKnowledgeQuestion
{
    None, ChanceAndReliability, WhatCountsAsExplanation, MaterialsForCandidate,
    KnowledgeChangesWithResults, RelevantFacts, OldAndCurrentKnowledge,
    TacitExperience, ConnectingGroundsAndTruth, ActionAndKnowledge,
}
internal enum SocratesKnowledgeFact
{
    BothGuidesReachedGate, FirstGuideExplainedRoute, SecondGuideDrewLots,
    BridgeChanged, OldExplanationDidNotReachGate, LotsReachedGateAgain,
}

internal sealed record SocratesKnowledgeDecision(
    SocratesKnowledgeScene Scene, SocratesKnowledgeChoice Choice,
    SocratesKnowledgeStatement Before, SocratesKnowledgeStatement After,
    SocratesKnowledgeQuestion Question, IReadOnlyList<SocratesKnowledgeFact> Facts);

// An explicit dialogue history, independent of card practice, rewards, and game models.
// It is not attached to production saves or events until their separate integration stage.
internal sealed class SocratesKnowledgeRouteRecord
{
    public const int Version = 1;
    public const string ThinkerId = "SOCRATES";
    public const string ProblemId = "KNOWLEDGE_AND_DOUBT";
    public const string RouteId = "SOCRATES_KNOWLEDGE_ROUTE";

    private readonly List<SocratesKnowledgeScene> _seen = [];
    private readonly List<SocratesKnowledgeDecision> _history = [];
    public IReadOnlyList<SocratesKnowledgeScene> SeenScenes => _seen.AsReadOnly();
    public IReadOnlyList<SocratesKnowledgeDecision> History => _history.AsReadOnly();
    public SocratesKnowledgeStatement Statement { get; private set; }
    public SocratesKnowledgeQuestion Question { get; private set; }
    public bool HasExplicitStatement => Statement != SocratesKnowledgeStatement.Unstated;
    public bool HasSeenFirstDay => _seen.Contains(SocratesKnowledgeScene.FirstDay);
    public bool ActTwoLateEntry => _seen.Contains(SocratesKnowledgeScene.ChangedBridge)
        && !_history.Any(item => item.Scene == SocratesKnowledgeScene.FirstDay
            && item.Choice != SocratesKnowledgeChoice.Leave);

    public IReadOnlyList<SocratesKnowledgeFact> VisibleFacts =>
        _seen.Contains(SocratesKnowledgeScene.ChangedBridge) ? FactsFor(SocratesKnowledgeScene.ChangedBridge)
        : HasSeenFirstDay ? FactsFor(SocratesKnowledgeScene.FirstDay)
        : Array.AsReadOnly(Array.Empty<SocratesKnowledgeFact>());

    public bool TryShow(SocratesKnowledgeScene scene)
    {
        if (!Enum.IsDefined(scene) || _seen.Contains(scene)
            || (scene == SocratesKnowledgeScene.FirstDay && _seen.Count != 0)) return false;
        _seen.Add(scene);
        return true;
    }

    public IReadOnlyList<SocratesKnowledgeChoice> AvailableChoices(SocratesKnowledgeScene scene)
    {
        if (!CanResolve(scene)) return Array.AsReadOnly(Array.Empty<SocratesKnowledgeChoice>());
        return Array.AsReadOnly(Enum.GetValues<SocratesKnowledgeChoice>()
            .Where(choice => TryTransition(scene, Statement, choice, out _, out _)).ToArray());
    }

    public bool TryResolve(SocratesKnowledgeScene scene, SocratesKnowledgeChoice choice)
    {
        if (!CanResolve(scene) || !TryTransition(scene, Statement, choice, out var next, out var question))
            return false;
        // Leaving is an explicit action, but is neither withdrawing nor accepting a conclusion.
        if (choice == SocratesKnowledgeChoice.Leave) question = Question;
        _history.Add(new(scene, choice, Statement, next, question, FactsFor(scene)));
        Statement = next;
        Question = question;
        return true;
    }

    private bool CanResolve(SocratesKnowledgeScene scene) => Enum.IsDefined(scene)
        && _seen.Count > 0 && _seen[^1] == scene
        && !_history.Any(item => item.Scene == scene);

    private static IReadOnlyList<SocratesKnowledgeFact> FactsFor(SocratesKnowledgeScene scene) =>
        Array.AsReadOnly(scene == SocratesKnowledgeScene.FirstDay
            ? new[] { SocratesKnowledgeFact.BothGuidesReachedGate,
                SocratesKnowledgeFact.FirstGuideExplainedRoute, SocratesKnowledgeFact.SecondGuideDrewLots }
            : Enum.GetValues<SocratesKnowledgeFact>());

    private static bool TryTransition(SocratesKnowledgeScene scene, SocratesKnowledgeStatement before,
        SocratesKnowledgeChoice choice, out SocratesKnowledgeStatement after,
        out SocratesKnowledgeQuestion question)
    {
        after = before;
        question = SocratesKnowledgeQuestion.None;
        if (!Enum.IsDefined(scene) || !Enum.IsDefined(before) || !Enum.IsDefined(choice)) return false;
        if (choice == SocratesKnowledgeChoice.Leave) return true;
        if (scene == SocratesKnowledgeScene.FirstDay)
        {
            if (before != SocratesKnowledgeStatement.Unstated) return false;
            switch (choice)
            {
                case SocratesKnowledgeChoice.Keep:
                    after = SocratesKnowledgeStatement.SingleCorrectPerformance;
                    question = SocratesKnowledgeQuestion.ChanceAndReliability; return true;
                case SocratesKnowledgeChoice.AddExplanation:
                    after = SocratesKnowledgeStatement.DistinguishExplanation;
                    question = SocratesKnowledgeQuestion.WhatCountsAsExplanation; return true;
                case SocratesKnowledgeChoice.Withdraw:
                    after = SocratesKnowledgeStatement.NoPositiveDefinition;
                    question = SocratesKnowledgeQuestion.MaterialsForCandidate; return true;
                default: return false;
            }
        }

        switch (before)
        {
            case SocratesKnowledgeStatement.SingleCorrectPerformance:
                switch (choice)
                {
                    case SocratesKnowledgeChoice.Keep:
                        question = SocratesKnowledgeQuestion.KnowledgeChangesWithResults; return true;
                    case SocratesKnowledgeChoice.RequireCurrentFacts:
                        after = SocratesKnowledgeStatement.CorrectAndResponsiveExplanation;
                        question = SocratesKnowledgeQuestion.RelevantFacts; return true;
                    case SocratesKnowledgeChoice.Withdraw:
                        after = SocratesKnowledgeStatement.NoPositiveDefinition;
                        question = SocratesKnowledgeQuestion.MaterialsForCandidate; return true;
                    default: return false;
                }
            case SocratesKnowledgeStatement.DistinguishExplanation:
                switch (choice)
                {
                    case SocratesKnowledgeChoice.LimitScope:
                        question = SocratesKnowledgeQuestion.OldAndCurrentKnowledge; return true;
                    case SocratesKnowledgeChoice.RequireCurrentFacts:
                        after = SocratesKnowledgeStatement.CorrectAndResponsiveExplanation;
                        question = SocratesKnowledgeQuestion.TacitExperience; return true;
                    case SocratesKnowledgeChoice.Withdraw:
                        after = SocratesKnowledgeStatement.NoPositiveDefinition;
                        question = SocratesKnowledgeQuestion.ConnectingGroundsAndTruth; return true;
                    default: return false;
                }
            case SocratesKnowledgeStatement.Unstated:
            case SocratesKnowledgeStatement.NoPositiveDefinition:
                switch (choice)
                {
                    case SocratesKnowledgeChoice.ProposeCandidate:
                        after = SocratesKnowledgeStatement.CorrectAndResponsiveExplanation;
                        question = SocratesKnowledgeQuestion.TacitExperience; return true;
                    case SocratesKnowledgeChoice.StayOpen:
                        after = SocratesKnowledgeStatement.NoPositiveDefinition;
                        question = SocratesKnowledgeQuestion.MaterialsForCandidate; return true;
                    case SocratesKnowledgeChoice.JudgeActionOnly:
                        after = SocratesKnowledgeStatement.ActionWithoutKnowledge;
                        question = SocratesKnowledgeQuestion.ActionAndKnowledge; return true;
                    default: return false;
                }
            default: return false;
        }
    }
}
