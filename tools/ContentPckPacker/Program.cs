using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StS2PckPacker;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: ContentPckPacker <contentRoot> <outputPck>");
    return 2;
}

string contentRoot = Path.GetFullPath(args[0]);
string outputPck = Path.GetFullPath(args[1]);
string scriptPath = Path.Combine(contentRoot, "BuildContentPck.gd");
string script = File.ReadAllText(scriptPath);

string Section(string start, string end)
{
    int a = script.IndexOf(start, StringComparison.Ordinal);
    if (a < 0) throw new InvalidOperationException($"missing section start: {start}");
    a += start.Length;
    int b = script.IndexOf(end, a, StringComparison.Ordinal);
    if (b < 0) throw new InvalidOperationException($"missing section end: {end}");
    return script[a..b];
}

List<string> ResPaths(string text) =>
    Regex.Matches(text, "\"(res://[^\"]+)\"")
        .Select(m => m.Groups[1].Value)
        .Distinct(StringComparer.Ordinal)
        .ToList();

var resourceEntries = Regex.Matches(
        Section("var resource_paths: Array[String] = [", "for resource_path in resource_paths:"),
        "\"([^\"]+)\"")
    .Select(m => "res://" + m.Groups[1].Value)
    .Distinct(StringComparer.Ordinal)
    .ToList();
var unsupportedResource = resourceEntries.FirstOrDefault(path =>
    !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
if (unsupportedResource is not null)
{
    Console.Error.WriteLine($"unsupported resource in BuildContentPck.gd, falling back to Godot: {unsupportedResource}");
    return 3;
}
var resourcePaths = resourceEntries;
var texturePaths = ResPaths(Section("var texture_paths: Array[String] = [", "for texture_path in texture_paths:"));
var unsupportedTexture = texturePaths.FirstOrDefault(path =>
    !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
    && !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));
if (unsupportedTexture is not null)
{
    Console.Error.WriteLine($"unsupported texture in BuildContentPck.gd, falling back to Godot: {unsupportedTexture}");
    return 3;
}
var unsupportedScriptPath = Regex.Matches(script, "\"(res://[^\"]+)\"")
    .Select(m => m.Groups[1].Value)
    .Distinct(StringComparer.Ordinal)
    .FirstOrDefault(path => Path.GetExtension(path).ToLowerInvariant() is
        ".tscn" or ".escn" or ".gd" or ".gdshader" or ".tres" or ".res" or ".material" or ".shader");
if (unsupportedScriptPath is not null)
{
    Console.Error.WriteLine($"unsupported packed resource, falling back to Godot: {unsupportedScriptPath}");
    return 3;
}
var aliasSection = Section("for alias in [", "# These events use");
var aliases = Regex.Matches(aliasSection, "\\[\\s*\"(res://[^\"]+)\"\\s*,\\s*\"(res://[^\"]+)\"\\s*,?\\s*\\]")
    .Select(m => (Alias: m.Groups[1].Value, Source: m.Groups[2].Value))
    .ToList();
var nameSection = Section("for name in [", "error = packer.flush()");
var portraitNames = Regex.Matches(nameSection, "\"([a-z0-9_]+)\"")
    .Select(m => m.Groups[1].Value)
    .ToList();

var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
var aliasSources = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var (alias, source) in aliases)
    if (!alias.EndsWith(".import", StringComparison.Ordinal))
        aliasSources[alias] = source;

void Add(string virtualPath, byte[] data)
{
    string key = virtualPath.StartsWith("res://", StringComparison.Ordinal) ? virtualPath[6..] : virtualPath;
    if (entries.TryGetValue(key, out byte[]? existing))
    {
        if (!existing.AsSpan().SequenceEqual(data))
            throw new InvalidOperationException($"conflicting duplicate PCK entry: {key}");
        return;
    }
    entries[key] = data;
}

string Full(string virtualPath) => Path.Combine(contentRoot, virtualPath[6..]);

string ImportText(string virtualPath)
{
    string path = Full(virtualPath) + ".import";
    if (!File.Exists(path)) throw new FileNotFoundException(path);
    return File.ReadAllText(path);
}

string RemapPath(string importText)
{
    var m = Regex.Match(importText, "path=\"(res://[^\"]+\\.ctex)\"");
    if (!m.Success) throw new InvalidOperationException("import has no ctex remap path");
    return m.Groups[1].Value;
}

string NewCtexPath(string virtualSource)
{
    string name = Path.GetFileName(virtualSource);
    string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(virtualSource))).ToLowerInvariant();
    return $"res://STS2Philosophers/imported/{name}-{hash}.ctex";
}

string RewriteImport(string importText, string newCtex, string? sourceFile = null, bool stripUid = false)
{
    string text = Regex.Replace(importText, "path=\"res://[^\"]+\\.ctex\"", $"path=\"{newCtex}\"");
    if (sourceFile is not null)
        text = Regex.Replace(text, "source_file=\"res://[^\"]+\"", $"source_file=\"{sourceFile}\"");
    if (stripUid)
        text = Regex.Replace(text, "(?m)^uid=.*\\n?", "");
    return text;
}

foreach (string resourcePath in resourcePaths)
{
    string resourceFull = Full(resourcePath);
    if (!File.Exists(resourceFull))
    {
        Console.Error.WriteLine($"missing resource, falling back to Godot: {resourceFull}");
        return 3;
    }
    Add(resourcePath, File.ReadAllBytes(resourceFull));
}

var textureCtex = new Dictionary<string, string>(StringComparer.Ordinal);

