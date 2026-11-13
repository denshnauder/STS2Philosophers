using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Reviewed release handovers. Each line keeps the installed completed-slot
// counter and only allocates its own saved person at the Ancient boundary.
internal static class ActFixedEntryHost
{
    internal static bool TryPrepare(RunState run, out EventRoom? room)
    {
        room = null;
        if (run.CurrentActIndex is not (1 or 2)) return false;
        using (FixedEntryScope.Begin())
        {
            EventModel? model = TrySelectReviewedTurns(run) ?? TrySelectReviewedDefaults(run);
            model ??= run.CurrentActIndex == 1
                ? LockeCargoIdentityPatch.TrySelectNative(run)
                : HumeInductionPatch.TrySelectNative(run);
            model ??= TrySelectSocraticLine(run);
            if (model is null) return false;
            room = new EventRoom(model);
            return true;
        }
    }

    private static EventModel? TrySelectSocraticLine(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1
            || !run.Players[0].Relics.OfType<SocratesBalanceWeight>().Any(relic => relic.CanOfferNativeQuestion)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey) return null;
        if (journey.RouteId.Length == 0
            && (journey.CurrentNodeId == SchopenhauerWillDenialState.FixedSourceNodeId
                || journey.CurrentNodeId == "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE"))
            journey.RouteId = "WILL_AND_VALUE";
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        if (run.CurrentActIndex == 1)
        {
            if (journey.CurrentNodeId != SchopenhauerWillDenialState.FixedSourceNodeId
                || journey.LastFixedAct != 1) return null;
            SchopenhauerWillDenialState? saved = shared.SchopenhauerWillDenial;
            if (saved is { Closed: true }) return null;
            var candidate = saved ?? SchopenhauerWillDenialState.CreateAt(
                $"SCHOPENHAUER_WILL_DENIAL:{run.Rng.Seed}:2", string.Empty, 2, true);
            if (!candidate.FixedMode || candidate.ActIndex != 2
                || candidate.InstanceId != $"SCHOPENHAUER_WILL_DENIAL:{run.Rng.Seed}:2"
                || !SchopenhauerWillDenialFlow.IsRouteConsistent(candidate, journey, shared.KantApperception)
                || !graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == SchopenhauerWillDenialState.FixedCompletionEdgeId)) return null;
            EventModel canonical = ModelDb.Event<SchopenhauerWillDenial>();
            if (run.VisitedEventIds.Contains(canonical.Id)) return null;
            shared.SchopenhauerWillDenial = candidate;
            return canonical;
        }
        if (journey.CurrentNodeId != "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE"
            || journey.LastFixedAct != 2 || shared.SchopenhauerWillDenial is not { Closed: true, FixedMode: true }
            || !journey.CompletedEdgeIds.Contains(SchopenhauerWillDenialState.FixedCompletionEdgeId)) return null;
        NietzscheRevaluationState? existing = shared.NietzscheRevaluation;
        if (existing is { Closed: true }) return null;
        var next = existing ?? NietzscheRevaluationState.CreateAt(
            $"NIETZSCHE_REVALUATION:{run.Rng.Seed}:3", 3, "WESTERN_EDGE_151");
        if (existing is null)
        {
            next.RoutePlaced = true;
            next.RouteSourceNodeId = journey.CurrentNodeId;
        }
        if (next.ActIndex != 3 || next.EdgeId != "WESTERN_EDGE_151"
            || next.InstanceId != $"NIETZSCHE_REVALUATION:{run.Rng.Seed}:3"
            || !NietzscheRevaluationFlow.IsRouteConsistent(next, journey)
            || !graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_151")) return null;
        EventModel selected = ModelDb.Event<NietzscheRevaluation>();
        if (run.VisitedEventIds.Contains(selected.Id)) return null;
        shared.NietzscheRevaluation = next;
        return selected;
    }

    // The three independently reviewed default lines are installed together.
    // Existing unrelated lines keep the previously released handover.
    private static EventModel? TrySelectReviewedDefaults(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || !NativeQuestionNodeEvents.HasSoleDoctrineCarrier(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey || journey.RetainedEnding) return null;
        var player = run.Players[0];
        string route, entry;
        if (player.Relics.OfType<HeraclitusLyre>().Any(relic => relic.CanOfferNativeQuestion))
        { route = "NATURE_AND_BECOMING"; entry = "HERACLITUS__BEING_AND_CHANGE__CORE"; }
        else if (player.Relics.OfType<AristotleCategoryTablet>().Any(relic => relic.CanOfferNativeQuestion))
        { route = "FORMAL_LANGUAGE"; entry = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE"; }
        else if (player.Relics.OfType<RousseauUncarvedStone>().Any(relic => relic.CanOfferNativeQuestion))
        { route = "EDUCATION_AND_SITUATION"; entry = "ROUSSEAU__HISTORY_AND_POWER__CORE"; }
        else if (player.Relics.OfType<PlatoCivicSeal>().Any(relic => relic.CanOfferNativeQuestion))
        { route = "CHECKS_AND_JUSTICE"; entry = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"; }
        else return null;
        string parent = route switch
        {
            "NATURE_AND_BECOMING" => SpinozaCauseNetworkState.AdoptedNodeId,
            "FORMAL_LANGUAGE" => FregeSenseState.AdoptedNodeId,
            "CHECKS_AND_JUSTICE" => MontesquieuBalanceState.AdoptedNodeId,
            _ => WollstonecraftAdmissionState.AdoptedNodeId,
        };
        int act = run.CurrentActIndex + 1;
        if (act == 2 && journey.CurrentNodeId != entry
            || act == 3 && journey.CurrentNodeId != parent
            || journey.LastFixedAct > act
            || journey.DeclinedFixedActs.Contains(act)) return null;
        int previous = journey.LastFixedAct;
        string oldRoute = journey.RouteId, oldEntry = journey.EntryNodeId;
        bool oldRules = journey.RouteRules;
        int oldAct = journey.Act;
        journey.RouteId = route; journey.EntryNodeId = entry;
        journey.RouteRules = true; journey.Act = act; journey.LastFixedAct = act;
        EventModel? selected = null;
        try
        {
            selected = (route, act) switch
        {
            ("CHECKS_AND_JUSTICE", 2) => MontesquieuBalancePatch.TrySelectNative(run),
            ("CHECKS_AND_JUSTICE", 3) => KantUniversalizationPatch.TrySelectNative(run),
            ("NATURE_AND_BECOMING", 2) => SpinozaCauseNetworkPatch.TrySelectNative(run),
            ("NATURE_AND_BECOMING", 3) => SchellingNaturePatch.TrySelectNative(run),
            ("FORMAL_LANGUAGE", 2) => FregeSensePatch.TrySelectNative(run),
            ("FORMAL_LANGUAGE", 3) => RussellAnalysisPatch.TrySelectNative(run),
            ("EDUCATION_AND_SITUATION", 2) => WollstonecraftAdmissionPatch.TrySelectNative(run),
            ("EDUCATION_AND_SITUATION", 3) => BeauvoirOtherPatch.TrySelectNative(run),
            _ => null,
            };
        }
        catch (Exception exception)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[STS2Philosophers] Reviewed fixed handover failed: {exception}");
        }
        if (selected is null)
        {
            journey.RouteId = oldRoute; journey.EntryNodeId = oldEntry;
            journey.RouteRules = oldRules; journey.Act = oldAct; journey.LastFixedAct = previous;
        }
        return selected;
    }

    private static EventModel? TrySelectReviewedTurns(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || !NativeQuestionNodeEvents.HasSoleDoctrineCarrier(run)
            || !PhilosophyRunStateService.TryGet(run, out var shared)
            || shared?.WesternJourney is not { TurnedAct: 1 } journey) return null;
        int act = run.CurrentActIndex + 1;
        if (journey.DeclinedFixedActs.Contains(act) || journey.LastFixedAct > act) return null;
        bool carrier = journey.RouteId == "STRUCTURE_AND_EXCLUSION"
            ? run.Players[0].Relics.OfType<AristotleCategoryTablet>().Any(relic => relic.CanOfferNativeQuestion)
            : journey.RouteId == "FALLIBLE_INQUIRY"
                ? run.Players[0].Relics.OfType<SocratesQuestionCup>().Any(relic => relic.CanOfferNativeQuestion)
                : journey.RouteId is "SOCIAL_CRITIQUE" or "SOCIAL_KNOWLEDGE" or "GENEALOGY_AND_POWER"
                ? run.Players[0].Relics.OfType<RousseauUncarvedStone>().Any(relic => relic.CanOfferNativeQuestion)
                : journey.RouteId is "APPEARANCE_AND_INTERPRETATION" or "UNCONSCIOUS_AND_NORM" or "EMBODIED_PERCEPTION"
                    && run.Players[0].Relics.OfType<DescartesThinkingLens>().Any(relic => relic.CanOfferNativeQuestion);
        if (!carrier) return null;
        int prior = journey.LastFixedAct, priorAct = journey.Act;
        journey.LastFixedAct = act; journey.Act = act;
        EventModel? model;
        try
        {
            model = (journey.RouteId, act) switch
            {
                ("STRUCTURE_AND_EXCLUSION", 2) => LeviStraussMythPatch.TrySelectNative(run),
                ("STRUCTURE_AND_EXCLUSION", 3) => DerridaDeconstructionPatch.TrySelectNative(run),
                ("APPEARANCE_AND_INTERPRETATION", 2) => HeideggerDaseinPatch.TrySelectNative(run),
                ("APPEARANCE_AND_INTERPRETATION", 3) => GadamerFusionPatch.TrySelectNative(run),
                ("UNCONSCIOUS_AND_NORM", 2) => LacanSignifierPatch.TrySelectNative(run),
                ("UNCONSCIOUS_AND_NORM", 3) => ButlerAddressPatch.TrySelectNative(run),
                ("SOCIAL_CRITIQUE", 2) => HorkheimerReasonPatch.TrySelectNative(run),
                ("SOCIAL_CRITIQUE", 3) => HabermasCommunicationPatch.TrySelectNative(run),
                ("SOCIAL_KNOWLEDGE", 2) => ComtePositivismPatch.TrySelectNative(run),
                ("SOCIAL_KNOWLEDGE", 3) => KuhnParadigmPatch.TrySelectNative(run),
                ("EMBODIED_PERCEPTION", 2) => MerleauPontyBodyPatch.TrySelectNative(run),
                ("EMBODIED_PERCEPTION", 3) => BeauvoirOtherPatch.TrySelectNative(run),
                ("FALLIBLE_INQUIRY", 2) => PeirceInquiryPatch.TrySelectNative(run),
                ("FALLIBLE_INQUIRY", 3) => DeweyPublicExperimentPatch.TrySelectNative(run),
                ("GENEALOGY_AND_POWER", 2) => FoucaultPowerPatch.TrySelectNative(run),
                ("GENEALOGY_AND_POWER", 3) => ArendtBeginningPatch.TrySelectNative(run),
                _ => null,
            };
        }
        catch { journey.LastFixedAct = prior; journey.Act = priorAct; throw; }
        if (model is null) { journey.LastFixedAct = prior; journey.Act = priorAct; }
        return model;
    }
}
