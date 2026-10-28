#!/bin/bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [ -f "$repo_dir/.tools/Env.sh" ]; then
  # shellcheck disable=SC1091
  source "$repo_dir/.tools/Env.sh"
fi
dotnet_bin="${DOTNET:-dotnet}"
project="$repo_dir/tools/ContentPckPacker/ContentPckPacker.csproj"
dll="$repo_dir/tools/ContentPckPacker/bin/Release/net9.0/ContentPckPacker.dll"
tmp="$(mktemp -d "${TMPDIR:-/tmp}/content-pck-test.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
run() {
  local content="$1" output="$2" log="$3"
  "$dotnet_bin" "$dll" "$content" "$output" >"$log" 2>&1
}
expect_status() {
  local expected="$1" content="$2" output="$3" log="$4" label="$5"
  set +e
  run "$content" "$output" "$log"
  local status=$?
  set -e
  if [ "$status" -ne "$expected" ]; then
    cat "$log" >&2
    fail "$label: expected exit $expected, got $status"
  fi
  echo "PASS: $label (exit $status)"
}

echo "Building ContentPckPacker..."
"$dotnet_bin" build "$project" -c Release --nologo >/dev/null

expect_status 0 "$repo_dir/content" "$tmp/baseline.pck" "$tmp/baseline.log" "baseline content"
magic="$(od -An -tx1 -N4 "$tmp/baseline.pck" | tr -d ' \n')"
[ "$magic" = "47445043" ] || fail "baseline PCK magic is $magic, expected GDPC"

cp -R "$repo_dir/content/." "$tmp/content"
python3 - "$tmp/content/STS2Philosophers/localization/eng/events.json" <<'PY'
import json, sys
path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
data["FAST_PCK_TEST"] = "fast path without Godot"
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
cp "$tmp/content/images/events/philosophers_gaze.png" "$tmp/content/images/events/zeno_assent_boundary.png"
expect_status 0 "$tmp/content" "$tmp/modified.pck" "$tmp/modified.log" "JSON and PNG changes"
grep -aq "FAST_PCK_TEST" "$tmp/modified.pck" || fail "JSON key is missing from the fast PCK"

cp -R "$repo_dir/content/." "$tmp/missing-alias"
python3 - "$tmp/missing-alias/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = '# These events use'
assert needle in text
text = text.replace(needle, '["res://images/events/missing_alias.png", "res://images/events/missing_source.png"],\n\n' + needle, 1)
open(path, "w", encoding="utf-8").write(text)
PY
expect_status 3 "$tmp/missing-alias" "$tmp/missing-alias.pck" "$tmp/missing-alias.log" "missing alias source asks for Godot fallback"

cp -R "$repo_dir/content/." "$tmp/missing-import"
rm "$tmp/missing-import/STS2Philosophers/images/heraclitus_lyre.svg.import"
expect_status 3 "$tmp/missing-import" "$tmp/missing-import.pck" "$tmp/missing-import.log" "missing texture import asks for Godot fallback"

cp -R "$repo_dir/content/." "$tmp/missing-resource"
rm "$tmp/missing-resource/STS2Philosophers/localization/eng/relics.json"
expect_status 3 "$tmp/missing-resource" "$tmp/missing-resource.pck" "$tmp/missing-resource.log" "missing JSON asks for Godot fallback"

cp -R "$repo_dir/content/." "$tmp/unsupported-resource"
python3 - "$tmp/unsupported-resource/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = 'var resource_paths: Array[String] = [\n'
assert needle in text
text = text.replace(needle, needle + '\t\t"STS2Philosophers/events/unsupported.tscn",\n', 1)
open(path, "w", encoding="utf-8").write(text)
PY
expect_status 3 "$tmp/unsupported-resource" "$tmp/unsupported-resource.pck" "$tmp/unsupported-resource.log" "unsupported tscn asks for Godot fallback"

cp -R "$repo_dir/content/." "$tmp/unsupported-shader"
python3 - "$tmp/unsupported-shader/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = 'var texture_paths: Array[String] = [\n'
assert needle in text
text = text.replace(needle, needle + '\t\t"res://STS2Philosophers/shaders/unsupported.gdshader",\n', 1)
open(path, "w", encoding="utf-8").write(text)
PY
expect_status 3 "$tmp/unsupported-shader" "$tmp/unsupported-shader.pck" "$tmp/unsupported-shader.log" "unsupported gdshader asks for Godot fallback"

printf 'not a png' > "$tmp/content/images/events/philosophers_gaze.png"
expect_status 3 "$tmp/content" "$tmp/bad-png.pck" "$tmp/bad-png.log" "corrupt PNG asks for Godot fallback"
cp "$repo_dir/content/images/events/philosophers_gaze.png" "$tmp/content/images/events/philosophers_gaze.png"

printf '\n<!-- test -->\n' >> "$tmp/content/STS2Philosophers/images/heraclitus_lyre.svg"
expect_status 3 "$tmp/content" "$tmp/stale-svg.pck" "$tmp/stale-svg.log" "changed SVG asks for Godot fallback"

ctex="$(find "$tmp/content/.godot/imported" -name 'heraclitus_lyre.svg-*.ctex' -print -quit)"
[ -n "$ctex" ] || fail "missing SVG ctex cache in fixture"
printf 'not a ctex' > "$ctex"
expect_status 3 "$tmp/content" "$tmp/bad-ctex.pck" "$tmp/bad-ctex.log" "corrupt SVG ctex asks for Godot fallback"

echo "ContentPckPacker checks passed."
