using System.Collections;
using System.Reflection;
using System.Text.Json;

internal static class EventPageLocalizationChecks
{
    private static readonly string[] Events =
    [
        "HUSSERL_EPOCHE", "HEIDEGGER_DASEIN", "SARTRE_EXISTENCE", "MERLEAU_PONTY_BODY",
        "FREGE_SENSE", "RUSSELL_ANALYSIS", "WITTGENSTEIN_GAME", "SAUSSURE_SIGN",
        "LEVI_STRAUSS_MYTH", "FOUCAULT_POWER", "DERRIDA_DECONSTRUCTION", "DELEUZE_DIFFERENCE",
        "GADAMER_FUSION", "HORKHEIMER_REASON",
    ];

    internal static void Run()
    {
        Dictionary<string, string> eng = Load("eng");
        Dictionary<string, string> zhs = Load("zhs");
        foreach (string root in Events)
        {
            string prefix = Pascal(root);
            Type stageType = Type.GetType($"STS2Philosophers.{prefix}Stage", throwOnError: true)!;
            Type flowType = Type.GetType($"STS2Philosophers.{prefix}Flow", throwOnError: true)!;
            MethodInfo options = flowType.GetMethod("Options", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{prefix}Flow.Options is missing.");
            MethodInfo descriptionKey = flowType.GetMethod("DescriptionKey", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{prefix}Flow.DescriptionKey is missing.");
            Type stateType = Type.GetType($"STS2Philosophers.{prefix}State", throwOnError: true)!;
            object sample = stateType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [$"{prefix}:LOCALIZATION:1"])!;
            PropertyInfo stageProperty = stateType.GetProperty("Stage")!;
            foreach (object stage in Enum.GetValues(stageType))
            {
                string page = UpperSnake(stage.ToString()!);
                stageProperty.SetValue(sample, stage);
                if ((string)descriptionKey.Invoke(null, [sample])! != page)
                    throw new InvalidOperationException($"{prefix} result page key must match its stage: {page}");
                Require(eng, zhs, $"{root}.pages.{page}.description");
                foreach (object action in (IEnumerable)options.Invoke(null, [stage])!)
                {
                    if (action.ToString() == "None") continue;
                    string option = $"{root}.pages.{page}.options.{UpperSnake(action.ToString()!)}";
                    Require(eng, zhs, $"{option}.title");
                    Require(eng, zhs, $"{option}.description");
                }
            }
        }
        Console.WriteLine("Event page localization checks passed.");
    }

    private static void Require(Dictionary<string, string> eng, Dictionary<string, string> zhs, string key)
    {
        if (!eng.ContainsKey(key)) throw new InvalidOperationException($"Missing English page key: {key}");
        if (!zhs.ContainsKey(key)) throw new InvalidOperationException($"Missing Chinese page key: {key}");
    }

    private static Dictionary<string, string> Load(string language)
    {
        string path = Path.Combine("content", "STS2Philosophers", "localization", language, "events.json");
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Unreadable {language} localization file.");
    }

    private static string Pascal(string upperSnake) =>
        string.Concat(upperSnake.Split('_').Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static string UpperSnake(string name)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) builder.Append('_');
            builder.Append(char.ToUpperInvariant(name[i]));
        }
        return builder.ToString();
    }
}
