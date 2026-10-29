namespace STS2Philosophers;

// Shared gold-payment protocol for route events whose outcome charges gold.
//
// The old per-event flow persisted "attempted" before charging and "paid"
// after, so a crash between the two writes restored an attempted-but-unpaid
// state that only Block()ed: the event room froze forever with no retry and
// no proof of payment. This protocol keeps a gold snapshot around the single
// charge so a restored state can be classified instead of frozen:
//
//   * gold already at or below (before - cost): the charge reached the save,
//     finish the event without charging again;
//   * gold still at the snapshot: the charge never reached the save, charge
//     exactly once now;
//   * no snapshot (state written by the old flow): documented recovery is one
//     fresh charge, never a freeze and never a silent "already paid".
//
// Zero-cost outcomes never persist an uncertain window at all.
internal interface IEventGoldCostState
{
    decimal Cost { get; }
    bool CostAttempted { get; set; }
    bool CostPaid { get; set; }
    decimal CostGoldBefore { get; set; }
    decimal CostGoldAfter { get; set; }
}

internal enum EventGoldCostOutcome
{
    Settled,
    OwnerUnavailable,
    PersistenceUnknown,
}

internal static class EventGoldCostProtocol
{
    public const decimal NoGoldSnapshot = -1m;

    public static async Task<EventGoldCostOutcome> ApplyAsync(
        IEventGoldCostState state,
        Func<decimal?> currentGold,
        Func<Task> charge,
        Func<Task<bool>> persist)
    {
        if (state.CostPaid) return EventGoldCostOutcome.Settled;

        decimal? gold = currentGold();
        if (state.Cost <= 0m)
        {
            // The states keep the invariant "paid implies attempted"; a zero-cost
            // outcome has no payment window, so both flags settle in one write.
            state.CostAttempted = true;
            state.CostPaid = true;
            state.CostGoldAfter = gold ?? NoGoldSnapshot;
            return await persist() ? EventGoldCostOutcome.Settled : EventGoldCostOutcome.PersistenceUnknown;
        }

        if (gold is not { } available) return EventGoldCostOutcome.OwnerUnavailable;

        if (!state.CostAttempted || state.CostGoldBefore < 0m)
        {
            state.CostAttempted = true;
            state.CostGoldBefore = available;
            if (!await persist()) return EventGoldCostOutcome.PersistenceUnknown;
        }
        else if (available <= ChargedThreshold(state))
        {
            state.CostPaid = true;
            state.CostGoldAfter = available;
            return await persist() ? EventGoldCostOutcome.Settled : EventGoldCostOutcome.PersistenceUnknown;
        }

        await charge();
        state.CostPaid = true;
        state.CostGoldAfter = currentGold() ?? NoGoldSnapshot;
        return await persist() ? EventGoldCostOutcome.Settled : EventGoldCostOutcome.PersistenceUnknown;
    }

    // Gold never goes below zero, so a charge larger than the wallet lands at 0.
    // Comparing against the clamped threshold keeps that case classified as paid
    // instead of charging a restored state a second time.
    private static decimal ChargedThreshold(IEventGoldCostState state)
    {
        decimal threshold = state.CostGoldBefore - state.Cost;
        return threshold < 0m ? 0m : threshold;
    }
}
