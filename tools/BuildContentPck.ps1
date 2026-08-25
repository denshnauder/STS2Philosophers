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
