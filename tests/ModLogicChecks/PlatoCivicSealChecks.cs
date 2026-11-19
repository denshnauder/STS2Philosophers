using STS2Philosophers;

internal static class PlatoCivicSealChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        Check(Transition(PhilosophersGazePage.PLATO_A, PhilosophersGazeOption.PlatoB).Destination
                  == PhilosophersGazePage.PLATO_B
              && Transition(PhilosophersGazePage.PLATO_A, PhilosophersGazeOption.PlatoC).Destination
                  == PhilosophersGazePage.PLATO_C
              && Transition(PhilosophersGazePage.PLATO_A, PhilosophersGazeOption.PlatoD).Destination
                  == PhilosophersGazePage.PLATO_D,
            "The gate roll call must expose three distinct successor pages.");
        Check(Transition(PhilosophersGazePage.PLATO_B, PhilosophersGazeOption.PlatoSealB).Effect
                  == PhilosophersGazeEffect.ObtainPlatoCivicSealB
              && Transition(PhilosophersGazePage.PLATO_C, PhilosophersGazeOption.PlatoSealC).Effect
                  == PhilosophersGazeEffect.ObtainPlatoCivicSealC
              && Transition(PhilosophersGazePage.PLATO_B, PhilosophersGazeOption.PlatoSealB).UsesNativeProceed
              && Transition(PhilosophersGazePage.PLATO_C, PhilosophersGazeOption.PlatoSealC).UsesNativeProceed,
            "Each of the two successor accept paths must grant the seal and finish the event.");
        Check(Transition(PhilosophersGazePage.PLATO_B, PhilosophersGazeOption.PlatoDecline).Destination
                  == PhilosophersGazePage.PLATO_DECLINE
              && Transition(PhilosophersGazePage.PLATO_C, PhilosophersGazeOption.PlatoDecline).Destination
                  == PhilosophersGazePage.PLATO_DECLINE
              && Transition(PhilosophersGazePage.PLATO_D, PhilosophersGazeOption.PlatoLeave).Destination
                  == PhilosophersGazePage.PLATO_DECLINE,
            "Both successor declines and the third-page leave must share the no-relic ending.");
        Check(PhilosophersGazeFlowPolicy.GetOptions(PhilosophersGazePage.PLATO_A).SequenceEqual(
                  [PhilosophersGazeOption.PlatoB, PhilosophersGazeOption.PlatoC, PhilosophersGazeOption.PlatoD])
              && PhilosophersGazeFlowPolicy.GetOptions(PhilosophersGazePage.PLATO_B).SequenceEqual(
                  [PhilosophersGazeOption.PlatoSealB, PhilosophersGazeOption.PlatoDecline])
              && PhilosophersGazeFlowPolicy.GetOptions(PhilosophersGazePage.PLATO_C).SequenceEqual(
                  [PhilosophersGazeOption.PlatoSealC, PhilosophersGazeOption.PlatoDecline])
              && PhilosophersGazeFlowPolicy.GetOptions(PhilosophersGazePage.PLATO_D).SequenceEqual(
                  [PhilosophersGazeOption.PlatoLeave]),
            "The four Plato pages must expose exactly the intended clickable options.");


        WesternRouteDefinition checks = WesternRouteTable.Instance.Routes.Single(route =>
            route.RouteId == "CHECKS_AND_JUSTICE");
        WesternRouteDefinition sympathy = WesternRouteTable.Instance.Routes.Single(route =>
            route.RouteId == "SYMPATHY_AND_PUBLIC_ACTION");
        Check(checks.EntryThinker == "PLATO" && checks.EntryProblem == "FREEDOM_AND_INSTITUTIONS"
              && checks.ActTwoFigure == "MONTESQUIEU"
              && sympathy.EntryThinker == "PLATO" && sympathy.EntryProblem == "FREEDOM_AND_INSTITUTIONS"
              && sympathy.ActTwoFigure == "ADAM_SMITH",
            "The two seal-granting accepts must open the checks/justice and sympathy/public-action lines.");

        Console.WriteLine("Plato Civic Seal page checks passed: A/B/C/D navigation, two seal-granting accepts, three no-relic exits and exact option order.");
    }

    private static PhilosophersGazeTransition Transition(
        PhilosophersGazePage page,
        PhilosophersGazeOption option)
    {
        if (!PhilosophersGazeFlowPolicy.TryGetTransition(page, option, out PhilosophersGazeTransition transition))
            throw new InvalidOperationException($"Missing Plato transition {page} -> {option}.");
        return transition;
    }
}
