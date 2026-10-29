using STS2Philosophers;

internal static class EventGoldCostProtocolChecks
{
    private sealed class FakeState : IEventGoldCostState
    {
        public decimal Cost { get; init; }
        public bool CostAttempted { get; set; }
        public bool CostPaid { get; set; }
        public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
        public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    }

    private sealed class Harness
    {
        public decimal? Gold { get; set; } = 10m;
        public int Charges { get; private set; }
        public int Persists { get; private set; }
        public List<bool> PersistResults { get; } = [];
        public List<string> Order { get; } = [];

        public EventGoldCostOutcome Apply(FakeState state) =>
            EventGoldCostProtocol.ApplyAsync(
                    state,
                    () => Gold,
                    () =>
                    {
                        Charges++;
                        Order.Add("charge");
                        if (Gold is { } gold) Gold = gold - state.Cost;
                        return Task.CompletedTask;
                    },
                    () =>
                    {
                        Persists++;
                        Order.Add("persist");
                        return Task.FromResult(
                            PersistResults.Count == 0 || PersistResults[Persists - 1]);
                    })
                .GetAwaiter().GetResult();
    }

    internal static void Run()
    {
        ZeroCostPaysWithoutCharging();
        FirstChargeRecordsSnapshotThenPays();
        AlreadyChargedRestoreDoesNotChargeAgain();
        ClampedChargeRestoreDoesNotChargeAgain();
        UnchargedRestoreChargesOnce();
        LegacyAttemptWithoutSnapshotSettlesWithoutCharging();
        AttemptPersistFailureDoesNotCharge();
        PaidPersistFailureReportsUnknownAfterOneCharge();
        MissingOwnerDoesNotPersist();
        AlreadyPaidIsIdempotent();
    }

    private static void ZeroCostPaysWithoutCharging()
    {
        var state = new FakeState { Cost = 0m };
        var harness = new Harness();
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled, "Zero cost settles.");
        Check(harness.Charges == 0 && harness.Persists == 1,
            "Zero cost persists once and never opens a payment window.");
        Check(state.CostPaid && state.CostAttempted && state.CostGoldAfter == 10m,
            "Zero cost records the paid snapshot while keeping paid-implies-attempted.");
        Check(harness.Gold == 10m, "Zero cost leaves gold untouched.");
    }

    private static void FirstChargeRecordsSnapshotThenPays()
    {
        var state = new FakeState { Cost = 6m };
        var harness = new Harness();
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled, "First charge settles.");
        Check(harness.Persists == 2 && harness.Charges == 1,
            "First charge persists the attempt, charges once, then persists the result.");
        Check(harness.Order.SequenceEqual(["persist", "charge", "persist"]),
            "The attempt checkpoint precedes the single charge.");
        Check(state.CostGoldBefore == 10m && state.CostGoldAfter == 4m && state.CostPaid,
            "The snapshot records pre-charge and post-charge gold.");
    }

    private static void AlreadyChargedRestoreDoesNotChargeAgain()
    {
        var state = new FakeState
        {
            Cost = 6m, CostAttempted = true, CostGoldBefore = 10m,
        };
        var harness = new Harness { Gold = 4m };
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled,
            "A restore whose gold already dropped settles.");
        Check(harness.Charges == 0 && harness.Persists == 1,
            "An already charged restore never charges a second time.");
        Check(state.CostPaid && state.CostGoldAfter == 4m, "The restore records the paid state.");
    }

    private static void ClampedChargeRestoreDoesNotChargeAgain()
    {
        var state = new FakeState
        {
            Cost = 12m, CostAttempted = true, CostGoldBefore = 5m,
        };
        var harness = new Harness { Gold = 0m };
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled,
            "A restore clamped to zero gold settles.");
        Check(harness.Charges == 0 && harness.Persists == 1,
            "A charge larger than the wallet is not repeated after restore.");
    }

    private static void UnchargedRestoreChargesOnce()
    {
        var state = new FakeState
        {
            Cost = 6m, CostAttempted = true, CostGoldBefore = 10m,
        };
        var harness = new Harness { Gold = 10m };
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled,
            "A restore whose gold is unchanged settles.");
        Check(harness.Charges == 1 && harness.Persists == 1,
            "An uncharged restore charges once and persists only the result.");
        Check(harness.Gold == 4m, "The retried charge is applied exactly once.");
    }

    private static void LegacyAttemptWithoutSnapshotSettlesWithoutCharging()
    {
        var state = new FakeState { Cost = 6m, CostAttempted = true };
        var harness = new Harness();
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled,
            "A legacy attempt without a snapshot settles instead of freezing.");
        Check(harness.Charges == 0 && harness.Persists == 1,
            "An ambiguous legacy record never charges a second time.");
        Check(harness.Gold == 10m && state.CostPaid && state.CostGoldAfter == 10m,
            "Legacy recovery leaves gold untouched and records the settled state.");
    }

    private static void AttemptPersistFailureDoesNotCharge()
    {
        var state = new FakeState { Cost = 6m };
        var harness = new Harness();
        harness.PersistResults.Add(false);
        Check(harness.Apply(state) == EventGoldCostOutcome.PersistenceUnknown,
            "A failed attempt checkpoint reports an unknown persistence.");
        Check(harness.Charges == 0 && harness.Gold == 10m,
            "No gold is taken when the attempt checkpoint fails.");
    }

    private static void PaidPersistFailureReportsUnknownAfterOneCharge()
    {
        var state = new FakeState { Cost = 6m };
        var harness = new Harness();
        harness.PersistResults.AddRange([true, false]);
        Check(harness.Apply(state) == EventGoldCostOutcome.PersistenceUnknown,
            "A failed paid checkpoint reports an unknown persistence.");
        Check(harness.Charges == 1 && harness.Gold == 4m,
            "The single charge happened before the failed paid checkpoint.");
    }

    private static void MissingOwnerDoesNotPersist()
    {
        var state = new FakeState { Cost = 6m };
        var harness = new Harness { Gold = null };
        Check(harness.Apply(state) == EventGoldCostOutcome.OwnerUnavailable,
            "A missing owner is reported instead of charged.");
        Check(harness.Charges == 0 && harness.Persists == 0,
            "A missing owner writes no payment checkpoint.");
    }

    private static void AlreadyPaidIsIdempotent()
    {
        var state = new FakeState { Cost = 6m, CostAttempted = true, CostPaid = true };
        var harness = new Harness();
        Check(harness.Apply(state) == EventGoldCostOutcome.Settled, "A paid state settles.");
        Check(harness.Charges == 0 && harness.Persists == 0,
            "A paid state neither charges nor persists again.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
