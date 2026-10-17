using System.Reflection;
using System.Runtime.Loader;
using Godot;

// Godot owns native API initialization. The existing checks still execute their
// real STS2/Ritsu assemblies against in-memory fixtures, without booting STS2.
public partial class RuntimeCheckHost : Node
{
    public override async void _Ready()
    {
        int exitCode = 1;
        try
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            if (arguments.Length < 2)
                throw new ArgumentException("User arguments must start with the NativeRuntimeChecks assembly path and repository directory.");

            string assemblyPath = Path.GetFullPath(arguments[0]);
            // Godot changes the process directory to the test project. Existing
            // checks read repository resources using paths relative to its root.
            System.Environment.CurrentDirectory = Path.GetFullPath(arguments[1]);
            var context = AssemblyLoadContext.GetLoadContext(GetType().Assembly)!;
            var dependencies = new AssemblyDependencyResolver(assemblyPath);
            context.Resolving += (_, name) =>
            {
                string? path = dependencies.ResolveAssemblyToPath(name);
                return path is null ? null : context.LoadFromAssemblyPath(path);
            };

            Assembly checks = context.LoadFromAssemblyPath(assemblyPath);
            MethodInfo entry = checks.EntryPoint
                ?? throw new InvalidOperationException("NativeRuntimeChecks has no entry point.");
            object? result = entry.Invoke(null, entry.GetParameters().Length == 0
                ? null : [arguments.Skip(2).ToArray()]);
            if (result is Task task) await task;
            exitCode = result switch
            {
                Task<int> codeTask => codeTask.Result,
                int code => code,
                _ => 0,
            };
            foreach (Assembly framework in context.Assemblies.Where(assembly =>
                assembly.GetName().Name?.StartsWith("STS2-RitsuLib", StringComparison.Ordinal) == true))
            {
                string? version = framework.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                Console.WriteLine($"Ritsu assembly exercised: {framework.GetName().Name} {version} ({framework.Location})");
            }
            Console.WriteLine($"Godot runtime checks completed with exit code {exitCode}.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception is TargetInvocationException { InnerException: not null } invocation
                ? invocation.InnerException : exception);
        }
        GetTree().Quit(exitCode);
    }
}
