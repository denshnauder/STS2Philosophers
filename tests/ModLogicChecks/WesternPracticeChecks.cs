using STS2Philosophers;

internal static class WesternPracticeChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static WesternEntryPracticeState Start(string problemId)
        {
            WesternEntryPracticeState state = new();
            state.BeginCombat(problemId);
            Check(state.BeginTurn(1), $"{problemId} must start its first combat turn.");
            return state;
        }

        static WesternEntryPracticeState RoundTrip(WesternEntryPracticeState state) =>
            WesternEntryPracticeStateCodec.Restore(WesternEntryPracticeStateCodec.Encode(state), state.ProblemId);

        static WesternEntryPlayResolution Play(
            WesternEntryPracticeState state,
            string playId,
            string cardId,
            WesternPracticeCardKind kind,
            decimal cost = 1m,
            bool trackedCurrent = false,
            bool trackedPractice = false,
            uint? target = null,
            bool singleTarget = false,
            WesternEntryCostEffect effect = WesternEntryCostEffect.None) =>
            state.RecordManualPlay(playId, cardId, kind, cost, costsX: false, trackedCurrent,
                trackedPractice, target, singleTarget, effect);

        Check(new[]
        {
            WesternEntryPracticeState.BeingAndChange,
            WesternEntryPracticeState.KnowledgeAndDoubt,
            WesternEntryPracticeState.VirtueAndHappiness,
            WesternEntryPracticeState.FreedomAndInstitutions,
            WesternEntryPracticeState.SelfAndOther,
            WesternEntryPracticeState.LanguageAndMeaning,
            WesternEntryPracticeState.HistoryAndPower,
        }.ToHashSet(StringComparer.Ordinal).SetEquals(WesternRouteCatalog.Problems.Select(problem => problem.ProblemId)),
            "Every approved entry problem must have an independent practice state.");

        WesternEntryPracticeState flow = Start(WesternEntryPracticeState.BeingAndChange);
        Check(!flow.ObserveDraw("POWER", WesternPracticeCardKind.Power, false)
              && !flow.ObserveDraw("EXHAUST", WesternPracticeCardKind.Attack, true)
              && flow.ObserveDraw("STRIKE", WesternPracticeCardKind.Attack, false)
              && !flow.ObserveDraw("DEFEND", WesternPracticeCardKind.Skill, false),
            "Flow must mark only the first eligible non-exhaust Attack or Skill drawn each turn.");
        Check(RoundTrip(flow).CurrentCardModelId == "STRIKE",
            "Save/load must preserve the current-card fact without inventing a card instance.");
        WesternEntryPlayResolution flowPlay = Play(flow, "F1", "STRIKE", WesternPracticeCardKind.Attack,
            trackedCurrent: true);
        Check(flowPlay is { Counted: true, DrawOne: true, ExhaustPlayedCard: true }
              && !Play(flow, "F1", "STRIKE", WesternPracticeCardKind.Attack, trackedCurrent: true).Counted,
            "Manually playing the current card must exhaust/draw exactly once despite duplicate callbacks.");
        flow.BeginTurn(2);
        flow.ObserveDraw("DEFEND", WesternPracticeCardKind.Skill, false);
        Check(flow.RecordAutomaticPlay("F2", trackedCurrent: true, trackedPractice: false)
              && flow.CurrentCardModelId.Length == 0,
            "Automatic play must end the current opportunity without claiming the flow reward.");
        flow.ObserveDraw("SECOND_DEFEND", WesternPracticeCardKind.Skill, false);
        WesternEntryRetainResolution flowRetain = flow.ResolveRetains(true, false);
        Check(flowRetain.RetainCurrent && !flow.ResolveRetains(true, false).Applied,
            "Keeping the current card must request retain only once.");
        flow.BeginTurn(3);
        Check(flow.CurrentCardModelId.Length == 0, "A retained current card must not remain current next turn.");

        WesternEntryPracticeState doubt = Start(WesternEntryPracticeState.KnowledgeAndDoubt);
        Check(doubt.TryBeginOpeningChoice()
              && doubt.TryCommitOpeningChoice(["CURSE", "STATUS"])
              && !doubt.TryCommitOpeningChoice([]),
            "The doubt opening must commit zero to two actual selected cards only once.");
        string committedDoubt = WesternEntryPracticeStateCodec.Encode(doubt);
        doubt = WesternEntryPracticeStateCodec.Restore(committedDoubt, doubt.ProblemId);
        Check(doubt.OpeningStage == WesternOpeningStage.Committed
              && doubt.OpeningCardModelIds.SequenceEqual(["CURSE", "STATUS"])
              && doubt.TryCompleteOpeningChoice() && !doubt.TryBeginOpeningChoice(),
            "Reload must preserve the exchange transaction and must never reopen it.");
        WesternEntryPracticeState failedDoubt = Start(WesternEntryPracticeState.KnowledgeAndDoubt);
        failedDoubt.TryBeginOpeningChoice();
        Check(failedDoubt.FailOpeningChoice() && !failedDoubt.TryBeginOpeningChoice(),
            "A failed exchange must close the combat window instead of retrying.");

        WesternEntryPracticeState virtue = Start(WesternEntryPracticeState.VirtueAndHappiness);
        Check(virtue.TryBeginOpeningChoice() && virtue.TryCommitOpeningChoice(["BASH"])
              && virtue.TryCompleteOpeningChoice() && virtue.PracticeCardModelId == "BASH",
            "The virtue opening must bind at most one chosen practice card.");
        Check(virtue.GetCostChange("BASH", WesternPracticeCardKind.Attack, 2m, false, true) == default,
            "A practice card must not become cheaper before it waits.");
        Check(virtue.ResolveRetains(false, true).RetainPractice && virtue.PracticeWaits == 1,
            "Keeping the practice card must retain it and accumulate one wait.");
        virtue.CloseTurn();
        virtue.BeginTurn(2);
        WesternEntryCostChange practiceCost = virtue.GetCostChange("BASH", WesternPracticeCardKind.Attack,
            2m, false, trackedPractice: true);
        WesternEntryPracticeState restoredVirtue = RoundTrip(virtue);
        Check(restoredVirtue.PracticeCardModelId == "BASH" && restoredVirtue.PracticeWaits == 1,
            "Save/load must preserve the practice identity and accumulated waiting discount.");
        Check(practiceCost is { Amount: -1m, Effect: WesternEntryCostEffect.PracticeDiscount }
              && virtue.GetCostChange("COPY", WesternPracticeCardKind.Attack, 2m, false, false) == default,
            "Only the tracked practice instance may use its accumulated discount.");
        WesternEntryPlayResolution practicePlay = Play(virtue, "V1", "BASH", WesternPracticeCardKind.Attack,
            cost: 2m, trackedPractice: true, effect: practiceCost.Effect);
        Check(practicePlay.ExhaustPlayedCard && virtue.PracticeCardModelId.Length == 0
              && virtue.PracticeWaits == 0,
            "Redeeming the practice must exhaust it and end the combat-long commitment.");

        WesternEntryPracticeState law = Start(WesternEntryPracticeState.FreedomAndInstitutions);
        Check(Play(law, "L1", "POWER", WesternPracticeCardKind.Power).Counted
              && law.LawKind == WesternPracticeCardKind.Other,
            "Powers must not establish a civic law.");
        Play(law, "L2", "STRIKE", WesternPracticeCardKind.Attack);
        Check(law.LawKind == WesternPracticeCardKind.Attack,
            "The first manual Attack or Skill must establish the law without retroactive benefit.");
        WesternEntryCostChange lawDiscount = law.GetCostChange("BASH", WesternPracticeCardKind.Attack, 2m, false, false);
        Check(lawDiscount.Effect == WesternEntryCostEffect.LawDiscount
              && law.GetCostChange("DEFEND", WesternPracticeCardKind.Skill, 0m, false, false).Effect
                  == WesternEntryCostEffect.LawSurcharge,
            "A law must offer one positive-cost same-kind discount and one opposing-kind surcharge.");
        Play(law, "L3", "BASH", WesternPracticeCardKind.Attack, 2m, effect: lawDiscount.Effect);
        Check(law.GetCostChange("SECOND", WesternPracticeCardKind.Attack, 1m, false, false) == default,
            "The same-kind benefit must be consumed at most once per turn.");
        WesternEntryCostChange lawSurcharge = law.GetCostChange("DEFEND", WesternPracticeCardKind.Skill, 0m, false, false);
        Play(law, "L4", "DEFEND", WesternPracticeCardKind.Skill, 0m, effect: lawSurcharge.Effect);
        Check(law.GetCostChange("SECOND_SKILL", WesternPracticeCardKind.Skill, 1m, false, false) == default,
            "The opposing-kind burden must be consumed at most once per turn.");

        WesternEntryPracticeState other = Start(WesternEntryPracticeState.SelfAndOther);
        Play(other, "O1", "CLEAVE", WesternPracticeCardKind.Attack, target: null, singleTarget: false);
        Check(other.OtherCombatId is null, "Untargeted attacks must not fabricate an Other.");
        Play(other, "O2", "STRIKE", WesternPracticeCardKind.Attack, target: 17, singleTarget: true);
        Check(other.OtherCombatId == 17 && !other.ObserveOtherIntent(17, true),
            "The binding attack must not read intent or receive a benefit in the same turn.");
        other.CloseTurn();
        other.BeginTurn(2);
        Check(other.ObserveOtherIntent(17, true)
              && other.GetCostChange("DEFEND", WesternPracticeCardKind.Skill, 1m, false, false).Effect
                  == WesternEntryCostEffect.OtherResponse
              && other.GetCostChange("STRIKE", WesternPracticeCardKind.Attack, 1m, false, false, 17) == default,
            "An attacking Other must discount the first positive-cost Skill response only.");
        WesternEntryPracticeState restoredOther = RoundTrip(other);
        Check(restoredOther.OtherCombatId == 17 && restoredOther.OtherIntentTurn == 2
              && restoredOther.OtherIntendsAttack == true && !restoredOther.OtherResponseUsed,
            "Save/load must preserve the bound Other and this turn's frozen response.");
        Play(other, "O3", "DEFEND", WesternPracticeCardKind.Skill, effect: WesternEntryCostEffect.OtherResponse);
        Check(other.GetCostChange("SECOND", WesternPracticeCardKind.Skill, 1m, false, false) == default,
            "The Other response must be consumed at most once per turn.");
        other.CloseTurn();
        other.BeginTurn(3);
        Check(other.ObserveOtherIntent(17, false)
              && other.GetCostChange("STRIKE", WesternPracticeCardKind.Attack, 1m, false, false, 18) == default
              && other.GetCostChange("STRIKE", WesternPracticeCardKind.Attack, 1m, false, false, 17).Effect
                  == WesternEntryCostEffect.OtherResponse,
            "A non-attacking Other must discount only an Attack targeting that same enemy.");
        Check(other.ForgetOther(17) && other.OtherCombatId is null,
            "A dead or departed Other must suspend the practice until a later manual binding attack.");

        WesternEntryPracticeState category = Start(WesternEntryPracticeState.LanguageAndMeaning);
        Play(category, "C1", "TWO", WesternPracticeCardKind.Power, 2m);
        Check(category.CategoryBucket == 2 && category.CategoryDiscountsRemaining == 2,
            "The first eligible manual card must establish its current-cost category.");
        Check(category.GetCostChange("ONE", WesternPracticeCardKind.Skill, 1m, false, false) == default
              && category.GetCostChange("THREE", WesternPracticeCardKind.Attack, 3m, false, false).Effect
                  == WesternEntryCostEffect.CategoryDiscount,
            "Category membership must use current cost before this relic, not card type or model identity.");
        Play(category, "C2", "THREE", WesternPracticeCardKind.Attack, 3m,
            effect: WesternEntryCostEffect.CategoryDiscount);
        WesternEntryPracticeState restoredCategory = RoundTrip(category);
        Check(restoredCategory.CategoryBucket == 2 && restoredCategory.CategoryDiscountsRemaining == 1,
            "Save/load must preserve the current category and remaining discounts.");
        Play(category, "C3", "TWO_SKILL", WesternPracticeCardKind.Skill, 2m,
            effect: WesternEntryCostEffect.CategoryDiscount);
        Check(category.CategoryDiscountsRemaining == 0
              && category.GetCostChange("FOUR", WesternPracticeCardKind.Power, 4m, false, false) == default,
            "A category may discount only the next two matching plays.");
        category.CloseTurn();
        category.BeginTurn(2);
        Play(category, "C4", "ZERO", WesternPracticeCardKind.Skill, 0m);
        Check(category.CategoryBucket == 0 && category.CategoryDiscountsRemaining == 0,
            "A zero-cost category may be shown but must not mint a discount.");

        WesternEntryPracticeState convention = Start(WesternEntryPracticeState.HistoryAndPower);
        Play(convention, "R1", "A", WesternPracticeCardKind.Attack);
        Play(convention, "R2", "A", WesternPracticeCardKind.Attack);
        Play(convention, "R3", "B", WesternPracticeCardKind.Skill);
        Check(convention.CloseTurn() && !convention.CloseTurn(), "Turn history must close exactly once.");
        Check(convention.ConventionCardModelId == "A" && convention.ConventionFormedTurn == 1,
            "A unique repeated model must become convention for later turns.");
        convention.BeginTurn(2);
        WesternEntryPracticeState restoredConvention = RoundTrip(convention);
        Check(restoredConvention.ConventionCardModelId == "A"
              && restoredConvention.ConventionFormedTurn == 1 && !restoredConvention.ConventionUsed,
            "Save/load must preserve the inherited convention before it is invoked.");
        Check(convention.GetCostChange("A", WesternPracticeCardKind.Attack, 0m, false, false) == default,
            "A zero-cost convention card must neither invoke nor consume convention.");
        WesternEntryCostChange conventionCost = convention.GetCostChange("A", WesternPracticeCardKind.Attack, 1m, false, false);
        WesternEntryPlayResolution conventionPlay = Play(convention, "R4", "A", WesternPracticeCardKind.Attack,
            effect: conventionCost.Effect);
        Check(conventionPlay.ApplyEnemyStrength && convention.ConventionUsed
              && convention.GetCostChange("A", WesternPracticeCardKind.Attack, 1m, false, false) == default,
            "Invoking convention must discount once and charge exactly one enemy-strength consequence.");
        Play(convention, "R5", "B", WesternPracticeCardKind.Skill);
        Play(convention, "R6", "B", WesternPracticeCardKind.Skill);
        Play(convention, "R7", "C", WesternPracticeCardKind.Skill);
        Play(convention, "R8", "C", WesternPracticeCardKind.Skill);
        convention.CloseTurn();
        Check(convention.ConventionCardModelId == "A",
            "A tied history must preserve the prior convention rather than invent a replacement.");

        WesternEntryPracticeState saved = Start(WesternEntryPracticeState.FreedomAndInstitutions);
        Play(saved, "S1", "STRIKE", WesternPracticeCardKind.Attack);
        WesternEntryCostChange savedDiscount = saved.GetCostChange("BASH", WesternPracticeCardKind.Attack, 2m, false, false);
        Play(saved, "S2", "BASH", WesternPracticeCardKind.Attack, 2m, effect: savedDiscount.Effect);
        string payload = WesternEntryPracticeStateCodec.Encode(saved);
        WesternEntryPracticeState restored = WesternEntryPracticeStateCodec.Restore(payload, saved.ProblemId);
        Check(restored.LawKind == WesternPracticeCardKind.Attack && restored.LawDiscountUsed
              && !Play(restored, "S2", "BASH", WesternPracticeCardKind.Attack).Counted,
            "Save/load must preserve rule state and callback idempotency.");
        Check(WesternEntryPracticeStateCodec.Restore(payload, WesternEntryPracticeState.SelfAndOther).Turn == 0,
            "A payload from another entry must not migrate combat benefits.");
        foreach (string invalid in new[]
        {
            "", "null", "{", payload.Replace("\"Version\":1", "\"Version\":99"),
            payload.Replace("\"LawKind\":1", "\"LawKind\":99"),
            payload.Replace("\"RecordedPlayIds\":[\"S1\",\"S2\"]", "\"RecordedPlayIds\":[\"S1\",\"S1\"]"),
        })
        {
            WesternEntryPracticeState reset = WesternEntryPracticeStateCodec.Restore(invalid, saved.ProblemId);
            Check(!reset.CombatActive && reset.Turn == 0 && reset.LawKind == WesternPracticeCardKind.Other,
                "Invalid or legacy payload must fail closed without granting a practice benefit.");
        }

        restored.EndCombat();
        Check(!restored.CombatActive && restored.Turn == 0 && restored.OpeningStage == WesternOpeningStage.NotStarted
              && restored.LawKind == WesternPracticeCardKind.Other && restored.OtherCombatId is null
              && restored.ConventionCardModelId.Length == 0 && restored.RecordedPlayIds.Count == 0,
            "Combat cleanup must remove every entry practice fact while preserving relic identity.");
        Check(restored.ProblemId == WesternEntryPracticeState.FreedomAndInstitutions,
            "Combat cleanup must preserve the entry identity for the next fight.");

        Console.WriteLine("Western entry practice checks passed: seven distinct rules, non-triggers, idempotency, cleanup and strict save/load.");
    }
}
