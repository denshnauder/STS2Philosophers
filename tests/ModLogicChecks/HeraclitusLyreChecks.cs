using System.Text.Json.Nodes;
using System.Text.Json;
using STS2Philosophers;

internal static class HeraclitusLyreChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        static WesternEntryPracticeState Start()
        {
            WesternEntryPracticeState state = new();
            state.BeginCombat(WesternEntryPracticeState.BeingAndChange);
            state.BeginTurn(1);
            return state;
        }
        static WesternEntryPracticeState Reload(WesternEntryPracticeState state) =>
            WesternEntryPracticeStateCodec.Restore(WesternEntryPracticeStateCodec.Encode(state), state.ProblemId);
        static WesternEntryPlayResolution Play(WesternEntryPracticeState state, string id,
            WesternPracticeCardKind kind, bool marked = false) =>
            state.RecordManualPlay(id, marked ? "MARKED" : id, kind, 1m, false, marked, false);

        // Exhaustively compare short hand-play orders to an independent "prior opposite" oracle.
        int paths = 0;
        foreach (WesternPracticeCardKind markedKind in new[] { WesternPracticeCardKind.Attack, WesternPracticeCardKind.Skill })
        for (int count = 0; count <= 6; count++)
        for (int bits = 0; bits < (1 << count); bits++)
        {
            WesternEntryPracticeState state = Start();
            Check(state.ObserveDraw("MARKED", markedKind, false), "The first eligible draw binds one instance.");
            bool oppositeSeen = false;
            for (int index = 0; index < count; index++)
            {
                var kind = (bits & (1 << index)) == 0 ? WesternPracticeCardKind.Attack : WesternPracticeCardKind.Skill;
                Play(state, $"prior{index}", kind);
                oppositeSeen |= kind != markedKind;
                state = Reload(state);
            }
            Check(state.CanRetainCurrent && state.CanReleaseCurrent == oppositeSeen,
                "Readiness must depend on a prior opposite type, equally for Attack and Skill.");
            WesternEntryPlayResolution result = Play(state, "release", markedKind, true);
            Check(result.Counted && result.DrawOne == oppositeSeen && result.ExhaustPlayedCard == oppositeSeen,
                "An unopposed marked play is ordinary; an opposed one exhausts and draws once.");
            Check(!state.CanRetainCurrent && !state.CanReleaseCurrent
                && !state.ObserveDraw("REWARD_DRAW", markedKind, false),
                "Bonus draw cannot recursively claim another first draw.");
            state = Reload(state);
            Check(!Play(state, "release", markedKind, true).Counted
                && !Play(state, "copy", markedKind, true).DrawOne,
                "Reload, duplicate callbacks and repeated effects cannot pay another draw.");
            paths++;
        }

        WesternEntryPracticeState flow = Start();
        Check(!flow.ObserveDraw("POWER", WesternPracticeCardKind.Power, false)
            && !flow.ObserveDraw("EXHAUST", WesternPracticeCardKind.Skill, true),
            "Powers and existing Exhaust cards must not consume the draw opportunity.");
        Play(flow, "earlyAttack", WesternPracticeCardKind.Attack);
        flow.ObserveDraw("MARKED", WesternPracticeCardKind.Skill, false);
        Check(flow.CanReleaseCurrent, "An opposite already played before the eligible draw still counts this turn.");
        Check(!flow.RecordManualPlay("sameModelCopy", "MARKED", WesternPracticeCardKind.Skill,
                1m, false, false, false).DrawOne
            && flow.CurrentCardModelId == "MARKED", "A different instance cannot spend the marker.");
        Check(flow.ResolveRetains(true, false).RetainCurrent
            && !flow.ResolveRetains(true, false).Applied, "Keeping requests one retain per turn, even when ready.");
        flow.CloseTurn();
        Check(!flow.CanReleaseCurrent && !flow.ObserveDraw("LATE", WesternPracticeCardKind.Skill, false),
            "Closed turns do not accept late opportunities or rewards.");
        flow = Reload(flow);
        flow.BeginTurn(2);
        Check(flow.CurrentCardModelId.Length == 0 && flow.FlowPlayedKinds == 0 && !flow.CanReleaseCurrent,
            "A retained card becomes ordinary next turn; opposite history does not carry over.");
        Check(flow.ObserveDraw("MARKED", WesternPracticeCardKind.Attack, false)
            && !flow.BeginTurn(2) && !flow.ObserveDraw("SECOND", WesternPracticeCardKind.Skill, false),
            "A duplicate turn callback cannot reset the draw budget.");
        flow.RecordAutomaticPlay("automaticOpposite", false, false);
        Play(flow, "power", WesternPracticeCardKind.Power);
        Check(!flow.CanReleaseCurrent, "Neither autoplay nor Power can supply an opposing manual play.");
        flow.RecordAutomaticPlay("automaticMarker", true, false);
        Check(flow.CurrentCardModelId.Length == 0 && !flow.ObserveDraw("SECOND", WesternPracticeCardKind.Skill, false),
            "Autoplay consumes the marker without rewarding it or refunding the opportunity.");

        foreach (bool stillInHand in new[] { true, false })
        {
            WesternEntryPracticeState departed = Start();
            departed.ObserveDraw("MARKED", WesternPracticeCardKind.Skill, false);
            if (!stillInHand) departed.ObserveTrackedCardDeparture(true, false);
            departed = Reload(departed);
            Check(departed.ResolveRetains(stillInHand, false).RetainCurrent == stillInHand,
                "Only the actual marker remaining in hand may be retained.");
            Check(!departed.ObserveDraw("RETURNED", WesternPracticeCardKind.Skill, false),
                "Discard/re-entry and draws after retain settlement cannot reclaim the turn.");
            departed.EndCombat();
            departed = Reload(departed);
            Check(!departed.CombatActive && departed.FlowRuleVersion is null && !departed.FlowDrawClaimed,
                "Combat cleanup removes only temporary practice facts.");
        }

        WesternEntryPracticeState legacy = Start();
        legacy.ObserveDraw("MARKED", WesternPracticeCardKind.Attack, false);
        JsonObject oldPayload = JsonNode.Parse(WesternEntryPracticeStateCodec.Encode(legacy))!.AsObject();
        JsonObject oldState = oldPayload["State"]!.AsObject();
        foreach (string field in new[] { "FlowRuleVersion", "FlowDrawClaimed", "FlowCardKind", "FlowPlayedKinds" })
            oldState.Remove(field);
        var restoredOld = WesternEntryPracticeStateCodec.Restore(oldPayload.ToJsonString(), legacy.ProblemId);
        Check(restoredOld.CombatActive && restoredOld.Turn == 1 && restoredOld.CurrentCardModelId.Length == 0
            && !restoredOld.ObserveDraw("REPLAY", WesternPracticeCardKind.Attack, false),
            "An old active save stays usable but cannot fabricate a marker or opposite history.");
        Check(restoredOld.BeginTurn(2) && restoredOld.ObserveDraw("NEW", WesternPracticeCardKind.Attack, false),
            "The next genuine turn resumes the new rule after a legacy load.");
        foreach (string field in new[] { "FlowRuleVersion", "FlowPlayedKinds", "FlowCardKind", "FlowDrawClaimed", "CurrentCardModelId" })
        {
            JsonObject invalid = JsonNode.Parse(WesternEntryPracticeStateCodec.Encode(legacy))!.AsObject();
            invalid["State"]![field] = field switch
            {
                "FlowDrawClaimed" => JsonValue.Create(false),
                "CurrentCardModelId" => null,
                _ => JsonValue.Create(99),
            };
            Check(!WesternEntryPracticeStateCodec.Restore(invalid.ToJsonString(), legacy.ProblemId).CombatActive,
                "Contradictory or future flow records must not grant combat benefits.");
        }
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument relics = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/relics.json"));
            string[] keys = relics.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            Check(keys.Length == keys.Distinct(StringComparer.Ordinal).Count(), "Relic text keys must not shadow each other.");
            foreach (string suffix in new[] { "title", "description", "flavor", "tension.title", "tension.waiting", "tension.held", "tension.ready", "tension.spent" })
                Check(relics.RootElement.TryGetProperty($"HERACLITUS_LYRE.{suffix}", out var text)
                    && !string.IsNullOrWhiteSpace(text.GetString()), "Every visible tension state needs both localizations.");
        }
        Console.WriteLine($"Heraclitus lyre checks passed: {paths} ordered paths, manual/automatic departure, one draw window, legacy and corrupt saves.");
    }
}