foreach (string texturePath in texturePaths)
{
    if (!File.Exists(Full(texturePath)))
    {
        Console.Error.WriteLine($"missing texture source, falling back to Godot: {Full(texturePath)}");
        return 3;
    }
    if (!File.Exists(Full(texturePath) + ".import"))
    {
        Console.Error.WriteLine($"missing texture import metadata, falling back to Godot: {Full(texturePath)}.import");
        return 3;
    }
    string importText = ImportText(texturePath);
    string oldCtex = RemapPath(importText);
    string newCtex = NewCtexPath(texturePath);
    byte[] ctex;
    if (texturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            ctex = CtexConverter.Convert(File.ReadAllBytes(Full(texturePath)));
        }
        catch (Exception error)
        {
            // The Godot importer may still handle an image the fast converter rejects.
            Console.Error.WriteLine($"PNG conversion failed, falling back to Godot: {texturePath}: {error.Message}");
            return 3;
        }
    }
    else
    {
        string oldCtexFull = Full(oldCtex);
        if (!File.Exists(oldCtexFull))
        {
            Console.Error.WriteLine($"missing imported texture, falling back to Godot: {oldCtexFull}");
            return 3;
        }
        // The fast path may reuse SVG import caches, but never a stale one.
        // Godot's .md5 sidecar records the source hash that produced the ctex.
        string md5Path = oldCtexFull[..^".ctex".Length] + ".md5";
        if (!File.Exists(md5Path))
        {
            Console.Error.WriteLine($"missing import hash, falling back to Godot: {md5Path}");
            return 3;
        }
        string md5Text = File.ReadAllText(md5Path);
        var sourceMatch = Regex.Match(md5Text, "source_md5=\"([0-9a-fA-F]+)\"");
        var destMatch = Regex.Match(md5Text, "dest_md5=\"([0-9a-fA-F]+)\"");
        string actualSourceMd5 = Convert.ToHexString(
            MD5.HashData(File.ReadAllBytes(Full(texturePath)))).ToLowerInvariant();
        byte[] cachedCtex = File.ReadAllBytes(oldCtexFull);
        string actualDestMd5 = Convert.ToHexString(MD5.HashData(cachedCtex)).ToLowerInvariant();
        if (!sourceMatch.Success
            || !string.Equals(sourceMatch.Groups[1].Value, actualSourceMd5, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"SVG import cache is stale, falling back to Godot: {texturePath}");
            return 3;
        }
        if (!destMatch.Success
            || !string.Equals(destMatch.Groups[1].Value, actualDestMd5, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"SVG import cache is corrupt, falling back to Godot: {texturePath}");
            return 3;
        }
        ctex = cachedCtex;
    }
    textureCtex[texturePath] = newCtex;
    Add(texturePath, File.ReadAllBytes(Full(texturePath)));
    Add(texturePath + ".import", Encoding.UTF8.GetBytes(RewriteImport(importText, newCtex)));
    Add(newCtex, ctex);
}

void AddAlias(string aliasPath, string sourcePath)
{
    if (aliasPath.EndsWith(".import", StringComparison.Ordinal))
    {
        string aliasTexture = aliasPath[..^".import".Length];
        string sourceTexture = aliasSources.TryGetValue(aliasTexture, out string? mapped)
            ? mapped : sourcePath[..^".import".Length];
        if (!textureCtex.TryGetValue(sourceTexture, out string? aliasCtex))
            throw new InvalidOperationException($"alias import source is not packed: {sourceTexture}");
        string aliasImport = ImportText(sourceTexture);
        Add(aliasPath, Encoding.UTF8.GetBytes(
            RewriteImport(aliasImport, aliasCtex, aliasTexture, stripUid: true)));
        return;
    }

    if (!textureCtex.TryGetValue(sourcePath, out string? ctex))
        throw new InvalidOperationException($"alias source is not packed: {sourcePath}");
    string sourceImport = ImportText(sourcePath);
    Add(aliasPath, File.ReadAllBytes(Full(sourcePath)));
    Add(aliasPath + ".import", Encoding.UTF8.GetBytes(
        RewriteImport(sourceImport, ctex, aliasPath, stripUid: true)));
}

foreach (var (alias, source) in aliases)
{
    string sourceTexture = source;
    if (source.EndsWith(".import", StringComparison.Ordinal))
    {
        string aliasTexture = alias[..^".import".Length];
        sourceTexture = aliasSources.TryGetValue(aliasTexture, out string? mapped)
            ? mapped : source[..^".import".Length];
    }
    string sourceFull = Full(sourceTexture);
    if (!File.Exists(sourceFull))
    {
        Console.Error.WriteLine($"missing alias source, falling back to Godot: {sourceFull}");
        return 3;
    }
    AddAlias(alias, source);
}
string portraitSourceFull = Full("res://images/events/philosophers_gaze.png");
if (!File.Exists(portraitSourceFull))
{
    Console.Error.WriteLine($"missing portrait alias source, falling back to Godot: {portraitSourceFull}");
    return 3;
}
foreach (string name in portraitNames)
    AddAlias($"res://images/events/{name}.png", "res://images/events/philosophers_gaze.png");

var pckEntries = entries.Select(kv => new PckFileEntry(kv.Key, kv.Value)).ToArray();
string temporaryOutput = outputPck + ".tmp";
try
{
    using (var stream = File.Create(temporaryOutput))
        PckWriter.Write(stream, pckEntries);
    File.Move(temporaryOutput, outputPck, overwrite: true);
}
catch
{
    if (File.Exists(temporaryOutput)) File.Delete(temporaryOutput);
    throw;
}
Console.WriteLine($"Packed {pckEntries.Length} entries -> {outputPck}");
Console.WriteLine($"resource={resourcePaths.Count} texture={texturePaths.Count} alias={aliases.Count} portrait={portraitNames.Count}");
return 0;
