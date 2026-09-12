namespace STS2Philosophers;

internal static class WesternEntryPolicy
{
    public static string RelicIdFor(string problemId) => problemId switch
    {
        "BEING_AND_CHANGE" => "HERACLITUS_LYRE",
        "KNOWLEDGE_AND_DOUBT" => "SOCRATES_QUESTION_CUP",
        "VIRTUE_AND_HAPPINESS" => "SOCRATES_BALANCE_WEIGHT",
        "FREEDOM_AND_INSTITUTIONS" => "PLATO_CIVIC_SEAL",
        "SELF_AND_OTHER" => "DESCARTES_THINKING_LENS",
        "LANGUAGE_AND_MEANING" => "ARISTOTLE_CATEGORY_TABLET",
        "HISTORY_AND_POWER" => "ROUSSEAU_UNCARVED_STONE",
        _ => string.Empty,
    };
    public static bool CanChoose(PhilosophyRunState state, GeneratedCandidates candidates,
        string thinkerId, string problemId, int actIndex, bool eventFinished, bool hasDoctrineRelic) =>
        actIndex == 0 && !eventFinished && !hasDoctrineRelic
        && state.CurrentDoctrine is null && state.WesternJourney is null
        && WesternActOneCandidatePolicy.CanChooseProblem(candidates, thinkerId, problemId);

    // Only call after verifying the selected relic was actually obtained.
    public static bool RecordObtained(PhilosophyRunState state, GeneratedCandidates candidates,
        string thinkerId, string problemId, string relicId)
    {
        if (!CanChoose(state, candidates, thinkerId, problemId, 0, false, false)
            || string.IsNullOrWhiteSpace(relicId) || relicId != RelicIdFor(problemId)) return false;
        WesternJourneyState journey = WesternRouteGraph.LoadEmbedded().Start($"{thinkerId}__{problemId}__CORE");
        state.RecordCurrentDoctrine(thinkerId, relicId, ["WESTERN", problemId]);
        state.WesternJourney = journey;
        return true;
    }
}
