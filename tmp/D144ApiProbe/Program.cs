using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

var gameDir = @"E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64";
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(gameDir, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
foreach (var path in Directory.EnumerateFiles(gameDir, "*.dll"))
{
    try { AssemblyLoadContext.Default.LoadFromAssemblyPath(path); } catch { }
}
var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDir, "sts2.dll"));
const BindingFlags declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
var one = new Dictionary<byte, OpCode>();
var two = new Dictionary<byte, OpCode>();
foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
{
    if (field.GetValue(null) is not OpCode op) continue;
    ushort value = (ushort)op.Value;
    if (value < 256) one[(byte)value] = op;
    else if ((value & 0xff00) == 0xfe00) two[(byte)(value & 0xff)] = op;
}

string Token(MethodBase method, int token)
{
    try
    {
        return method.Module.ResolveMember(
            token,
            method.DeclaringType?.GetGenericArguments(),
            method.IsGenericMethod ? method.GetGenericArguments() : null)?.ToString() ?? $"token 0x{token:x8}";
    }
    catch { return $"token 0x{token:x8}"; }
}

void DumpIl(MethodBase method)
{
    Console.WriteLine($"\nIL {method.DeclaringType?.FullName}.{method.Name}");
    var body = method.GetMethodBody();
    if (body is null) { Console.WriteLine("  <no body>"); return; }
    var bytes = body.GetILAsByteArray()!;
    int i = 0;
    while (i < bytes.Length)
    {
        int offset = i;
        OpCode op = bytes[i++] == 0xfe ? two[bytes[i++]] : one[bytes[i - 1]];
        object? value = null;
        int count = 0;
        switch (op.OperandType)
        {
            case OperandType.InlineNone: break;
            case OperandType.ShortInlineI: value = (sbyte)bytes[i]; count = 1; break;
            case OperandType.InlineI: value = BitConverter.ToInt32(bytes, i); count = 4; break;
            case OperandType.InlineI8: value = BitConverter.ToInt64(bytes, i); count = 8; break;
            case OperandType.ShortInlineR: value = BitConverter.ToSingle(bytes, i); count = 4; break;
            case OperandType.InlineR: value = BitConverter.ToDouble(bytes, i); count = 8; break;
            case OperandType.ShortInlineBrTarget: value = $"IL_{i + 1 + (sbyte)bytes[i]:x4}"; count = 1; break;
            case OperandType.InlineBrTarget: value = $"IL_{i + 4 + BitConverter.ToInt32(bytes, i):x4}"; count = 4; break;
            case OperandType.ShortInlineVar: value = bytes[i]; count = 1; break;
            case OperandType.InlineVar: value = BitConverter.ToUInt16(bytes, i); count = 2; break;
            case OperandType.InlineString:
                int stringToken = BitConverter.ToInt32(bytes, i);
                try { value = '"' + method.Module.ResolveString(stringToken) + '"'; }
                catch { value = $"string 0x{stringToken:x8}"; }
                count = 4;
                break;
            case OperandType.InlineField:
            case OperandType.InlineMethod:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            case OperandType.InlineSig:
                value = Token(method, BitConverter.ToInt32(bytes, i)); count = 4; break;
            case OperandType.InlineSwitch:
                int n = BitConverter.ToInt32(bytes, i);
                var targets = new string[n];
                for (int x = 0; x < n; x++) targets[x] = $"IL_{i + 4 + 4 * n + BitConverter.ToInt32(bytes, i + 4 + 4 * x):x4}";
                value = string.Join(',', targets); count = 4 + 4 * n; break;
        }
        i += count;
        Console.WriteLine($"  IL_{offset:x4}: {op.Name} {value}");
    }
}

Type TypeOf(string name) => asm.GetType(name) ?? throw new InvalidOperationException(name);
void DumpMembers(string name)
{
    var type = TypeOf(name);
    Console.WriteLine($"\nTYPE {type.FullName} : {type.BaseType?.FullName}");
    foreach (var property in type.GetProperties(declared).OrderBy(x => x.Name))
        Console.WriteLine($"  PROP {property.PropertyType.Name} {property.Name} get={property.GetMethod?.Attributes} set={property.SetMethod?.Attributes}");
    foreach (var field in type.GetFields(declared).OrderBy(x => x.Name))
        Console.WriteLine($"  FIELD {field.FieldType.Name} {field.Name} [{field.Attributes}]");
    foreach (var method in type.GetMethods(declared).OrderBy(x => x.Name))
        Console.WriteLine($"  METHOD {method.ReturnType.Name} {method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.Name))}) [{method.Attributes}]");
}

void Dump(string typeName, string methodName)
{
    var type = TypeOf(typeName);
    foreach (var method in type.GetMethods(declared).Where(x => x.Name == methodName)) DumpIl(method);
}

void DumpStateMachine(string ownerType, string methodName)
{
    var owner = TypeOf(ownerType);
    var method = owner.GetMethods(declared).Single(x => x.Name == methodName);
    DumpIl(method);
    var attr = method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>();
    if (attr is not null) DumpIl(attr.StateMachineType.GetMethod("MoveNext", declared)!);
}

foreach (var typeName in new[]
{
    "MegaCrit.Sts2.Core.Nodes.Events.NEventLayout",
    "MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl",
    "MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton",
    "MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom",
    "MegaCrit.Sts2.Core.Runs.RunManager",
    "MegaCrit.Sts2.Core.Runs.RunState",
    "MegaCrit.Sts2.Core.Rooms.EventRoom",
    "MegaCrit.Sts2.Core.Saves.Runs.SerializableRoom"
}) DumpMembers(typeName);

Dump("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "get_DefaultFocusedControl");
Dump("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "SetPortrait");
Dump("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "ClearOptions");
Dump("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "AddOption");
Dump("MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton", "_Input");
Dump("MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl", "_GuiInput");
Dump("MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", "SetOptions");
Dump("MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", "SetPortrait");
DumpStateMachine("MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", "SetupLayout");
DumpStateMachine("MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", "BeforeOptionChosen");
DumpStateMachine("MegaCrit.Sts2.Core.Runs.RunManager", "EnterRoomWithoutExitingCurrentRoom");
DumpStateMachine("MegaCrit.Sts2.Core.Runs.RunManager", "EnterRoomInternal");
Dump("MegaCrit.Sts2.Core.Runs.RunManager", "ToSave");
DumpStateMachine("MegaCrit.Sts2.Core.Runs.RunManager", "ExitCurrentRoom");
DumpStateMachine("MegaCrit.Sts2.Core.Runs.RunManager", "ExitCurrentRooms");
Dump("MegaCrit.Sts2.Core.Runs.RunState", "FromSerializable");
DumpStateMachine("MegaCrit.Sts2.Core.Rooms.EventRoom", "EnterInternal");
DumpStateMachine("MegaCrit.Sts2.Core.Rooms.EventRoom", "Exit");

Console.WriteLine("\nCANCEL-LIKE DECLARED METHODS");
foreach (var type in asm.GetTypes().Where(t => t.FullName?.Contains("Event", StringComparison.OrdinalIgnoreCase) == true))
foreach (var method in type.GetMethods(declared).Where(m => m.Name.Contains("Cancel", StringComparison.OrdinalIgnoreCase)))
    Console.WriteLine($"  {type.FullName}.{method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.Name))}) [{method.Attributes}]");
