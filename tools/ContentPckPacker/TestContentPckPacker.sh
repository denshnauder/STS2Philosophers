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

# The fast path must reproduce the verifier's structural localization checks
# without Godot, and a rejected build must never replace the previous output.
printf 'OLD-OUTPUT' > "$tmp/guarded.pck"
cp -R "$repo_dir/content/." "$tmp/root-array"
printf '[]' > "$tmp/root-array/STS2Philosophers/localization/eng/relics.json"
expect_status 6 "$tmp/root-array" "$tmp/guarded.pck" "$tmp/root-array.log" "JSON root must be an object"
grep -q "Packed JSON root must be an object" "$tmp/root-array.log" || fail "root-array build did not name the root check"
[ "$(cat "$tmp/guarded.pck")" = "OLD-OUTPUT" ] || fail "a rejected root-array build replaced the old PCK"
if find "$tmp" -maxdepth 1 -name 'guarded.pck.tmp*' -print -quit | grep -q .; then
  fail "a rejected root-array build left a temporary PCK"
fi

cp -R "$repo_dir/content/." "$tmp/non-string"
python3 - "$tmp/non-string/STS2Philosophers/localization/eng/relics.json" <<'PY'
import json, sys
path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
data["FAST_PCK_NON_STRING"] = 3
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
expect_status 6 "$tmp/non-string" "$tmp/non-string.pck" "$tmp/non-string.log" "localization value must be a string"
grep -q "Localization value must be a string" "$tmp/non-string.log" || fail "non-string build did not name the value check"

cp -R "$repo_dir/content/." "$tmp/empty-value"
python3 - "$tmp/empty-value/STS2Philosophers/localization/eng/relics.json" <<'PY'
import json, sys
path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
data["FAST_PCK_EMPTY"] = ""
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
expect_status 6 "$tmp/empty-value" "$tmp/empty-value.pck" "$tmp/empty-value.log" "localization value must be non-empty"
grep -q "Localization key and value must be non-empty" "$tmp/empty-value.log" || fail "empty-value build did not name the non-empty check"

cp -R "$repo_dir/content/." "$tmp/key-parity"
python3 - "$tmp/key-parity/STS2Philosophers/localization/eng/relics.json" "$tmp/key-parity/STS2Philosophers/localization/zhs/relics.json" <<'PY'
import json, sys
eng_path, zhs_path = sys.argv[1], sys.argv[2]
eng = json.load(open(eng_path, encoding="utf-8"))
zhs = json.load(open(zhs_path, encoding="utf-8"))
key = sorted(set(eng) - set(zhs))[0] if set(eng) - set(zhs) else sorted(eng)[0]
zhs.pop(key, None)
json.dump(zhs, open(zhs_path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
expect_status 6 "$tmp/key-parity" "$tmp/key-parity.pck" "$tmp/key-parity.log" "eng and zhs keys must match"
grep -q "Localization key missing in zhs/relics.json" "$tmp/key-parity.log" || fail "key-parity build did not name the missing key"

cp -R "$repo_dir/content/." "$tmp/content"
python3 - "$tmp/content/STS2Philosophers/localization/eng/events.json" \
  "$tmp/content/STS2Philosophers/localization/zhs/events.json" <<'PY'
import json, sys
for path in sys.argv[1:]:
    data = json.load(open(path, encoding="utf-8"))
    data["FAST_PCK_TEST"] = "fast path without Godot"
    json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
cp "$tmp/content/images/events/philosophers_gaze.png" "$tmp/content/images/events/zeno_assent_boundary.png"
expect_status 0 "$tmp/content" "$tmp/modified.pck" "$tmp/modified.log" "JSON and PNG changes"
grep -aq "FAST_PCK_TEST" "$tmp/modified.pck" || fail "JSON key is missing from the fast PCK"

cp -R "$repo_dir/content/." "$tmp/unlisted-alias-source"
cp "$tmp/unlisted-alias-source/images/events/philosophers_gaze.png" "$tmp/unlisted-alias-source/STS2Philosophers/images/alias_unlisted.png"
cp "$tmp/unlisted-alias-source/images/events/philosophers_gaze.png.import" "$tmp/unlisted-alias-source/STS2Philosophers/images/alias_unlisted.png.import"
python3 - "$tmp/unlisted-alias-source/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = '# These events use'
assert needle in text
text = text.replace(needle, '["res://images/events/alias_unlisted.png", "res://STS2Philosophers/images/alias_unlisted.png"],\n\n' + needle, 1)
open(path, "w", encoding="utf-8").write(text)
PY
expect_status 3 "$tmp/unlisted-alias-source" "$tmp/unlisted-alias-source.pck" "$tmp/unlisted-alias-source.log" "alias source outside texture list asks for Godot fallback"

cp -R "$repo_dir/content/." "$tmp/missing-alias-import"
cp "$tmp/missing-alias-import/images/events/philosophers_gaze.png" "$tmp/missing-alias-import/STS2Philosophers/images/alias_without_import.png"
python3 - "$tmp/missing-alias-import/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = '# These events use'
assert needle in text
text = text.replace(needle, '["res://images/events/alias_without_import.png", "res://STS2Philosophers/images/alias_without_import.png"],\n\n' + needle, 1)
open(path, "w", encoding="utf-8").write(text)
PY
expect_status 3 "$tmp/missing-alias-import" "$tmp/missing-alias-import.pck" "$tmp/missing-alias-import.log" "missing alias import asks for Godot fallback"

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
