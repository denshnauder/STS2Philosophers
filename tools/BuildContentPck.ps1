param(
    [Parameter(Mandatory = $true)]
    [string]$GodotExecutable,

    [Parameter(Mandatory = $true)]
    [string]$DotNetRoot,

    [Parameter(Mandatory = $true)]
    [string]$ProjectDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = $DotNetRoot
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:PATH = "$DotNetRoot;$env:PATH"

$packerProject = Join-Path $PSScriptRoot 'ContentPckPacker\ContentPckPacker.csproj'
& (Join-Path $DotNetRoot 'dotnet.exe') run --project $packerProject -c Release -- $ProjectDirectory $OutputPath
$packerExit = $LASTEXITCODE
if ($packerExit -eq 0)
{
    Write-Host "Content PCK built by fast packer: $OutputPath"
    return
}
if ($packerExit -ne 3)
{
    throw "ContentPckPacker failed with exit code $packerExit."
}
Write-Host "Fast packer requested Godot fallback."

& $GodotExecutable `
    --headless `
    --editor `
    --path $ProjectDirectory `
    --import `
    --quit

if ($LASTEXITCODE -ne 0)
{
    throw "Godot content import failed with exit code $LASTEXITCODE."
}

& $GodotExecutable `
    --headless `
    --path $ProjectDirectory `
    --script (Join-Path $ProjectDirectory 'BuildContentPck.gd') `
    -- `
    $OutputPath

if ($LASTEXITCODE -ne 0)
{
    throw "Godot content pack export failed with exit code $LASTEXITCODE."
}
