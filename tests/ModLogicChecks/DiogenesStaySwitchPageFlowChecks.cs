using STS2Philosophers;
using System.Text.Json;

internal static class DiogenesStaySwitchPageFlowChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement],
        []);

    public static void Run()
    {
        RestoresOnlyTheUnresolvedOrCommittedRoute();
        EveryIrreversiblePageFocusesItsSafeOptionFirst();
        NavigationAndCancelNeverCommit();
        ConfirmationEmitsOnlyOneRouteIntent();
        LocalizationContainsEveryPageAndOption();
        Console.WriteLine("Diogenes stay/switch page checks passed: safe focus, reversible navigation, inert cancel and single route intent.");
    }

    private static void RestoresOnlyTheUnresolvedOrCommittedRoute()
    {
        ZenoRouteFeatureState unresolved = CreateUnresolved();
        Assert(DiogenesStaySwitchPageFlow.TryRestore(
                unresolved,
                Catalog,
                out DiogenesStaySwitchPageView? initial) &&
               initial!.Page == DiogenesStaySwitchPage.RouteChoice,
            "An unresolved route must restore at the route choice page.");

        ZenoRouteFeatureState stay = Commit(
            ZenoRouteStateService.PrepareStay(unresolved, 0, Catalog).State);
        Assert(DiogenesStaySwitchPageFlow.TryRestore(
                stay,
                Catalog,
                out DiogenesStaySwitchPageView? stayResult) &&
               stayResult!.Page == DiogenesStaySwitchPage.ResultStay,
            "A committed Stay must restore only its completed result.");

        ZenoRouteFeatureState switched = Commit(
            ZenoRouteStateService.PrepareSwitch(
                CreateUnresolved(),
                0,
                CreateMaterial(),
                Catalog).State);
        Assert(DiogenesStaySwitchPageFlow.TryRestore(
                switched,
                Catalog,
                out DiogenesStaySwitchPageView? switchResult) &&
               switchResult!.Page == DiogenesStaySwitchPage.ResultSwitch,
            "A committed Switch must restore its local result, not the Zeno event page.");

        ZenoRouteFeatureState pending = ZenoRouteStateService.PrepareStay(
            CreateUnresolved(),
            0,
            Catalog).State;
        Assert(!DiogenesStaySwitchPageFlow.TryRestore(pending, Catalog, out _),
            "A pending persistence transaction must not be presented as a completed page.");
    }

    private static void EveryIrreversiblePageFocusesItsSafeOptionFirst()
    {
        ZenoRouteState route = CreateUnresolved().Route!;
        AssertFirstOption(
            route,
            DiogenesStaySwitchPage.RouteChoice,
            DiogenesStaySwitchPageOption.ReviewStatement);
        AssertFirstOption(
            route,
            DiogenesStaySwitchPage.ConfirmStay,
            DiogenesStaySwitchPageOption.BackToRouteChoice);
        AssertFirstOption(
            route,
            DiogenesStaySwitchPage.ZenoRelationReview,
            DiogenesStaySwitchPageOption.BackToRouteChoice);
        AssertFirstOption(
            route,
            DiogenesStaySwitchPage.ConfirmSwitch,
            DiogenesStaySwitchPageOption.BackToRouteChoice);
    }

    private static void NavigationAndCancelNeverCommit()
    {
        ZenoRouteState route = CreateUnresolved().Route!;
        DiogenesStaySwitchPageFlow.TryCreateView(
            route,
            DiogenesStaySwitchPage.RouteChoice,
            out DiogenesStaySwitchPageView? routeChoice);

        DiogenesStaySwitchPageAction stay = DiogenesStaySwitchPageFlow.Choose(
            route,
            routeChoice!,
            DiogenesStaySwitchPageOption.Stay);
        Assert(stay == new DiogenesStaySwitchPageAction(
                DiogenesStaySwitchPageActionKind.Navigate,
                DiogenesStaySwitchPage.ConfirmStay),
            "Stay must navigate to confirmation without committing.");

        DiogenesStaySwitchPageAction learn = DiogenesStaySwitchPageFlow.Choose(
            route,
            routeChoice!,
            DiogenesStaySwitchPageOption.LearnAboutZeno);
        Assert(learn.NextPage == DiogenesStaySwitchPage.ZenoRelationReview,
            "Learning about Zeno must open only the local intermediary explanation.");
        DiogenesStaySwitchPageFlow.TryCreateView(
            route,
            learn.NextPage!.Value,
            out DiogenesStaySwitchPageView? relation);
        Assert(DiogenesStaySwitchPageFlow.Choose(
                route,
                relation!,
                DiogenesStaySwitchPageOption.ContinueToSwitch).NextPage ==
               DiogenesStaySwitchPage.ConfirmSwitch,
            "The intermediary explanation may lead only to Switch confirmation.");

        foreach (DiogenesStaySwitchPage page in new[]
                 {
                     DiogenesStaySwitchPage.StatementReview,
                     DiogenesStaySwitchPage.ConfirmStay,
                     DiogenesStaySwitchPage.ZenoRelationReview,
                     DiogenesStaySwitchPage.ConfirmSwitch,
                 })
        {
            DiogenesStaySwitchPageFlow.TryCreateView(route, page, out DiogenesStaySwitchPageView? view);
            Assert(DiogenesStaySwitchPageFlow.Choose(
                    route,
                    view!,
                    DiogenesStaySwitchPageOption.BackToRouteChoice).NextPage ==
                   DiogenesStaySwitchPage.RouteChoice,
                $"{page} must return to the unresolved route choice without side effects.");
        }

        Assert(DiogenesStaySwitchPageFlow.Cancel().Kind ==
               DiogenesStaySwitchPageActionKind.NoChange,
            "Cancel must remain inert when the native event API has no safe cancel callback.");
        Assert(route.Stage == ZenoRouteStage.Unresolved && route.Revision == 0,
            "Page navigation and cancel must not mutate route state.");
    }

    private static void ConfirmationEmitsOnlyOneRouteIntent()
    {
        ZenoRouteState route = CreateUnresolved().Route!;
        DiogenesStaySwitchPageFlow.TryCreateView(
            route,
            DiogenesStaySwitchPage.ConfirmStay,
            out DiogenesStaySwitchPageView? stay);
        DiogenesStaySwitchPageFlow.TryCreateView(
            route,
            DiogenesStaySwitchPage.ConfirmSwitch,
            out DiogenesStaySwitchPageView? switched);

        Assert(DiogenesStaySwitchPageFlow.Choose(
                route,
                stay!,
                DiogenesStaySwitchPageOption.ConfirmStay).Kind ==
               DiogenesStaySwitchPageActionKind.CommitStay,
            "Only ConfirmStay may emit the Stay transaction intent.");
        Assert(DiogenesStaySwitchPageFlow.Choose(
                route,
                switched!,
                DiogenesStaySwitchPageOption.ConfirmSwitch).Kind ==
               DiogenesStaySwitchPageActionKind.CommitSwitch,
            "Only ConfirmSwitch may emit the Switch transaction intent.");
    }

    private static void LocalizationContainsEveryPageAndOption()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            string path = Path.Combine(
                "content",
                "STS2Philosophers",
                "localization",
                language,
                "events.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            AssertText(root, "DIOGENES_STAY_SWITCH.title", path);

            ZenoRouteState unresolved = CreateUnresolved().Route!;
            foreach (DiogenesStaySwitchPage page in Enum.GetValues<DiogenesStaySwitchPage>())
            {
                string pageKey = DiogenesStaySwitchPageFlow.PageKey(page);
                AssertText(root, $"DIOGENES_STAY_SWITCH.pages.{pageKey}.description", path);

                ZenoRouteState route = page switch
                {
                    DiogenesStaySwitchPage.ResultStay =>
                        Commit(ZenoRouteStateService.PrepareStay(CreateUnresolved(), 0, Catalog).State).Route!,
                    DiogenesStaySwitchPage.ResultSwitch =>
                        Commit(ZenoRouteStateService.PrepareSwitch(
                            CreateUnresolved(),
                            0,
                            CreateMaterial(),
                            Catalog).State).Route!,
                    _ => unresolved,
                };
                Assert(DiogenesStaySwitchPageFlow.TryCreateView(route, page, out DiogenesStaySwitchPageView? view),
                    $"Expected canonical page {page} while checking localization.");
                foreach (DiogenesStaySwitchPageOption option in view!.Options)
                {
                    string prefix = $"DIOGENES_STAY_SWITCH.pages.{pageKey}.options.{DiogenesStaySwitchPageFlow.OptionKey(option)}";
                    AssertText(root, $"{prefix}.title", path);
                    AssertText(root, $"{prefix}.description", path);
                }
            }
        }
    }

    private static void AssertFirstOption(
        ZenoRouteState route,
        DiogenesStaySwitchPage page,
        DiogenesStaySwitchPageOption expected)
    {
        Assert(DiogenesStaySwitchPageFlow.TryCreateView(route, page, out DiogenesStaySwitchPageView? view) &&
               view!.Options[0] == expected,
            $"{page} must put {expected} first so native controller focus starts safely.");
    }

    private static void AssertText(JsonElement root, string key, string path)
    {
        Assert(root.TryGetProperty(key, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString()),
            $"Missing Diogenes stay/switch localization key {key} in {path}.");
    }

    internal static ZenoRouteFeatureState CreateUnresolved() =>
        new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260927_DIOGENES_001",
                2,
                ZenoRouteIds.RouteEdge,
                ZenoRouteIds.TerminalNode,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

    internal static ZenoAssentMaterialSnapshot CreateMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [CurrentStatement],
            CurrentStatement,
            null,
            null);

    internal static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected a prepared route operation.");
        return ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog).State;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
