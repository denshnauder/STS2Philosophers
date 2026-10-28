# ContentPckPacker

Fast content-PCK builder for `STS2Philosophers`.

## Why

The normal path starts Godot headlessly, imports content, and runs
`content/BuildContentPck.gd`. For JSON/PNG-only changes this is unnecessarily
slow and requires a working Godot/.NET editor environment.

This tool uses `BSchneppe.StS2.PckPacker` primitives:

- JSON files are packed byte-for-byte.
- PNG files are converted to Godot `GST2` ctex with `CtexConverter`.
- Existing SVG import caches are reused only when both the source hash and the
  ctex destination hash match Godot's `.md5` sidecar.
- `.import` remaps point at a **non-hidden** `STS2Philosophers/imported/...ctex`
  path. Godot 4.5.1 hangs when a PCK mounts a `.godot/` entry, so the original
  `.godot/imported/...` path is not packed directly.
- The PCK is written to a temporary sibling and moved into place only after a
  complete write, so a failed pack cannot leave a partial output file.
- The fast path refuses to silently omit unsupported resources. A `tscn`,
  `gdshader`, `gd`, `tres`, `res`, `material`, or `shader` path in
  `BuildContentPck.gd`, or a non-JSON resource / non-PNG-SVG texture, exits `3`
  so the caller uses the Godot fallback.

## Usage

```bash
dotnet run --project tools/ContentPckPacker -c Release -- <contentRoot> <output.pck>
```

Exit codes:

- `0`: fast PCK written.
- `3`: needs the Godot fallback (missing source/JSON, missing/stale SVG import
  cache, a PNG conversion that the fast path cannot handle, or an unsupported
  resource type).
- other: hard failure.

`tools/BuildContentPck.sh` and `tools/BuildContentPck.ps1` call this tool first
and fall back to the existing Godot import+pack path on exit `3`.

Run the fast/fallback regression checks with:

```bash
tools/ContentPckPacker/TestContentPckPacker.sh
```
