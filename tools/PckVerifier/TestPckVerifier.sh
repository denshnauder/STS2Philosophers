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

cp -R "$repo_dir/content/." "$tmp/missing-texture"
rm "$tmp/missing-texture/STS2Philosophers/images/heraclitus_lyre.svg"
python3 - "$tmp/missing-texture/BuildContentPck.gd" <<'PY'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
needle = '\t\t"res://STS2Philosophers/images/heraclitus_lyre.svg",\n'
assert needle in text
open(path, "w", encoding="utf-8").write(text.replace(needle, "", 1))
PY
pack "$tmp/missing-texture" "$tmp/missing-texture.pck"
expect_verify 4 "$tmp/missing-texture" "$tmp/missing-texture.pck" "$tmp/missing-texture.log" "missing required texture file"
grep -q "Missing packed file" "$tmp/missing-texture.log" || fail "verifier did not report the missing texture"

cp -R "$repo_dir/content/." "$tmp/wrong-root"
printf '[]' > "$tmp/wrong-root/STS2Philosophers/localization/eng/relics.json"
pack "$tmp/wrong-root" "$tmp/wrong-root.pck"
expect_verify 6 "$tmp/wrong-root" "$tmp/wrong-root.pck" "$tmp/wrong-root.log" "localization JSON root is not an object"
grep -q "Packed JSON root must be an object" "$tmp/wrong-root.log" || fail "verifier did not report the wrong JSON root"

cp -R "$repo_dir/content/." "$tmp/missing-key"
python3 - "$tmp/missing-key/STS2Philosophers/localization/zhs/relics.json" <<'PY'
import json,sys
path=sys.argv[1]
data=json.load(open(path,encoding='utf-8'))
del data[next(iter(data))]
open(path,'w',encoding='utf-8').write(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
PY
pack "$tmp/missing-key" "$tmp/missing-key.pck"
expect_verify 6 "$tmp/missing-key" "$tmp/missing-key.pck" "$tmp/missing-key.log" "localization key parity"
grep -q "Localization key missing in zhs/relics.json" "$tmp/missing-key.log" || fail "verifier did not report the missing localization key"

cp -R "$repo_dir/content/." "$tmp/wrong-value"
python3 - "$tmp/wrong-value/STS2Philosophers/localization/eng/relics.json" <<'PY'
import json,sys
path=sys.argv[1]
data=json.load(open(path,encoding='utf-8'))
key=next(iter(data))
data[key]=[]
open(path,'w',encoding='utf-8').write(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
PY
pack "$tmp/wrong-value" "$tmp/wrong-value.pck"
expect_verify 6 "$tmp/wrong-value" "$tmp/wrong-value.pck" "$tmp/wrong-value.log" "localization value is not a string"
grep -q "Localization value must be a string" "$tmp/wrong-value.log" || fail "verifier did not report the wrong value type"

cp -R "$repo_dir/content/." "$tmp/empty-value"
python3 - "$tmp/empty-value/STS2Philosophers/localization/eng/relics.json" <<'PY'
import json,sys
path=sys.argv[1]
data=json.load(open(path,encoding='utf-8'))
key=next(iter(data))
data[key]=''
open(path,'w',encoding='utf-8').write(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
PY
pack "$tmp/empty-value" "$tmp/empty-value.pck"
expect_verify 6 "$tmp/empty-value" "$tmp/empty-value.pck" "$tmp/empty-value.log" "empty localization value"
grep -q "Localization key and value must be non-empty" "$tmp/empty-value.log" || fail "verifier did not report the empty localization value"

cp -R "$repo_dir/content/." "$tmp/missing-import"
rm "$tmp/missing-import/STS2Philosophers/images/heraclitus_lyre.svg.import"
python3 - "$tmp/missing-import/BuildContentPck.gd" <<'PY'
import sys
path=sys.argv[1]
text=open(path,encoding='utf-8').read()
needle='''\tfor texture_path in texture_paths:
\t\tvar import_path: String = texture_path + ".import"
'''
replacement='''\tfor texture_path in texture_paths:
\t\tif texture_path == "res://STS2Philosophers/images/heraclitus_lyre.svg":
\t\t\tvar raw_error := add_file_or_fail(packer, texture_path, ProjectSettings.globalize_path(texture_path))
\t\t\tif raw_error != OK:
\t\t\t\tquit(raw_error)
\t\t\t\treturn
\t\t\tcontinue
\t\tvar import_path: String = texture_path + ".import"
'''
assert needle in text
open(path,'w',encoding='utf-8').write(text.replace(needle,replacement,1))
PY
"$godot_bin" --headless --path "$tmp/missing-import" --script "$tmp/missing-import/BuildContentPck.gd" -- "$tmp/missing-import.pck" >"$tmp/missing-import-build.log" 2>&1
expect_verify 4 "$tmp/missing-import" "$tmp/missing-import.pck" "$tmp/missing-import.log" "missing texture import metadata"
grep -q "Missing packed import metadata" "$tmp/missing-import.log" || fail "verifier did not report missing import metadata"

cp -R "$repo_dir/content/." "$tmp/empty-key"
python3 - "$tmp/empty-key/STS2Philosophers/localization/eng/relics.json" "$tmp/empty-key/STS2Philosophers/localization/zhs/relics.json" <<'PY'
import json,sys
for path in sys.argv[1:]:
 data=json.load(open(path,encoding='utf-8'))
 data['']='empty key'
 open(path,'w',encoding='utf-8').write(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
PY
pack "$tmp/empty-key" "$tmp/empty-key.pck"
expect_verify 6 "$tmp/empty-key" "$tmp/empty-key.pck" "$tmp/empty-key.log" "empty localization key"
grep -q "Localization key and value must be non-empty" "$tmp/empty-key.log" || fail "verifier did not report the empty localization key"

echo "PckVerifier checks passed."
