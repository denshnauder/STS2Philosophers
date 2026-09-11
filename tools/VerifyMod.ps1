param([switch]$SkipDeploy)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
$originalRoot = $env:DOTNET_ROOT
$originalRollForward = $env:DOTNET_ROLL_FORWARD
$originalPath = $env:PATH
Push-Location $repository
$outputDirectory = Join-Path $repository 'bin/Release/net9.0'
$reportPath = Join-Path $outputDirectory 'verification.md'
$stage = 'Configuration'
try {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    @('# Verification result', '', "Time: $(Get-Date -Format o)", 'Status: running; previous success is not valid for this attempt') |
        Set-Content -LiteralPath $reportPath -Encoding UTF8
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
    $initialStatus = @(& git status --short)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the working tree.' }
    # The deployed build includes working-tree changes, not only HEAD.
    $diffOutputOption = '--output=' + (Join-Path $outputDirectory 'verification_changes.patch')
    & git diff HEAD --binary $diffOutputOption
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record the tracked source changes.' }
    [xml]$settings = Get-Content -LiteralPath (Join-Path $repository 'local.props') -Raw
    $dotNetRoot = [string]$settings.Project.PropertyGroup.DotNetRoot
    $godot = [string]$settings.Project.PropertyGroup.GodotExecutable
    $gameDirectory = [string]$settings.Project.PropertyGroup.GameDirectory
    $dotnet = Join-Path $dotNetRoot 'dotnet.exe'
    foreach ($required in @($dotnet, $godot, (Join-Path $gameDirectory 'data_sts2_windows_x86_64/sts2.dll'))) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing configured file: $required" }
    }
    $env:DOTNET_ROOT = $dotNetRoot
    $env:DOTNET_ROLL_FORWARD = 'Major'
    $env:PATH = "$dotNetRoot;$originalPath"

    $stage = 'Logic checks'
    & $dotnet run --project ./tests/ModLogicChecks/ModLogicChecks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Logic checks failed: $LASTEXITCODE" }
    $stage = 'Release build'
    & $dotnet build ./STS2Philosophers.csproj -c Release -p:DeployOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw "Release build failed: $LASTEXITCODE" }

    $stage = 'PCK export'
    $pck = Join-Path $outputDirectory 'STS2Philosophers.pck'
    & (Join-Path $PSScriptRoot 'BuildContentPck.ps1') -GodotExecutable $godot -DotNetRoot $dotNetRoot -ProjectDirectory (Join-Path $repository 'content') -OutputPath $pck
    $stage = 'PCK verification'
    & $godot --headless --path (Join-Path $PSScriptRoot 'PckVerifier') --script (Join-Path $PSScriptRoot 'PckVerifier/VerifyContentPck.gd') -- $pck
    if ($LASTEXITCODE -ne 0) { throw "PCK verification failed: $LASTEXITCODE" }

    $sources = @((Join-Path $outputDirectory 'STS2Philosophers.dll'), $pck, (Join-Path $repository 'STS2Philosophers.json'))
    $stage = 'Deployment'
    $deployment = 'Skipped by request'
    $hashes = @($sources | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 })
    if (-not $SkipDeploy) {
        $running = @(Get-Process | Where-Object { $_.ProcessName -match '(?i)sts2|slay.*spire' })
        if ($running.Count -gt 0) {
            $deployment = 'Blocked: game is running'
            Write-Warning $deployment
        } else {
            $destination = Join-Path $gameDirectory 'mods/STS2Philosophers'
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            foreach ($source in $sources) {
                if (@(Get-Process | Where-Object { $_.ProcessName -match '(?i)sts2|slay.*spire' }).Count -gt 0) {
                    throw 'Game started during deployment. Close it and rerun verification before playing.'
                }
                $target = Join-Path $destination (Split-Path -Leaf $source)
                Copy-Item -LiteralPath $source -Destination $target -Force
                if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                    throw "Deployment hash mismatch: $target"
                }
            }
            $deployment = 'Deployed; all three SHA256 hashes match'
        }
    }
    $stage = 'Report'
    $report = @('# Verification result', '', "Time: $(Get-Date -Format o)", "Source base commit: $commit", 'Source: working tree; tracked changes recorded in verification_changes.patch; untracked contents are not archived', '', 'Logic checks: passed', 'Release build: passed', 'PCK load and textures: passed', "Deployment: $deployment", '', 'In-game acceptance: not performed by this script', '', '## Working tree before verification', '```', $initialStatus, '```', '', '## Working tree after verification', '```', (& git status --short), '```', '', '## SHA256')
    $report += $hashes | ForEach-Object { "$(Split-Path -Leaf $_.Path): $($_.Hash)" }
    $report | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Host "Verification passed. $deployment"
} catch {
    @('# Verification result', '', "Time: $(Get-Date -Format o)", 'Status: failed', "Stage: $stage", "Error: $($_.Exception.Message)", '', 'Deployment completion is not confirmed. Fix the failure and rerun before acceptance.') |
        Set-Content -LiteralPath $reportPath -Encoding UTF8
    throw
} finally {
    Pop-Location
    $env:DOTNET_ROOT = $originalRoot
    $env:DOTNET_ROLL_FORWARD = $originalRollForward
    $env:PATH = $originalPath
}
