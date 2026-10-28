#!/bin/bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [ -f "$repo_dir/.tools/Env.sh" ]; then
  # shellcheck disable=SC1091
  source "$repo_dir/.tools/Env.sh"
fi
dotnet_bin="${DOTNET:-dotnet}"
godot_bin="${GODOT:-Godot}"
packer_project="$repo_dir/tools/ContentPckPacker/ContentPckPacker.csproj"
verifier="$repo_dir/tools/PckVerifier/VerifyContentPck.gd"
tmp="$(mktemp -d "${TMPDIR:-/tmp}/pck-verifier-test.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
pack() {
  "$dotnet_bin" run --project "$packer_project" -c Release -- "$1" "$2" >"$tmp/pack.log" 2>&1 \
    || { cat "$tmp/pack.log" >&2; fail "packer failed for $1"; }
}
verify() {
  local content="$1" pck="$2" log="$3"
  "$godot_bin" --headless --path "$content" --script "$verifier" -- "$pck" >"$log" 2>&1
}
expect_verify() {
  local expected="$1" content="$2" pck="$3" log="$4" label="$5"
  set +e
  verify "$content" "$pck" "$log"
  local status=$?
  set -e
  if [ "$status" -ne "$expected" ]; then
    cat "$log" >&2
    fail "$label: expected verifier exit $expected, got $status"
  fi
  echo "PASS: $label (exit $status)"
}

pack "$repo_dir/content" "$tmp/baseline.pck"
expect_verify 0 "$repo_dir/content" "$tmp/baseline.pck" "$tmp/baseline.log" "baseline PCK JSON"

cp -R "$repo_dir/content/." "$tmp/content"
printf '{not-json' > "$tmp/content/STS2Philosophers/localization/eng/relics.json"
pack "$tmp/content" "$tmp/bad-json.pck"
expect_verify 6 "$tmp/content" "$tmp/bad-json.pck" "$tmp/bad-json.log" "invalid localization JSON"
grep -q "Packed JSON is invalid" "$tmp/bad-json.log" || fail "verifier did not report invalid JSON"

echo "PckVerifier checks passed."
