using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum AnselmProofStage
{
    Invitation, Scope, Objection, Decision,
    ResultDeclined, ResultPublished, ResultRevised, ResultWithdrawn, ResultUnanswered,
}

internal enum AnselmProofAction
{
    None, Accept, Decline, UniqueBeing, UniversalMaximum, QuestionOnly,
    GenericRuleAttacked, DistinctScope, Undecided,
    Publish, Revise, Withdraw, LeaveUnanswered, Close,
}

internal enum AnselmProofScope { None, UniqueBeing, UniversalMaximum, QuestionOnly }
internal enum AnselmProofObjection { None, GenericRuleAttacked, DistinctScope, Undecided }
internal enum AnselmProofDisposition { None, Published, Revised, Withdrawn, Unanswered }

internal sealed class AnselmUnfinishedProofState : IJsonOnDeserialized
{
    public const int QuestionAct = 3;
    // Current production Sextus reserves Question 1 without committing graph edge 012.
    // The later textual inquiry must follow those saved event facts, not invent 012.
    public const string RequiredNodeId = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";

    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string PyrrhoInstanceId { get; set; } = string.Empty;
    public AnselmProofStage Stage { get; set; } = (AnselmProofStage)(-1);
    public AnselmProofAction PendingAction { get; set; } = (AnselmProofAction)(-1);
    public List<AnselmProofAction> ConfirmedActions { get; set; } = null!;
    public AnselmProofScope OriginalScope { get; set; }
    public AnselmProofScope FinalScope { get; set; }
    public AnselmProofObjection Objection { get; set; }
    public AnselmProofDisposition Disposition { get; set; }
    public bool ThirdFixedDeclined { get; set; }
    public bool QuestionReserved { get; set; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static AnselmUnfinishedProofState Create(string instanceId, string pyrrhoInstanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        PyrrhoInstanceId = pyrrhoInstanceId,
        Stage = AnselmProofStage.Invitation,
        PendingAction = AnselmProofAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        PyrrhoInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId)
            || string.IsNullOrWhiteSpace(PyrrhoInstanceId) || !_closed.HasValue
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(OriginalScope) || !Enum.IsDefined(FinalScope)
            || !Enum.IsDefined(Objection)
            || !Enum.IsDefined(Disposition) || ConfirmedActions is null
            || ConfirmedActions.Count > 5) return false;
        AnselmProofStage replay = AnselmProofStage.Invitation;
        AnselmProofScope scope = AnselmProofScope.None;
        AnselmProofScope finalScope = AnselmProofScope.None;
        AnselmProofObjection objection = AnselmProofObjection.None;
        AnselmProofDisposition disposition = AnselmProofDisposition.None;
        bool closed = false;
        bool accepted = false;
        foreach (AnselmProofAction action in ConfirmedActions)
        {
            if (closed || !AnselmUnfinishedProofFlow.Options(replay).Contains(action)) return false;
            if (action == AnselmProofAction.Accept) accepted = true;
            scope = action switch
            {
                AnselmProofAction.UniqueBeing => AnselmProofScope.UniqueBeing,
                AnselmProofAction.UniversalMaximum => AnselmProofScope.UniversalMaximum,
                AnselmProofAction.QuestionOnly => AnselmProofScope.QuestionOnly,
                _ => scope,
            };
            objection = action switch
            {
                AnselmProofAction.GenericRuleAttacked => AnselmProofObjection.GenericRuleAttacked,
                AnselmProofAction.DistinctScope => AnselmProofObjection.DistinctScope,
                AnselmProofAction.Undecided => AnselmProofObjection.Undecided,
                _ => objection,
            };
            disposition = action switch
            {
                AnselmProofAction.Publish => AnselmProofDisposition.Published,
                AnselmProofAction.Revise => AnselmProofDisposition.Revised,
                AnselmProofAction.Withdraw => AnselmProofDisposition.Withdrawn,
                AnselmProofAction.LeaveUnanswered => AnselmProofDisposition.Unanswered,
                _ => disposition,
            };
            finalScope = action switch
            {
                AnselmProofAction.Publish => scope,
                AnselmProofAction.Revise => AnselmUnfinishedProofFlow.Narrow(scope),
                _ => finalScope,
            };
            closed = action == AnselmProofAction.Close;
            replay = AnselmUnfinishedProofFlow.Next(replay, action);
        }
        return Stage == replay && Closed == closed
            && OriginalScope == scope && FinalScope == finalScope
            && Objection == objection && Disposition == disposition
            && ThirdFixedDeclined == accepted && QuestionReserved == accepted
            && (PendingAction == AnselmProofAction.None
                || !Closed && AnselmUnfinishedProofFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class AnselmUnfinishedProofFlow
{
    public static AnselmProofScope Narrow(AnselmProofScope scope) => scope switch
    {
        AnselmProofScope.UniversalMaximum => AnselmProofScope.UniqueBeing,
        AnselmProofScope.UniqueBeing => AnselmProofScope.QuestionOnly,
        AnselmProofScope.QuestionOnly => AnselmProofScope.QuestionOnly,
        _ => AnselmProofScope.None,
    };

    public static IReadOnlyList<AnselmProofAction> Options(AnselmProofStage stage) => stage switch
    {
        AnselmProofStage.Invitation => [AnselmProofAction.Accept, AnselmProofAction.Decline],
        AnselmProofStage.Scope => [AnselmProofAction.UniqueBeing,
            AnselmProofAction.UniversalMaximum, AnselmProofAction.QuestionOnly],
        AnselmProofStage.Objection => [AnselmProofAction.GenericRuleAttacked,
            AnselmProofAction.DistinctScope, AnselmProofAction.Undecided],
        AnselmProofStage.Decision => [AnselmProofAction.Publish,
            AnselmProofAction.Revise, AnselmProofAction.Withdraw,
            AnselmProofAction.LeaveUnanswered],
        >= AnselmProofStage.ResultDeclined and <= AnselmProofStage.ResultUnanswered =>
            [AnselmProofAction.Close],
        _ => [],
    };

    public static AnselmProofStage Next(AnselmProofStage stage, AnselmProofAction action) =>
        (stage, action) switch
        {
            (AnselmProofStage.Invitation, AnselmProofAction.Accept) => AnselmProofStage.Scope,
            (AnselmProofStage.Invitation, AnselmProofAction.Decline) => AnselmProofStage.ResultDeclined,
            (AnselmProofStage.Scope, _) => AnselmProofStage.Objection,
            (AnselmProofStage.Objection, _) => AnselmProofStage.Decision,
            (AnselmProofStage.Decision, AnselmProofAction.Publish) => AnselmProofStage.ResultPublished,
            (AnselmProofStage.Decision, AnselmProofAction.Revise) => AnselmProofStage.ResultRevised,
            (AnselmProofStage.Decision, AnselmProofAction.Withdraw) => AnselmProofStage.ResultWithdrawn,
            (AnselmProofStage.Decision, AnselmProofAction.LeaveUnanswered) => AnselmProofStage.ResultUnanswered,
            _ => stage,
        };

    public static bool IsRouteConsistent(AnselmUnfinishedProofState state,
        WesternJourneyState journey, PyrrhoReportedCrossingState pyrrho) =>
        state.IsUsable() && pyrrho.IsUsable() && pyrrho.Closed
        && state.PyrrhoInstanceId == pyrrho.InstanceId
        && journey.CurrentNodeId == AnselmUnfinishedProofState.RequiredNodeId
        && journey.ResolvedQuestionActs.Contains(1)
        && journey.ResolvedQuestionActs.Contains(2)
        && journey.DeclinedFixedActs.Contains(2)
        && (state.ThirdFixedDeclined
            ? journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.RetainedEnding == (state.Disposition != AnselmProofDisposition.None)
            : journey.LastFixedAct == 2 && !journey.DeclinedFixedActs.Contains(3)
                && !journey.ResolvedQuestionActs.Contains(3) && !journey.RetainedEnding);

    public static bool TryPrepare(AnselmUnfinishedProofState state, AnselmProofAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AnselmProofAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(AnselmUnfinishedProofState state,
        WesternJourneyState journey, PyrrhoReportedCrossingState pyrrho, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey, pyrrho) || state.PendingAction == AnselmProofAction.None)
            return false;
        AnselmProofAction action = state.PendingAction;
        if (action == AnselmProofAction.Accept
            && (!graph.TryDecline(journey, "Fixed", 3)
                || !graph.TryReserveQuestion(journey, 3))) return false;
        if (state.Stage == AnselmProofStage.Decision && !graph.TryRetainEnding(journey)) return false;
        state.Stage = Next(state.Stage, action);
        state.OriginalScope = action switch
        {
            AnselmProofAction.UniqueBeing => AnselmProofScope.UniqueBeing,
            AnselmProofAction.UniversalMaximum => AnselmProofScope.UniversalMaximum,
            AnselmProofAction.QuestionOnly => AnselmProofScope.QuestionOnly,
            _ => state.OriginalScope,
        };
        state.Objection = action switch
        {
            AnselmProofAction.GenericRuleAttacked => AnselmProofObjection.GenericRuleAttacked,
            AnselmProofAction.DistinctScope => AnselmProofObjection.DistinctScope,
            AnselmProofAction.Undecided => AnselmProofObjection.Undecided,
            _ => state.Objection,
        };
        state.Disposition = action switch
        {
            AnselmProofAction.Publish => AnselmProofDisposition.Published,
            AnselmProofAction.Revise => AnselmProofDisposition.Revised,
            AnselmProofAction.Withdraw => AnselmProofDisposition.Withdrawn,
            AnselmProofAction.LeaveUnanswered => AnselmProofDisposition.Unanswered,
            _ => state.Disposition,
        };
        state.FinalScope = action switch
        {
            AnselmProofAction.Publish => state.OriginalScope,
            AnselmProofAction.Revise => Narrow(state.OriginalScope),
            _ => state.FinalScope,
        };
        state.ThirdFixedDeclined |= action == AnselmProofAction.Accept;
        state.QuestionReserved |= action == AnselmProofAction.Accept;
        state.Closed = action == AnselmProofAction.Close;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AnselmProofAction.None;
        return IsRouteConsistent(state, journey, pyrrho);
    }

    public static string StageKey(AnselmProofStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(AnselmProofAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(AnselmUnfinishedProofState state) =>
        state.Stage is AnselmProofStage.Decision or >= AnselmProofStage.ResultPublished
            ? $"{StageKey(state.Stage)}_{UpperSnake(state.OriginalScope.ToString())}_{UpperSnake(state.Objection.ToString())}"
            : state.Stage == AnselmProofStage.Objection
                ? $"OBJECTION_{UpperSnake(state.OriginalScope.ToString())}"
            : StageKey(state.Stage);

    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
