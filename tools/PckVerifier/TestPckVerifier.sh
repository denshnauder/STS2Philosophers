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

cp -R "$repo_dir/content/." "$tmp/missing-file"
rm "$tmp/missing-file/STS2Philosophers/localization/eng/relics.json"
python3 - "$tmp/missing-file/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = '\t\t"STS2Philosophers/localization/eng/relics.json",\n'
assert needle in text
open(path, "w", encoding="utf-8").write(text.replace(needle, "", 1))
PY
pack "$tmp/missing-file" "$tmp/missing-file.pck"
expect_verify 4 "$tmp/missing-file" "$tmp/missing-file.pck" "$tmp/missing-file.log" "missing required localization file"
grep -q "Missing packed file" "$tmp/missing-file.log" || fail "verifier did not report the missing file"

cp -R "$repo_dir/content/." "$tmp/atomic-content"
printf '{not-json' > "$tmp/atomic-content/STS2Philosophers/localization/eng/relics.json"
printf 'OLD-OUTPUT' > "$tmp/atomic.pck"
set +e
CONTENT_DIR="$tmp/atomic-content" "$repo_dir/tools/BuildContentPck.sh" "$tmp/atomic.pck" >"$tmp/atomic.log" 2>&1
atomic_status=$?
set -e
[ "$atomic_status" -eq 6 ] || { cat "$tmp/atomic.log" >&2; fail "atomic build expected 6, got $atomic_status"; }
[ "$(cat "$tmp/atomic.pck")" = "OLD-OUTPUT" ] || fail "failed atomic build replaced the old PCK"
if find "$tmp" -maxdepth 1 -name 'atomic.pck.tmp.*' -print -quit | grep -q .; then
  fail "failed atomic build left a temporary PCK"
fi
echo "PASS: failed build keeps old PCK and removes temporary output"

echo "PckVerifier checks passed."
