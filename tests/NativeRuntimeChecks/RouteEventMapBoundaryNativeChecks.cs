using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
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

    public static async Task Run()
    {
        FieldInfo formatter = typeof(LocManager).GetField("_smartFormatter", BindingFlags.NonPublic | BindingFlags.Static)!;
        PropertyInfo defaultFormatter = formatter.FieldType.Assembly.GetType("SmartFormat.Smart")!
            .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
        object? priorFormatter = formatter.GetValue(null);
        object? priorDefault = defaultFormatter.GetValue(null);
        try { await RunCore(); }
        finally
        {
            formatter.SetValue(null, priorFormatter);
            defaultFormatter.SetValue(null, priorDefault);
        }
    }

    private static async Task RunCore()
    {
        InitializeLocalization();
        Type boundary = ModType("RouteEventMapBoundary");
        MethodInfo registered = RequiredMethod(boundary, "IsRegisteredEvent", [typeof(EventModel)]);
        MethodInfo matches = RequiredMethod(boundary, "MatchesSavedRoom", [typeof(SerializableRun), typeof(EventRoom)]);
        bool IsRegistered(EventModel? model) => (bool)registered.Invoke(null, [model])!;

        Type[] eventTypes =
        [
            typeof(DemocritusArrangement), typeof(EmpedoclesRootsInquiry), typeof(AnaxagorasMixtureInquiry),
            typeof(GorgiasSpeechInquiry), typeof(SenecaDailyReview), typeof(EpictetusGuidePractice),
            typeof(MarcusAureliusCampPractice),
            typeof(PyrrhoReportedCrossing), typeof(AnselmUnfinishedProof),
            typeof(ParmenidesInquiry), typeof(AnaximanderOriginInquiry),
            typeof(AnaximenesBreathInvitation), typeof(AristotleCivicInquiry), typeof(AugustinePromise),
            typeof(PlatoWholeArrangement), typeof(ProtagorasSharedDeliberation), typeof(EpicurusSharedMargin),
            typeof(PlotinusCommonGround), typeof(AquinasBridgeNotice), typeof(OckhamNamesInquiry), typeof(MachiavelliFloodPreparation), typeof(HobbesAuthorizedSovereignty), typeof(LockeCargoIdentity), typeof(HumeInduction), typeof(WollstonecraftAdmission), typeof(KantApperception), typeof(RousseauConsent), typeof(KantUniversalization), typeof(MillHarm), typeof(LutherSignedCare), typeof(BaconSampleLedger), typeof(CopernicusShiftedCenter),
            typeof(ZenoCivicChallenge),
            typeof(SextusOpposedAppearances), typeof(DescartesMethodicReconstruction), typeof(HanFeiPublicStandard)
        ];
        Check(!IsRegistered(null), "ENC123: a missing event is not registered for the map boundary.");
        foreach (Type eventType in eventTypes)
        {
            ModelDb.Inject(eventType);
            EventModel canonical = ModelDb.GetById<EventModel>(ModelDb.GetId(eventType));
            Check(IsRegistered(canonical), $"ENC123: {eventType.Name} is registered for the shared map boundary.");
            CheckNativeCheckpoint(canonical, matches);
            CheckLockedEntry(canonical);
            await CheckRejectedChoices(canonical);
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
        CheckNativeStartHook();
        CheckNativeOverlayCleanup();
        CheckPatchTarget("RouteEventMapBoundaryPatch", typeof(NMapScreen), nameof(NMapScreen.Open), [typeof(bool)]);
        CheckPatchTarget("RouteEventMapBoundaryRewardsProceedPatch", typeof(RunManager),
            nameof(RunManager.ProceedFromTerminalRewardsScreen), Type.EmptyTypes);
        Console.WriteLine($"Native map boundary checks passed: {eventTypes.Length} registered events, locked initial choices, stale callbacks and blocked pending recovery, native awaited start hook, exact native room round-trips and checkpoint rejection, explicit/no-run save prefix boundaries, rewards mounting/skip gates, current DLL rewards-to-map calls, and both shared patch targets; no rendered UI or actual run save.");
    }

    private static void InitializeLocalization()
    {
        // Only the native table lookup is needed to construct real EventOptions;
        // avoid LocManager.Initialize(), which loads game assets and settings.
        string path = Path.Combine(Directory.GetCurrentDirectory(), "content", "STS2Philosophers",
            "localization", "eng", "events.json");
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        var table = (LocTable)Activator.CreateInstance(typeof(LocTable),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            ["events", translations, null], null)!;
        var manager = (LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
        typeof(LocManager).GetMethod("LoadLocFormatters", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, null);
        var tables = new Dictionary<string, LocTable> { ["events"] = table };
        Set(manager, "_tables", tables);
        Set(manager, "_engTables", tables);
        typeof(LocManager).GetProperty(nameof(LocManager.Instance),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(null, manager);
    }

    private static void CheckLockedEntry(EventModel canonical)
    {
        EventModel local = canonical.ToMutable();
        MethodInfo generate = RequiredMethod(local.GetType(), "GenerateInitialOptions", Type.EmptyTypes);
        IReadOnlyList<EventOption> options = (IReadOnlyList<EventOption>)generate.Invoke(local, null)!;
        Check(options.Count == 1 && options[0].IsLocked && !local.IsFinished,
            $"ENC123: {local.GetType().Name} stays open with disabled choices before a run or persistence confirmation exists.");
        MethodInfo started = RequiredMethod(local.GetType(), nameof(EventModel.AfterEventStarted), Type.EmptyTypes);
        Check(started.DeclaringType == local.GetType(),
            $"ENC123: {local.GetType().Name} uses its native start hook for awaited restoration.");
    }

    private static void CheckNativeStartHook()
    {
        MethodInfo enter = RequiredMethod(typeof(EventRoom), nameof(EventRoom.EnterInternal),
            [typeof(IRunState), typeof(bool)]);
        var references = ReferencesIncludingAsyncBody(enter).ToArray();
        int start = Array.FindIndex(references, reference =>
            reference.Target.DeclaringType == typeof(EventModel)
            && reference.Target.Name == nameof(EventModel.AfterEventStarted));
        Check(start >= 0 && start + 1 < references.Length
            && references[start + 1].Target.DeclaringType == typeof(Task)
            && references[start + 1].Target.Name == nameof(Task.GetAwaiter),
            "ENC123: the current native EventRoom obtains an awaiter for AfterEventStarted before entry completes.");
    }

    private static void CheckNativeOverlayCleanup()
    {
        MethodInfo nativeEntry = RequiredMethod(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom),
            [typeof(AbstractRoom), typeof(bool)]);
        Type nativeMachine = nativeEntry.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        NativeInstruction[] instructions = MethodInstructions(RequiredMethod(nativeMachine, "MoveNext", Type.EmptyTypes)).ToArray();
        int clear = Array.FindIndex(instructions, instruction => instruction.Member is MethodBase method
            && method.DeclaringType == typeof(RunManager) && method.Name == "ClearScreens");
        int fade = clear < 0 ? -1 : Array.FindLastIndex(instructions, clear, clear + 1, instruction =>
            instruction.Member is FieldInfo field && field.Name == "fadeToBlack");
        Check(clear >= 0 && fade >= 0 && fade + 1 < clear
            && (instructions[fade + 1].Opcode == OpCodes.Brfalse || instructions[fade + 1].Opcode == OpCodes.Brfalse_S)
            && instructions[fade + 1].BranchTarget > instructions[clear].Offset,
            "ENC123: native nested-room entry skips ClearScreens when fadeToBlack is false.");

        MethodInfo routeEntry = RequiredMethod(ModType("RouteEventMapBoundary"), "EnterAsync",
            [typeof(RunManager), typeof(RunState), typeof(EventModel)]);
        var calls = ReferencesIncludingAsyncBody(routeEntry).Where(reference => Equals(reference.Target, nativeEntry)).ToArray();
        Check(calls.Length == 1 && calls[0].PreviousOpcode == OpCodes.Ldc_I4_1,
            "ENC123: the actual shared route entry passes true to native cleanup so a terminal reward overlay cannot cover the new event.");
    }

    private static async Task CheckRejectedChoices(EventModel canonical)
    {
        Type eventType = canonical.GetType();
        Type stateType = ModType(eventType.Name + "State");
        Type flowType = ModType(eventType.Name + "Flow");
        MethodInfo? create = eventType == typeof(SenecaDailyReview) || eventType == typeof(EpictetusGuidePractice)
            || eventType == typeof(MarcusAureliusCampPractice) || eventType == typeof(PyrrhoReportedCrossing)
            || eventType == typeof(AnselmUnfinishedProof) || eventType == typeof(OckhamNamesInquiry) || eventType == typeof(LutherSignedCare) || eventType == typeof(BaconSampleLedger) || eventType == typeof(CopernicusShiftedCenter) ? null
            : RequiredMethod(stateType, "Create", [typeof(string)]);
        MethodInfo generate = RequiredMethod(eventType, "GenerateOptions", [stateType]);
        PropertyInfo instance = stateType.GetProperty("InstanceId")!;
        PropertyInfo stage = stateType.GetProperty("Stage")!;
        PropertyInfo confirmed = stateType.GetProperty("ConfirmedActions")!;
        PropertyInfo pending = stateType.GetProperty("PendingAction")!;
        object NewState(string id)
        {
            if (eventType == typeof(CopernicusShiftedCenter))
            {
                Type forkType = stateType.GetProperty("Fork")!.PropertyType;
                object fork = JsonSerializer.Deserialize("""{"RunSeed":"42","ActIndex":1,"SourceRow":2,"SourceColumn":1,"SourceRoomId":10,"VisitedCount":3,"HistoryCount":3,"Exits":[{"Row":3,"Column":0,"PointType":"Monster"},{"Row":3,"Column":2,"PointType":"RestSite"}]}""", forkType)!;
                return RequiredMethod(stateType, "Create", [typeof(string), typeof(string), typeof(string), forkType])
                    .Invoke(null, [id, "DEMOCRITUS:NATIVE:2", "EMPEDOCLES:NATIVE:2", fork])!;
            }
            if (eventType == typeof(BaconSampleLedger))
                return RequiredMethod(stateType, "Create", [typeof(string), typeof(string)]).Invoke(null, [id, "OCKHAM:NATIVE:2"])!;
            if (create is not null) return create.Invoke(null, [id])!;
            object review = Activator.CreateInstance(stateType, nonPublic: true)!;
            stateType.GetProperty("Version")!.SetValue(review, 1);
            instance.SetValue(review, id);
            stateType.GetProperty("RunId")?.SetValue(review, "DIOGENES_SOCRATES_42");
            PropertyInfo? outcome = stateType.GetProperty("Outcome") ?? stateType.GetProperty("ZenoOutcome");
            if (outcome is not null)
                outcome.SetValue(review, Enum.GetValues(outcome.PropertyType).GetValue(0));
            stateType.GetProperty("SenecaInstanceId")?.SetValue(review, "SENECA_DAILY_REVIEW:42:1");
            stateType.GetProperty("EpictetusInstanceId")?.SetValue(review, "EPICTETUS_GUIDE_PRACTICE:42:1");
            stateType.GetProperty("SextusInstanceId")?.SetValue(review, "SEXTUS_OPPOSED_APPEARANCES:42:1");
            stateType.GetProperty("DescartesInstanceId")?.SetValue(review, "DESCARTES_METHODIC_RECONSTRUCTION:42:2");
            stateType.GetProperty("AugustineInstanceId")?.SetValue(review, "AUGUSTINE_PROMISE:42:2");
            stateType.GetProperty("GorgiasInstanceId")?.SetValue(review, "GORGIAS_SPEECH_INQUIRY:42:1");
            stateType.GetProperty("PyrrhoInstanceId")?.SetValue(review, "PYRRHO_REPORTED_CROSSING:42:2");
            stateType.GetProperty("QuestionSlotReserved")?.SetValue(review, true);
            stage.SetValue(review, Enum.GetValues(stage.PropertyType).GetValue(0));
            pending.SetValue(review, Enum.GetValues(pending.PropertyType).GetValue(0));
            confirmed.SetValue(review, Activator.CreateInstance(confirmed.PropertyType));
            stateType.GetProperty("Closed")!.SetValue(review, false);
            return review;
        }
        string Snapshot(object value) => JsonSerializer.Serialize(value, stateType);
        Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption)
            .GetProperty("OnChosen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(option);

        foreach (string mismatch in new[] { "instance", "stage", "action count" })
        {
            EventModel local = canonical.ToMutable();
            object state = NewState("native-callback-check");
            IReadOnlyList<EventOption> options = (IReadOnlyList<EventOption>)generate.Invoke(local, [state])!;
            Check(options.Count > 0 && Callback(options[0]) is not null,
                $"ENC123: {eventType.Name} supplies a real choice callback.");
            if (mismatch == "instance") instance.SetValue(state, "another-instance");
            else if (mismatch == "stage") stage.SetValue(state, Enum.GetValues(stage.PropertyType).GetValue(1));
            else ((System.Collections.IList)confirmed.GetValue(state)!).Add(Enum.GetValues(pending.PropertyType).GetValue(0));
            Set(local, "_ready", true);
            string before = Snapshot(state);
            await Callback(options[0])!();
            Check(Snapshot(state) == before && (bool)eventType.GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(local)!,
                $"ENC123: {eventType.Name} rejects an obsolete {mismatch} without changing the saved choice or a ready event.");
        }

        EventModel blocked = canonical.ToMutable();
        object pendingState = NewState("native-blocked-check");
        MethodInfo optionsForStage = RequiredMethod(flowType, "Options", [stage.PropertyType]);
        object firstAction = ((System.Collections.IEnumerable)optionsForStage.Invoke(null, [stage.GetValue(pendingState)])!)
            .Cast<object>().First();
        MethodInfo prepare = RequiredMethod(flowType, "TryPrepare", [stateType, pending.PropertyType]);
        Check((bool)prepare.Invoke(null, [pendingState, firstAction])!,
            $"ENC123: {eventType.Name} has a real prepared action to protect.");
        IReadOnlyList<EventOption> blockedOptions = (IReadOnlyList<EventOption>)generate.Invoke(blocked, [pendingState])!;
        Set(blocked, "_ready", false);
        Set(blocked, "_blocked", true);
        string pendingBefore = Snapshot(pendingState);
        await Callback(blockedOptions[0])!();
        await blocked.AfterEventStarted();
        Check(Snapshot(pendingState) == pendingBefore
            && !(bool)eventType.GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(blocked)!,
            $"ENC123: {eventType.Name} leaves a prepared choice unchanged and locked when callbacks or startup repeat after recovery was blocked.");
        if (eventType == typeof(CopernicusShiftedCenter))
        {
            object travel = NewState("native-travel-intention");
            MethodInfo apply = RequiredMethod(flowType, "Apply", [stateType, pending.PropertyType]);
            foreach (string action in new[] { "AcceptInvitation", "KeepOldTable", "RecordResidual", "FollowTable" })
                apply.Invoke(null, [travel, Enum.Parse(pending.PropertyType, action)]);
            EventModel lockedTravel = canonical.ToMutable();
            RequiredMethod(eventType, "ShowConfirmedState", [stateType]).Invoke(lockedTravel, [travel]);
            Check(!lockedTravel.IsFinished && lockedTravel.CurrentOptions.Count == 1
                && Callback(lockedTravel.CurrentOptions[0]) is null,
                "A saved travel intention keeps the native event unfinished and locked, so Proceed cannot bypass a failed travel's freeze.");
        }
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

    private static IEnumerable<(OpCode Opcode, MethodBase Target, OpCode PreviousOpcode)> ReferencesIncludingAsyncBody(MethodInfo method)
    {
        foreach (var reference in MethodReferences(method)) yield return reference;
        Type? stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachine is null) yield break;
        MethodInfo moveNext = RequiredMethod(stateMachine, "MoveNext", Type.EmptyTypes);
        foreach (var reference in MethodReferences(moveNext)) yield return reference;
    }

    // Parse instructions from the assembly loaded by this test process. Searching
    // raw bytes could mistake an operand for a call; no cached IL text is used.
    private static IEnumerable<(OpCode Opcode, MethodBase Target, OpCode PreviousOpcode)> MethodReferences(MethodBase method)
    {
        OpCode previous = default;
        foreach (NativeInstruction instruction in MethodInstructions(method))
        {
            if (instruction.Member is MethodBase target) yield return (instruction.Opcode, target, previous);
            previous = instruction.Opcode;
        }
    }

    private sealed record NativeInstruction(int Offset, OpCode Opcode, MemberInfo? Member, int? BranchTarget);

    private static IEnumerable<NativeInstruction> MethodInstructions(MethodBase method)
    {
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new InvalidOperationException($"Native IL is unavailable for {method}.");
        for (int position = 0; position < bytes.Length;)
        {
            int offset = position;
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
            MemberInfo? member = null;
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField)
            {
                member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, position),
                    method.DeclaringType?.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null);
            }
            int? branchTarget = opcode.OperandType switch
            {
                OperandType.ShortInlineBrTarget => position + 1 + (sbyte)bytes[position],
                OperandType.InlineBrTarget => position + 4 + BitConverter.ToInt32(bytes, position),
                _ => null
            };
            yield return new(offset, opcode, member, branchTarget);
            position += operandSize;
        }
    }
}
