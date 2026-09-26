using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class ZenoAssentBoundaryTestConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "zenoassenttest";
    public override string Args => "";
    public override string Description =>
        "Enter the development-only Zeno assent event from a safe Act 3 map boundary.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 0)
        {
            return new CmdResult(false, "Usage: zenoassenttest");
        }

        RunManager runManager = RunManager.Instance;
        if (issuingPlayer is null ||
            !runManager.IsInProgress ||
            runManager.DebugOnlyGetState() is not { Players.Count: 1 } runState ||
            !ReferenceEquals(issuingPlayer.RunState, runState))
        {
            return new CmdResult(false, "A current single-player run is required.");
        }

        if (CombatManager.Instance.IsInProgress)
        {
            return new CmdResult(false, "Use this command outside combat.");
        }

        if (runState.CurrentRoom is not { } currentRoom ||
            runState.BaseRoom is not { } baseRoom)
        {
            return new CmdResult(false, "The current room boundary is unavailable.");
        }

        PhilosophyRunState sharedState = PhilosophyRunStateService.GetOrCreate(runState);
        ZenoRouteObservedEventKind eventKind = currentRoom is EventRoom eventRoom
            ? eventRoom.LocalMutableEvent is IZenoRouteEventSceneIdentity identity
                ? identity.RouteEventKind
                : ZenoRouteObservedEventKind.Unrelated
            : ZenoRouteObservedEventKind.None;
        if (!ZenoAssentBoundaryTestEntryPolicy.TryCreatePlan(
                runState.Rng.Seed,
                sharedState.ZenoRoutePayload.Classification,
                sharedState.PreservedSaveMarkerEntries.Count > 0,
                new ZenoRouteNativeMapBoundary(
                    runState.CurrentActIndex,
                    runState.CurrentRoomCount,
                    currentRoom.Id,
                    currentRoom.IsPreFinished,
                    currentRoom is MapRoom,
                    eventKind),
                out ZenoAssentBoundaryTestEntryPlan? plan) ||
            plan is null)
        {
            return new CmdResult(
                false,
                "Requires Act 3 at a completed safe room with no existing or preserved Zeno route.");
        }

        sharedState.SetCurrentZenoRouteState(plan.InitialState, plan.Catalog);
        if (!ZenoRouteRuntimeService.TryGetOrCreate(
                runState,
                plan.Catalog,
                out ZenoRoutePersistenceRuntime? runtime) ||
            runtime is null)
        {
            return new CmdResult(false, "The development Zeno route could not initialize safely.");
        }

        return new CmdResult(
            EnterAsync(runManager, baseRoom, runtime, plan),
            true,
            "Preparing the development-only Zeno event with an explicit NoMaterial record.");
    }

    private static async Task EnterAsync(
        RunManager runManager,
        AbstractRoom baseRoom,
        ZenoRoutePersistenceRuntime runtime,
        ZenoAssentBoundaryTestEntryPlan plan)
    {
        try
        {
            ZenoRouteTransitionResult switchPrepared = ZenoRouteStateService.PrepareSwitch(
                runtime.CurrentState,
                runtime.CurrentState.Route!.Revision,
                plan.Material,
                plan.Catalog);
            ZenoRoutePersistenceCoordinationResult switched =
                await runtime.ExecuteAsync(switchPrepared);
            if (switched.Status != ZenoRoutePersistenceCoordinationStatus.Completed ||
                switched.State.Route?.Stage != ZenoRouteStage.WaitingInterval)
            {
                throw new InvalidOperationException("The development Switch checkpoint did not persist.");
            }

            string receipt = plan.TriggerPlan.Context.CompletedRoomReceiptId!;
            ZenoRouteTransitionResult intervalPrepared =
                ZenoRouteStateService.PrepareIntervalCompletion(
                    runtime.CurrentState,
                    runtime.CurrentState.Route!.Revision,
                    receipt,
                    plan.Catalog);
            ZenoRoutePersistenceCoordinationResult ready =
                await runtime.ExecuteAsync(intervalPrepared);
            if (ready.Status != ZenoRoutePersistenceCoordinationStatus.Completed ||
                ready.State.Route?.Stage != ZenoRouteStage.ReadyToClaim)
            {
                throw new InvalidOperationException("The development interval checkpoint did not persist.");
            }

            if (!ZenoRouteNativeTriggerRegistry.TryGet(
                    runManager,
                    out ZenoRouteNativeTriggerSession? session) ||
                session is null)
            {
                throw new InvalidOperationException("The development Zeno trigger session was unavailable.");
            }

            NMapScreen.Instance?.SetTravelEnabled(false);
            ZenoRouteNativeTriggerStatus status = await session.TriggerAsync(
                plan.TriggerPlan,
                baseRoom);
            if (status != ZenoRouteNativeTriggerStatus.Entered)
            {
                throw new InvalidOperationException(
                    $"The development Zeno event did not enter safely: {status}.");
            }

            Log.Info("[STS2Philosophers] Entered the development-only Zeno assent event.");
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[STS2Philosophers] Development Zeno entry stopped at a recoverable checkpoint: {exception}");
            throw;
        }
    }
}
