using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class RouteEventMapBoundaryNativeChecks
{
    private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Assembly ModAssembly = typeof(DemocritusArrangement).Assembly;
    private static readonly Dictionary<short, OpCode> Opcodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);

    public static Task Run()
    {
        Type boundary = ModType("RouteEventMapBoundary");
        MethodInfo registered = RequiredMethod(boundary, "IsRegisteredEvent", [typeof(EventModel)]);
        MethodInfo matches = RequiredMethod(boundary, "MatchesSavedRoom", [typeof(SerializableRun), typeof(EventRoom)]);
        bool IsRegistered(EventModel? model) => (bool)registered.Invoke(null, [model])!;

        Type[] eventTypes =
        [
            typeof(DemocritusArrangement), typeof(ParmenidesInquiry), typeof(AristotleCivicInquiry),
            typeof(PlatoWholeArrangement), typeof(ProtagorasSharedDeliberation), typeof(EpicurusSharedMargin),
            typeof(SextusOpposedAppearances), typeof(DescartesMethodicReconstruction), typeof(HanFeiPublicStandard),
            typeof(MachiavelliEmergencyMandate)
        ];
        Check(!IsRegistered(null), "ENC123: a missing event is not registered for the map boundary.");
        foreach (Type eventType in eventTypes)
        {
            ModelDb.Inject(eventType);
            EventModel canonical = ModelDb.GetById<EventModel>(ModelDb.GetId(eventType));
            Check(IsRegistered(canonical), $"ENC123: {eventType.Name} is registered for the shared map boundary.");
            CheckNativeCheckpoint(canonical, matches);
        }

        // Select an ordinary built-in event from the loaded game assembly, without
        // depending on a localized name or constructing any rendered game node.
        Type nativeEventType = typeof(EventModel).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.ContainsGenericParameters
                && type.Namespace == "MegaCrit.Sts2.Core.Models.Events"
                && typeof(EventModel).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.FullName)
            .First();
        ModelDb.Inject(nativeEventType);
        EventModel nativeEvent = ModelDb.GetById<EventModel>(ModelDb.GetId(nativeEventType));
        Check(!IsRegistered(nativeEvent), "ENC123: an ordinary native event is not a registered route event.");

        CheckSavePrefixBoundaries();
        CheckRewardsGate(boundary);
        CheckNativeReturnChain();
        CheckPatchTarget("RouteEventMapBoundaryPatch", typeof(NMapScreen), nameof(NMapScreen.Open), [typeof(bool)]);
        CheckPatchTarget("RouteEventMapBoundaryRewardsProceedPatch", typeof(RunManager),
            nameof(RunManager.ProceedFromTerminalRewardsScreen), Type.EmptyTypes);
        Console.WriteLine("ENC123 native map boundary checks passed: ten registered events, exact native room round-trips and checkpoint rejection, explicit/no-run save prefix boundaries, rewards mounting/skip gates, current DLL rewards-to-map calls, and both shared patch targets; no rendered UI or actual run save.");
        return Task.CompletedTask;
    }

    private static void CheckNativeCheckpoint(EventModel canonical, MethodInfo matches)
    {
        EventRoom expected = new(canonical);
        SerializableRoom saved = expected.ToSerializable();
        Check(saved.RoomType == RoomType.Event && Equals(saved.EventId, canonical.Id) && !saved.IsPreFinished,
            $"ENC123: {canonical.GetType().Name} serializes its exact event identity and open flag.");
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(saved, null!)!;
        SerializableRoom roundTrip = restored.ToSerializable();
        Check(ReferenceEquals(restored.CanonicalEvent, canonical) && roundTrip.RoomType == RoomType.Event
            && Equals(roundTrip.EventId, canonical.Id) && !roundTrip.IsPreFinished,
            $"ENC123: native restore preserves {canonical.GetType().Name} without starting a room or combat.");

        SerializableRun snapshot = new() { PreFinishedRoom = roundTrip };
        bool Matches(EventRoom room) => (bool)matches.Invoke(null, [snapshot, room])!;
        Check(Matches(expected), "ENC123: an exact native event checkpoint is accepted.");
        snapshot.PreFinishedRoom = null;
        Check(!Matches(expected), "ENC123: a checkpoint without a native room cannot confirm the event.");
        snapshot.PreFinishedRoom = roundTrip;
        roundTrip.EventId = null;
        Check(!Matches(expected), "ENC123: a missing native EventId cannot confirm the event.");
        roundTrip.EventId = new ModelId(canonical.Id.Category, "OTHER_EVENT");
        Check(!Matches(expected), "ENC123: a different native EventId cannot confirm the event.");
        roundTrip.EventId = canonical.Id;
        roundTrip.RoomType = (RoomType)(-1);
        Check(!Matches(expected), "ENC123: a different native RoomType cannot confirm the event.");
        roundTrip.RoomType = RoomType.Event;
        roundTrip.IsPreFinished = true;
        Check(!Matches(expected), "ENC123: a completed checkpoint cannot confirm an open native event.");

        EventRoom completed = (EventRoom)AbstractRoom.FromSerializable(roundTrip, null!)!;
        Check(completed.IsPreFinished && completed.ToSerializable().IsPreFinished && Matches(completed),
            "ENC123: native restore and checkpoint matching preserve a completed event's flag.");
        roundTrip.IsPreFinished = false;
        Check(!Matches(completed), "ENC123: an open checkpoint cannot confirm a completed native event.");
    }

    private static void CheckSavePrefixBoundaries()
    {
        MethodInfo prefix = RequiredMethod(ModType("RouteEventMapBoundarySaveRoomPatch"), "Prefix",
            [typeof(RunManager), typeof(AbstractRoom).MakeByRefType()]);
        RunManager manager = RunManager.Instance;
        EventRoom explicitRoom = new(ModelDb.Event<DemocritusArrangement>());
        object?[] explicitArguments = [manager, explicitRoom];
        prefix.Invoke(null, explicitArguments);
        Check(ReferenceEquals(explicitArguments[1], explicitRoom),
            "ENC123: the real save prefix preserves an explicitly supplied EventRoom by reference.");

        // The managed fixture fills ActionExecutor but never creates a run State.
        // Keep that precondition explicit rather than fabricating a scene or save.
        Check(!manager.IsInProgress, "ENC123: the save prefix no-run case requires no active native run.");
        object?[] noRunArguments = [manager, null];
        prefix.Invoke(null, noRunArguments);
        Check(noRunArguments[1] is null,
            "ENC123: the real save prefix does not invent an event identity when no run is active.");
    }

    private static void CheckRewardsGate(Type boundary)
    {
        MethodInfo gate = RequiredMethod(boundary, "RewardsPermitBoundary",
            [typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool)]);
        (int Count, bool RewardOverlay, bool Complete, bool Acknowledged, bool Combat, bool Accepted, string Reason)[] cases =
        [
            (0, false, false, false, true, false, "combat rewards may not have mounted yet"),
            (1, true, false, false, true, false, "opening the map cannot skip unacknowledged rewards"),
            (1, true, false, true, true, true, "the acknowledged combat source permits explicit reward skipping"),
            (1, true, true, false, true, true, "a completed terminal rewards overlay remains on the native stack"),
            (0, false, false, true, true, true, "an acknowledged combat source also permits an empty overlay stack"),
            (1, false, false, true, true, false, "acknowledgment cannot bypass an unrelated overlay"),
            (2, true, true, true, true, false, "acknowledgment cannot bypass a nested reward selection"),
            (0, false, false, false, false, true, "a noncombat room needs no combat rewards acknowledgment")
        ];
        foreach (var candidate in cases)
        {
            bool actual = (bool)gate.Invoke(null,
                [candidate.Count, candidate.RewardOverlay, candidate.Complete, candidate.Acknowledged, candidate.Combat])!;
            Check(actual == candidate.Accepted, $"ENC123: {candidate.Reason}.");
        }
    }

    private static void CheckNativeReturnChain()
    {
        MethodInfo proceed = RequiredMethod(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen), Type.EmptyTypes);
        MethodInfo open = RequiredMethod(typeof(NMapScreen), nameof(NMapScreen.Open), [typeof(bool)]);
        var proceedReferences = ReferencesIncludingAsyncBody(proceed).ToArray();
        Check(proceedReferences.Any(reference => reference.Target.DeclaringType == typeof(NMapScreen)
                && reference.Target.Name == nameof(NMapScreen.Open)),
            "ENC123: the current native terminal rewards continuation opens the map screen.");
        Check(!proceedReferences.Any(reference => reference.Target.DeclaringType == typeof(RunManager)
                && reference.Target.Name == "EnterRoomInternal"),
            "ENC123: returning from terminal rewards does not enter a new MapRoom.");

        var openReferences = ReferencesIncludingAsyncBody(open).ToArray();
        Check(!openReferences.Any(reference => reference.Opcode == OpCodes.Newobj
                && reference.Target.DeclaringType == typeof(MapRoom)),
            "ENC123: opening the current native map screen does not construct a MapRoom.");
        Check(!openReferences.Any(reference => reference.Target.DeclaringType == typeof(RunManager)
                && reference.Target.Name == "EnterRoomInternal"),
            "ENC123: opening the native map screen does not call the former room-entry trigger.");
    }

    private static void CheckPatchTarget(string patchName, Type expectedOwner, string expectedName, Type[] parameters)
    {
        MethodInfo targetMethod = RequiredMethod(ModType(patchName), "TargetMethod", Type.EmptyTypes);
        MethodInfo expected = RequiredMethod(expectedOwner, expectedName, parameters);
        Check(Equals(targetMethod.Invoke(null, null), expected),
            $"ENC123: {patchName} targets the native {expectedOwner.Name}.{expectedName} method.");
    }

    private static Type ModType(string name) => ModAssembly.GetType($"STS2Philosophers.{name}", throwOnError: true)!;

    private static MethodInfo RequiredMethod(Type owner, string name, Type[] parameters) =>
        owner.GetMethod(name, StaticMethods | BindingFlags.Instance, null, parameters, null)
        ?? throw new MissingMethodException(owner.FullName, name);

    private static IEnumerable<(OpCode Opcode, MethodBase Target)> ReferencesIncludingAsyncBody(MethodInfo method)
    {
        foreach (var reference in MethodReferences(method)) yield return reference;
        Type? stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachine is null) yield break;
        MethodInfo moveNext = RequiredMethod(stateMachine, "MoveNext", Type.EmptyTypes);
        foreach (var reference in MethodReferences(moveNext)) yield return reference;
    }

    // Parse instructions from the assembly loaded by this test process. Searching
    // raw bytes could mistake an operand for a call; no cached IL text is used.
    private static IEnumerable<(OpCode Opcode, MethodBase Target)> MethodReferences(MethodBase method)
    {
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new InvalidOperationException($"Native IL is unavailable for {method}.");
        for (int position = 0; position < bytes.Length;)
        {
            short value = bytes[position++];
            if (value == 0xfe) value = unchecked((short)(0xfe00 | bytes[position++]));
            OpCode opcode = Opcodes[value];
            int operandSize = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, position),
                _ => 4
            };
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                MemberInfo? target = method.Module.ResolveMember(BitConverter.ToInt32(bytes, position),
                    method.DeclaringType?.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (target is MethodBase member) yield return (opcode, member);
            }
            position += operandSize;
        }
    }
}
