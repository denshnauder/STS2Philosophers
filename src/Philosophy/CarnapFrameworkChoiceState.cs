namespace STS2Philosophers;

internal enum CarnapFramework
{
    None,
    Observation,
    Region,
}

internal enum CarnapFrameworkChoiceStage
{
    Rules,
    Evidence,
    ResultTranslated,
    ResultLimited,
    ResultConvenient,
}

internal enum CarnapFrameworkChoiceAction
{
    None,
    ChooseObservation,
    ChooseRegion,
    BuildTranslation,
    PublishLimited,
    KeepConvenient,
    Leave,
}

internal enum CarnapFrameworkChoiceOutcome
{
    None,
    Translated,
    Limited,
    Convenient,
}

internal sealed class CarnapFrameworkChoiceState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    public const string RequiredQuestionAct = "2";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public CarnapFrameworkChoiceStage Stage { get; set; }
    public CarnapFrameworkChoiceAction PendingAction { get; set; }
    public List<CarnapFrameworkChoiceAction> ConfirmedActions { get; set; } = [];
    public CarnapFramework Framework { get; set; }
    public bool TranslationBuilt { get; set; }
    public bool CrossFrameworkLimitShown { get; set; }
    public CarnapFrameworkChoiceOutcome Outcome { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public bool Closed { get; set; }

    public static CarnapFrameworkChoiceState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = CarnapFrameworkChoiceStage.Rules,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Framework) || !Enum.IsDefined(Outcome)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || (CostAttempted && Outcome == CarnapFrameworkChoiceOutcome.None)
            || (CostPaid && !CostAttempted)
            || (Stage == CarnapFrameworkChoiceStage.Rules) != (Framework == CarnapFramework.None)
            || TranslationBuilt != (Outcome == CarnapFrameworkChoiceOutcome.Translated)
            || CrossFrameworkLimitShown != (Outcome is CarnapFrameworkChoiceOutcome.Limited
                or CarnapFrameworkChoiceOutcome.Convenient)
            || Outcome switch
            {
                CarnapFrameworkChoiceOutcome.None => Stage is not (CarnapFrameworkChoiceStage.Rules
                    or CarnapFrameworkChoiceStage.Evidence),
                CarnapFrameworkChoiceOutcome.Translated =>
                    Stage != CarnapFrameworkChoiceStage.ResultTranslated,
                CarnapFrameworkChoiceOutcome.Limited =>
                    Stage != CarnapFrameworkChoiceStage.ResultLimited,
                CarnapFrameworkChoiceOutcome.Convenient =>
                    Stage != CarnapFrameworkChoiceStage.ResultConvenient,
                _ => true,
            }) return false;

        CarnapFrameworkChoiceStage replay = CarnapFrameworkChoiceStage.Rules;
        bool closed = false;
        foreach (CarnapFrameworkChoiceAction action in ConfirmedActions)
        {
            if (closed || !CarnapFrameworkChoiceFlow.Options(replay).Contains(action)) return false;
            replay = CarnapFrameworkChoiceFlow.Next(replay, action);
            closed = action == CarnapFrameworkChoiceAction.Leave;
        }
        return replay == Stage && closed == Closed
            && (PendingAction == CarnapFrameworkChoiceAction.None
                || !Closed && CarnapFrameworkChoiceFlow.Options(Stage).Contains(PendingAction));
    }

    public decimal Cost => Outcome == CarnapFrameworkChoiceOutcome.Translated ? 15 : 0;
}

