using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class AnselmUnfinishedProofNativeChecks
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly Mod = typeof(SocratesQuestionCup).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(Type type, string method, params object[] args) =>
        type.GetMethod(method, Static)!.Invoke(null, args)!;

    private static object Action(string enumName, string actionName) =>
        Enum.Parse(TypeOf(enumName), actionName);

    private static void Commit(Type flow, object state, string actionName, string actionEnum,
        params object[] rest)
    {
        object action = Action(actionEnum, actionName);
        Check((bool)Call(flow, "TryPrepare", state, action), $"Native {flow.Name} prepares {actionName}.");
        Check((bool)Call(flow, "TryCommitPending", [state, .. rest]),
            $"Native {flow.Name} commits {actionName}.");
    }

    private static object CreateShared(string outcome)
    {
        Type graphType = TypeOf("WesternRouteGraph");
        object graph = Call(graphType, "LoadEmbedded");
        object journey = graphType.GetMethod("Start")!.Invoke(graph,
            ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Check((bool)graphType.GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!
            && (bool)graphType.GetMethod("TryDecline")!.Invoke(graph, [journey, "Fixed", 2])!
            && (bool)graphType.GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 2])!,
            "The native knowledge journey reserves both earlier Questions and declines Fixed 2.");

        object sextus = Call(TypeOf("SextusOpposedAppearancesState"), "Create", "SEXTUS:NATIVE:1");
        foreach (string action in new[] { "AcceptInvitation", "SuspendAssent", "Leave" })
            Commit(TypeOf("SextusOpposedAppearancesFlow"), sextus, action, "SextusAppearancesAction");
        object descartes = Call(TypeOf("DescartesMethodicReconstructionState"), "Create", "DESCARTES:NATIVE:2");
        foreach (string action in new[] { "DeclineInvitation", "Leave" })
            Commit(TypeOf("DescartesMethodicReconstructionFlow"), descartes, action,
                "DescartesReconstructionAction");
        object pyrrho = Call(TypeOf("PyrrhoReportedCrossingState"), "Create",
            "PYRRHO:NATIVE:2", sextus, descartes);
        foreach (string action in new[] { "CiteConflict", "InspectAndCross", "Leave" })
            Commit(TypeOf("PyrrhoReportedCrossingFlow"), pyrrho, action,
                "PyrrhoReportedCrossingAction", journey, sextus, descartes);
        object anselm = Call(TypeOf("AnselmUnfinishedProofState"), "Create",
            "ANSELM:NATIVE:3", "PYRRHO:NATIVE:2");
        foreach (string action in new[] { "Accept", "UniqueBeing", "DistinctScope", outcome, "Close" })
            Commit(TypeOf("AnselmUnfinishedProofFlow"), anselm, action,
                "AnselmProofAction", journey, pyrrho, graph);

        Type sharedType = TypeOf("PhilosophyRunState");
        object shared = Activator.CreateInstance(sharedType, nonPublic: true)!;
        foreach ((string name, object value) in new[]
        {
            ("WesternJourney", journey), ("SextusOpposedAppearances", sextus),
            ("DescartesMethodicReconstruction", descartes),
            ("PyrrhoReportedCrossing", pyrrho), ("AnselmUnfinishedProof", anselm),
        }) sharedType.GetProperty(name)!.SetValue(shared, value);
        return shared;
    }

    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            NativeRuntimeFixture fixture = new(typeof(SocratesQuestionCup));
            RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            Set(run, "_players", new List<Player> { fixture.Player });
            Set(run, "_currentActIndex", 2);
            Set(manager, "State", run);
            string[] outcomes = ["Publish", "Revise", "Withdraw"];
            for (int index = 0; index < outcomes.Length; index++)
            {
                string outcome = outcomes[index];
                object shared = CreateShared(outcome);
                Call(TypeOf("PhilosophyRunStateService"), "Restore", run, shared);
                object?[] eligibility = [null];
                bool eligible = (bool)typeof(SocratesQuestionCup)
                    .GetMethod("TryGetAnselmDisposition", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(fixture.Relic, eligibility)!;
                Check(eligible && eligibility[0]?.ToString() == outcome + (outcome == "Publish" ? "ed"
                    : outcome == "Revise" ? "d" : "n"),
                    "The native Question Cup sees the saved closed Anselm outcome.");
                CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
                Set(room, "Id", 12 + index);
                Set(run, "_currentRooms", new List<AbstractRoom> { room });
                Set(run, "ActFloor", 4 + index);
                fixture.PlayerState.Energy = 0;
                await fixture.Relic.BeforeCombatStart();
                await fixture.Relic.BeforeSideTurnStart(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
                    [fixture.Player.Creature], fixture.Combat);
                int beforeEnergy = fixture.PlayerState.Energy;
                int beforeHand = fixture.PlayerState.Hand.Cards.Count;
                int beforeBlock = fixture.Player.Creature.Block;
                if (outcome == "Withdraw")
                {
                    await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
                    Check(fixture.Player.Creature.Block == beforeBlock,
                        "A withdrawn claim does not pay before the second turn.");
                    fixture.PlayerState.IncrementTurnNumber();
                    await fixture.Relic.BeforeSideTurnStart(null!,
                        MegaCrit.Sts2.Core.Combat.CombatSide.Player,
                        [fixture.Player.Creature], fixture.Combat);
                }
                await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
                Check(outcome switch
                {
                    "Publish" => fixture.PlayerState.Energy == beforeEnergy + 1,
                    "Revise" => fixture.Player.Creature.Block == beforeBlock + 6,
                    _ => fixture.Player.Creature.Block == beforeBlock + 8,
                }, $"{outcome} native turn effect: energy {beforeEnergy}->{fixture.PlayerState.Energy}, hand {beforeHand}->{fixture.PlayerState.Hand.Cards.Count}, Block {fixture.Player.Creature.Block}, payload {((SocratesQuestionCup)fixture.Relic).AnselmPracticePayload}.");
                string payload = ((SocratesQuestionCup)fixture.Relic).AnselmPracticePayload;
                typeof(SocratesQuestionCup).GetProperty("AnselmPracticePayload")!
                    .SetValue(fixture.Relic, payload);
                int energy = fixture.PlayerState.Energy;
                int hand = fixture.PlayerState.Hand.Cards.Count;
                int block = fixture.Player.Creature.Block;
                await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
                Check(fixture.PlayerState.Energy == energy
                    && fixture.PlayerState.Hand.Cards.Count == hand
                    && fixture.Player.Creature.Block == block,
                    "A loaded receipt cannot pay the same combat twice.");
            }
            Console.WriteLine("Anselm native practice passed: saved third-act route, first-turn Energy/Block and second-turn Block, plus duplicate-gated relic callbacks.");
        }
        finally { Set(manager, "State", prior); }
    }
}
