namespace STS2Philosophers;

internal static class ZenoAssentBoundaryCreationPolicyChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string EventInstanceId = "ZENO_EVENT_FACTORY_001";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [],
        []);

    public static void Run()
    {
        StableOpeningProducesItsFrozenIdentity();
        PendingAndWrongStagesAreRejected();
        InvalidAndConflictingIdentityAreRejected();
        CacheReturnsExactlyOneInstance();
        Console.WriteLine("Zeno event creation checks passed: confirmed opening only, frozen identity and one mutable instance.");
    }

    private static void StableOpeningProducesItsFrozenIdentity()
    {
        ZenoRouteFeatureState opening = CreateOpening();
        Assert(ZenoAssentBoundaryCreationPolicy.TryCreatePlan(opening, Catalog, out var plan) &&
               plan?.EventInstanceId == EventInstanceId,
            "A confirmed OpeningClaimed route must expose exactly its frozen event identity.");
    }

    private static void PendingAndWrongStagesAreRejected()
    {
        ZenoRouteFeatureState opening = CreateOpening();
        ZenoRouteFeatureState pending = ZenoRouteStateService.PrepareEventEstablished(
            opening,
            opening.Route!.Revision,
            Catalog).State;
        Assert(!ZenoAssentBoundaryCreationPolicy.TryCreatePlan(pending, Catalog, out _),
            "An event-establishment write-ahead record must not construct the event early.");

        ZenoRouteFeatureState active = Commit(pending);
        Assert(!ZenoAssentBoundaryCreationPolicy.TryCreatePlan(active, Catalog, out _),
            "An already active route must not construct a second event instance.");
    }

    private static void InvalidAndConflictingIdentityAreRejected()
    {
        ZenoRouteFeatureState opening = CreateOpening();
        ZenoRouteFeatureState missingIdentity = opening with
        {
            Route = opening.Route! with { EventInstanceId = null },
        };
        Assert(!ZenoAssentBoundaryCreationPolicy.TryCreatePlan(missingIdentity, Catalog, out _),
            "A missing frozen event identity must be rejected as an invalid opening.");

        ZenoRouteValidationCatalog anotherCatalog = new(
            [new KeyValuePair<string, int>(SourceKind, 2)],
            [ActionFact],
            [],
            []);
        Assert(!ZenoAssentBoundaryCreationPolicy.TryCreatePlan(opening, anotherCatalog, out _),
            "A route that does not validate against the bound catalog must be rejected.");
    }

    private static void CacheReturnsExactlyOneInstance()
    {
        ZenoAssentBoundaryCreationCache<object> cache = new();
        ZenoAssentBoundaryCreationPlan plan = new(EventInstanceId);
        int creations = 0;

        Assert(cache.TryGetOrCreate(plan, Create, out object? first) &&
               cache.TryGetOrCreate(plan, Create, out object? second) &&
               ReferenceEquals(first, second) && creations == 1,
            "Repeated callbacks for one frozen identity must return the same instance.");
        Assert(!cache.TryGetOrCreate(
                new ZenoAssentBoundaryCreationPlan("ZENO_EVENT_FACTORY_002"),
                Create,
                out _) && creations == 1,
            "A conflicting identity must neither replace nor construct beside the reserved instance.");

        object Create()
        {
            creations++;
            return new object();
        }
    }

    private static ZenoRouteFeatureState CreateOpening()
    {
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [],
            null,
            null,
            null);
        ZenoRouteFeatureState unresolved = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260925_FACTORY_001",
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
        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(unresolved, 0, material, Catalog).State);
        return Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            waiting.Route!.Revision,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "ACT_THREE_BOSS_001",
            EventInstanceId,
            Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
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