internal static class CarnapFrameworkChoiceFlow
{
    public static bool TryPrepare(CarnapFrameworkChoiceState state, CarnapFrameworkChoiceAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != CarnapFrameworkChoiceAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(CarnapFrameworkChoiceState state, WesternJourneyState journey)
    {
        if (!IsRouteConsistent(state, journey)
            || state.PendingAction == CarnapFrameworkChoiceAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        CarnapFrameworkChoiceAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = CarnapFrameworkChoiceAction.None;
        return true;
    }

    public static IReadOnlyList<CarnapFrameworkChoiceAction> Options(CarnapFrameworkChoiceStage stage) =>
        stage switch
        {
            CarnapFrameworkChoiceStage.Rules =>
                [CarnapFrameworkChoiceAction.ChooseObservation, CarnapFrameworkChoiceAction.ChooseRegion],
            CarnapFrameworkChoiceStage.Evidence =>
                [CarnapFrameworkChoiceAction.BuildTranslation,
                    CarnapFrameworkChoiceAction.PublishLimited,
                    CarnapFrameworkChoiceAction.KeepConvenient],
            CarnapFrameworkChoiceStage.ResultTranslated or
            CarnapFrameworkChoiceStage.ResultLimited or
            CarnapFrameworkChoiceStage.ResultConvenient => [CarnapFrameworkChoiceAction.Leave],
            _ => [],
        };

    public static CarnapFrameworkChoiceStage Next(CarnapFrameworkChoiceStage stage,
        CarnapFrameworkChoiceAction action) => (stage, action) switch
        {
            (CarnapFrameworkChoiceStage.Rules, CarnapFrameworkChoiceAction.ChooseObservation)
                or (CarnapFrameworkChoiceStage.Rules, CarnapFrameworkChoiceAction.ChooseRegion) =>
                CarnapFrameworkChoiceStage.Evidence,
            (CarnapFrameworkChoiceStage.Evidence, CarnapFrameworkChoiceAction.BuildTranslation) =>
                CarnapFrameworkChoiceStage.ResultTranslated,
            (CarnapFrameworkChoiceStage.Evidence, CarnapFrameworkChoiceAction.PublishLimited) =>
                CarnapFrameworkChoiceStage.ResultLimited,
            (CarnapFrameworkChoiceStage.Evidence, CarnapFrameworkChoiceAction.KeepConvenient) =>
                CarnapFrameworkChoiceStage.ResultConvenient,
            (CarnapFrameworkChoiceStage.ResultTranslated or CarnapFrameworkChoiceStage.ResultLimited
                or CarnapFrameworkChoiceStage.ResultConvenient, CarnapFrameworkChoiceAction.Leave) => stage,
            _ => throw new InvalidOperationException($"Invalid Carnap transition {stage}/{action}."),
        };

    public static bool IsRouteConsistent(CarnapFrameworkChoiceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != CarnapFrameworkChoiceState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_011")) return false;
        return state.Outcome == CarnapFrameworkChoiceOutcome.None
            || state.ConfirmedActions.Contains(state.Outcome switch
            {
                CarnapFrameworkChoiceOutcome.Translated => CarnapFrameworkChoiceAction.BuildTranslation,
                CarnapFrameworkChoiceOutcome.Limited => CarnapFrameworkChoiceAction.PublishLimited,
                CarnapFrameworkChoiceOutcome.Convenient => CarnapFrameworkChoiceAction.KeepConvenient,
                _ => CarnapFrameworkChoiceAction.None,
            });
    }

    public static string StageKey(CarnapFrameworkChoiceStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(CarnapFrameworkChoiceAction action) => ToUpperSnake(action.ToString());
    public static string DescriptionKey(CarnapFrameworkChoiceState state) => state.Stage switch
    {
        CarnapFrameworkChoiceStage.Rules => "RULES",
        CarnapFrameworkChoiceStage.Evidence => "EVIDENCE",
        CarnapFrameworkChoiceStage.ResultTranslated => "RESULT_TRANSLATED",
        CarnapFrameworkChoiceStage.ResultLimited => "RESULT_LIMITED",
        CarnapFrameworkChoiceStage.ResultConvenient => "RESULT_CONVENIENT",
        _ => "RULES",
    };

    private static void Apply(CarnapFrameworkChoiceState state, CarnapFrameworkChoiceAction action)
    {
        switch (action)
        {
            case CarnapFrameworkChoiceAction.ChooseObservation:
                state.Framework = CarnapFramework.Observation;
                state.Stage = CarnapFrameworkChoiceStage.Evidence;
                break;
            case CarnapFrameworkChoiceAction.ChooseRegion:
                state.Framework = CarnapFramework.Region;
                state.Stage = CarnapFrameworkChoiceStage.Evidence;
                break;
            case CarnapFrameworkChoiceAction.BuildTranslation:
                state.TranslationBuilt = true;
                state.Outcome = CarnapFrameworkChoiceOutcome.Translated;
                state.Stage = CarnapFrameworkChoiceStage.ResultTranslated;
                break;
            case CarnapFrameworkChoiceAction.PublishLimited:
                state.CrossFrameworkLimitShown = true;
                state.Outcome = CarnapFrameworkChoiceOutcome.Limited;
                state.Stage = CarnapFrameworkChoiceStage.ResultLimited;
                break;
            case CarnapFrameworkChoiceAction.KeepConvenient:
                state.CrossFrameworkLimitShown = true;
                state.Outcome = CarnapFrameworkChoiceOutcome.Convenient;
                state.Stage = CarnapFrameworkChoiceStage.ResultConvenient;
                break;
            case CarnapFrameworkChoiceAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
