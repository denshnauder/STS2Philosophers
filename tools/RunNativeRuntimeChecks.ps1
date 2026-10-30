param(
    [switch]$SkipNativeBuild,
    [string[]]$CheckArguments = @(),
    [string[]]$RitsuRuntimeDirectory = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
[xml]$settings = Get-Content -LiteralPath (Join-Path $repository 'local.props') -Raw
$dotNetRoot = [string]$settings.Project.PropertyGroup.DotNetRoot
$godot = [string]$settings.Project.PropertyGroup.GodotExecutable
$dotnet = Join-Path $dotNetRoot 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { $dotnet = Join-Path $dotNetRoot 'dotnet' }
foreach ($required in @($dotnet, $godot)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing configured file: $required" }
}
$hostDirectory = Join-Path $repository 'tests/GodotRuntimeChecks'
$nativeProject = Join-Path $repository 'tests/NativeRuntimeChecks/NativeRuntimeChecks.csproj'
$checks = Join-Path $repository 'tests/NativeRuntimeChecks/bin/Release/net9.0/NativeRuntimeChecks.dll'
$originalRoot = $env:DOTNET_ROOT
$originalPath = $env:PATH
$originalRollForward = $env:DOTNET_ROLL_FORWARD
$stageDirectory = $null
try {
    $env:DOTNET_ROOT = $dotNetRoot
    $env:PATH = "$dotNetRoot;$originalPath"
    $env:DOTNET_ROLL_FORWARD = 'Major'
    if (-not $SkipNativeBuild) {
        & $dotnet build $nativeProject -c Release -p:DeployOnBuild=false
        if ($LASTEXITCODE -ne 0) { throw "Native checks build failed: $LASTEXITCODE" }
    }
    if (-not (Test-Path -LiteralPath $checks -PathType Leaf)) { throw "Missing native checks assembly: $checks" }
    & $dotnet build (Join-Path $hostDirectory 'GodotRuntimeChecks.csproj') -c Debug
    if ($LASTEXITCODE -ne 0) { throw "Godot test host build failed: $LASTEXITCODE" }

    if ($RitsuRuntimeDirectory.Count -gt 0) {
        # Run the identical checks with another real Ritsu distribution without
        # changing NuGet outputs or the installed Mod. Pass its compatible facade
        # directory and its shared directory, never the workshop root loader.
        $stageRoot = [IO.Path]::GetFullPath((Join-Path $hostDirectory '.godot/RuntimeChecksStages'))
        $stageDirectory = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
        Copy-Item -Path (Join-Path (Split-Path -Parent $checks) '*') -Destination $stageDirectory -Recurse
        $replaced = @{}
        foreach ($directory in $RitsuRuntimeDirectory) {
            $runtimeFiles = @(Get-ChildItem -LiteralPath $directory -Filter 'STS2-RitsuLib*.dll' -File)
            if ($runtimeFiles.Count -eq 0) { throw "No Ritsu runtime assemblies in $directory" }
            foreach ($file in $runtimeFiles) {
                if ($replaced.ContainsKey($file.Name)) { throw "Ambiguous Ritsu runtime assembly: $($file.Name)" }
                $replaced[$file.Name] = $true
                Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $stageDirectory $file.Name) -Force
                Write-Host "Ritsu runtime override: $($file.FullName) ($($file.VersionInfo.FileVersion))"
            }
        }
        foreach ($name in @('STS2-RitsuLib.dll', 'STS2-RitsuLib.Runtime.dll', 'STS2-RitsuLib.Shared.dll', 'STS2-RitsuLib.Settings.dll', 'STS2-RitsuLib.Ui.dll')) {
            if (-not $replaced.ContainsKey($name)) { throw "A complete override must supply $name" }
        }
        $checks = Join-Path $stageDirectory 'NativeRuntimeChecks.dll'
    }

    # This starts only the isolated headless test scene, never the STS2 project or
    # an editor window. Its user directory is separate from the player's game.
    & $godot --headless --path $hostDirectory -- $checks $repository @CheckArguments
    if ($LASTEXITCODE -ne 0) { throw "Godot runtime checks failed: $LASTEXITCODE" }
}
finally {
    $env:DOTNET_ROOT = $originalRoot
    $env:PATH = $originalPath
    $env:DOTNET_ROLL_FORWARD = $originalRollForward
    if ($null -ne $stageDirectory -and (Test-Path -LiteralPath $stageDirectory)) {
        $resolvedStage = [IO.Path]::GetFullPath($stageDirectory)
        $allowedRoot = [IO.Path]::GetFullPath((Join-Path $hostDirectory '.godot/RuntimeChecksStages')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedStage.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a runtime stage outside the test output directory: $resolvedStage"
        }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
