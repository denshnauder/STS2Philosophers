namespace STS2Philosophers;

// A map-history point reduced to the two facts the cross-act prerequisite
// check needs. Keeping this pure lets the act-spanning rule be tested without
// a live RunState, which is where the Hegel 025 entry previously failed: the
// old check only searched the current act, so a second-act Leibniz event could
// never be found from a third-act question node.
internal readonly record struct CombatHistoryPoint(bool IsPredecessor, bool IsCombat);

internal static class CrossActCombatHistory
{
    public static bool HasCombatAfterPredecessor(
        IReadOnlyList<IReadOnlyList<CombatHistoryPoint>> acts, int currentAct)
    {
        if (currentAct < 0 || currentAct >= acts.Count) return false;
        for (int act = currentAct; act >= 0; act--)
        {
            IReadOnlyList<CombatHistoryPoint> points = acts[act];
            int last = -1;
            for (int i = 0; i < points.Count; i++)
                if (points[i].IsPredecessor) last = i;
            if (last < 0) continue;
            for (int i = last + 1; i < points.Count; i++)
                if (points[i].IsCombat) return true;
            // The predecessor was the last recorded point of its act; any fight
            // in a later act up to the current one is still after it.
            for (int later = act + 1; later <= currentAct; later++)
                for (int i = 0; i < acts[later].Count; i++)
                    if (acts[later][i].IsCombat) return true;
            return false;
        }
        return false;
    }
}
