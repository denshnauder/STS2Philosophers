#!/bin/bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${1:-$repo_dir/bin/Release/net9.0/STS2Philosophers.pck}"
temporary="${output}.tmp.$$"
trap 'rm -f "$temporary"' EXIT
content_dir="${CONTENT_DIR:-$repo_dir/content}"
if [ -f "$repo_dir/.tools/Env.sh" ]; then
  # shellcheck disable=SC1091
  source "$repo_dir/.tools/Env.sh"
fi
dotnet_bin="${DOTNET:-dotnet}"
godot_bin="${GODOT:-Godot}"
packer_project="$repo_dir/tools/ContentPckPacker/ContentPckPacker.csproj"

set +e
"$dotnet_bin" run --project "$packer_project" -c Release -- "$content_dir" "$temporary"
packer_status=$?
set -e

if [ "$packer_status" -eq 0 ]; then
  echo "Content PCK built by fast packer, verifying: $temporary"
elif [ "$packer_status" -eq 3 ]; then
  echo "Fast packer requested Godot fallback."
  "$godot_bin" --headless --editor --path "$content_dir" --import --quit
  "$godot_bin" --headless --path "$content_dir" --script "$content_dir/BuildContentPck.gd" -- "$temporary"
else
  echo "ContentPckPacker failed with exit code $packer_status." >&2
  exit "$packer_status"
fi

"$godot_bin" --headless --path "$content_dir" --script "$repo_dir/tools/PckVerifier/VerifyContentPck.gd" -- "$temporary"
mv -f "$temporary" "$output"
trap - EXIT
echo "Content PCK installed at: $output"
