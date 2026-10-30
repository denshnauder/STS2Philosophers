# Runs the same checks as VerifyMod.ps1 on a machine that is not the Windows
# game host: the delivery selection checks, the release build, the logic
# checks, the native runtime checks and the content pack build. Deployment,
# the pack load check and the portrait check need the game host and are not
# run here.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
Push-Location $repository
try {
    [xml]$settings = Get-Content -LiteralPath (Join-Path $repository 'local.props') -Raw
    $dotNetRoot = [string]$settings.Project.PropertyGroup.DotNetRoot
    $godot = [string]$settings.Project.PropertyGroup.GodotExecutable
    $dotnet = Join-Path $dotNetRoot $(if (Test-Path -LiteralPath (Join-Path $dotNetRoot 'dotnet.exe')) { 'dotnet.exe' } else { 'dotnet' })
    $env:DOTNET_ROOT = $dotNetRoot
    $env:DOTNET_ROLL_FORWARD = 'Major'

    $stage = 'Design and route table'
    & python3 (Join-Path $repository 'tools/CompareRouteTable.py')
    if ($LASTEXITCODE -ne 0) { throw "Design and route table disagree: $LASTEXITCODE" }

    $stage = 'Delivery selection checks'
    & pwsh -NoProfile -File (Join-Path $repository 'tests/DeliverySelectionChecks.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Delivery selection checks failed: $LASTEXITCODE" }

    $stage = 'Release build'
    & $dotnet build ./STS2Philosophers.csproj -c Release -p:DeployOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw "Release build failed: $LASTEXITCODE" }

    $stage = 'Logic checks'
    & $dotnet run --project ./tests/ModLogicChecks/ModLogicChecks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Logic checks failed: $LASTEXITCODE" }

    $stage = 'Native runtime checks'
    & (Join-Path $PSScriptRoot 'RunNativeRuntimeChecks.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Native runtime checks failed: $LASTEXITCODE" }

    $stage = 'Content PCK build'
    & (Join-Path $PSScriptRoot 'BuildContentPck.ps1') -GodotExecutable $godot -DotNetRoot $dotNetRoot `
        -ProjectDirectory (Join-Path $repository 'content') `
        -OutputPath (Join-Path $repository 'bin/Release/net9.0/STS2Philosophers.pck')
    if ($LASTEXITCODE -ne 0) { throw "Content PCK build failed: $LASTEXITCODE" }

    Write-Host "Local verification passed: $stage last."
} finally {
    Pop-Location
}
