#!/bin/bash
# Runs the native all-EventModel portrait check against a built content PCK.
# The check enumerates every concrete EventModel in the mod assembly, resolves
# its real InitialPortraitPath, loads the packed texture, and calls the game's
# own CreateInitialPortrait. A PCK that merely passes the JSON/texture verifier
# can still fail here when a new event's default portrait path is not packed.
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
pck="${1:?usage: VerifyEventPortraits.sh /path/to/STS2Philosophers.pck}"
pck="$(cd "$(dirname "$pck")" && pwd)/$(basename "$pck")"
[ -f "$pck" ] || { echo "PCK not found: $pck" >&2; exit 2; }

# shellcheck disable=SC1091
source "$repo_dir/.tools/Env.sh"
export DOTNET_ROLL_FORWARD=Major
dotnet_bin="${DOTNET:-dotnet}"

"$dotnet_bin" build "$repo_dir/tests/NativeRuntimeChecks/NativeRuntimeChecks.csproj" \
  -c Release -p:DeployOnBuild=false --configfile "$repo_dir/.tools/NuGet.Config"

checks_dir="$repo_dir/tests/NativeRuntimeChecks/bin/Release/net9.0"
"$GODOT" --headless --path "$repo_dir/tests/GodotRuntimeChecks" -- \
  "$checks_dir/NativeRuntimeChecks.dll" "$repo_dir" --portraits-only "$pck"
