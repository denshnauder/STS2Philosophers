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
//   * no snapshot (state written by the old flow): the record is ambiguous,
//     because a save between the charge and the lost paid checkpoint can hold
//     the reduced gold while CostPaid is still false. It settles as paid
//     instead of charging a second time; see the legacy branch below.
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

        if (!state.CostAttempted)
        {
            state.CostAttempted = true;
            state.CostGoldBefore = available;
            if (!await persist()) return EventGoldCostOutcome.PersistenceUnknown;
        }
        else if (state.CostGoldBefore < 0m)
        {
            // A record written by the old flow: "attempted" was persisted before
            // the charge, but no gold snapshot exists. The charge itself may or
            // may not have reached a save, and the state cannot tell us which:
            // the old attempt checkpoint saved the pre-charge gold, yet any
            // unrelated save between the charge and the lost paid checkpoint
            // would persist the reduced gold without CostPaid. Taking gold again
            // is irreversible, while settling as paid can at worst grant this
            // one outcome for free, so the unknown legacy record fails open.
            state.CostPaid = true;
            state.CostGoldAfter = available;
            return await persist() ? EventGoldCostOutcome.Settled : EventGoldCostOutcome.PersistenceUnknown;
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
