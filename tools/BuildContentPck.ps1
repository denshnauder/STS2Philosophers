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
$temporary = "$OutputPath.tmp.$PID"
try
{
    & (Join-Path $DotNetRoot 'dotnet.exe') run --project $packerProject -c Release -- $ProjectDirectory $temporary
    $packerExit = $LASTEXITCODE
    if ($packerExit -eq 0)
    {
        Write-Host "Content PCK built by fast packer, installing: $temporary"
    }
    elseif ($packerExit -eq 3)
    {
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
            $temporary

        if ($LASTEXITCODE -ne 0)
        {
            throw "Godot content pack export failed with exit code $LASTEXITCODE."
        }
    }
    else
    {
        throw "ContentPckPacker failed with exit code $packerExit."
    }

    Move-Item -LiteralPath $temporary -Destination $OutputPath -Force
    Write-Host "Content PCK installed at: $OutputPath"
}
finally
{
    if (Test-Path -LiteralPath $temporary)
    {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
    }
}
