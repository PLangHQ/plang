# Stage 1 — total reader registry (one `ITypeReader`, thin generic default)

**Design authority:** `plan.md` "Phase 1" + "Reader-coverage worklist". This is the execution checklist only — do not re-derive design here. Line numbers drift; re-verify before cutting.

## Entry (green before starting)
- Branch builds clean (`dotnet build PlangConsole`); C# suite (`dotnet run --project PLang.Tests`) and `plang --test` (from `Tests/`) green — record the baseline counts here.

## Exit (green after)
- `App.Type.Reader(source)` is **total** — never null: a specific reader (`dict`/`list`/`table`/`object`, binary-family) or the **thin generic default reader** (string-raw scalars, one delegation to `type.Convert`, zero branching).
- `Readers.Of` + the `Read` delegate type + `_generated`/`_runtime` tables + the static-`Read` discovery branch are deleted; no caller references them.
- `code.load` `Register()` targets the `ITypeReader` table (static `Read` wrapped in an adapter).
- **Totality proven:** every `(type, kind)` reachable today via `Of`/`Convert`/direct-binary maps to exactly one reader — log the map here before deleting `Of`.
- Behavior unchanged this stage (the registry is consumed the same way; `source.Value` still works). Build + both suites green.

## Dies (re-verify line numbers)
- `Readers.Of` (`reader/this.cs:71`), the `Read` delegate (`:37`), `_generated`/`_runtime` (`:39-40`), static-`Read` discovery (`:181-202`).

## Stays / re-homed
- `ITypeReader` registry (`_generatedTyped`/`_runtimeTyped`/`TypeOf`, discovery); rename lookup `Readers.Typed` → `App.Type.Reader`.
- Per-type `Convert` hooks + `catalog/Conversion.cs` router (generic reader delegates to them).
- `byte[]` → binary family, not the generic reader.

## Shipped + deltas from plan

### Settled design (diverges from plan's Option-A/B framing AND the "generic reader" — both rejected by Ingi)
The read unifies to **one method**: the type's `ITypeReader.Read(ref reader, kind, ctx)` (already exists, format-blind). No `Read(source)` second door, no `OneShotReader`/`json.Reader` at the call site, **and no generic/fallback reader**.

- **NO generic reader.** A `Scalar`/generic fallback was tried and rejected: it's a *second execution path* (specific-file types vs. fallback) + a *fork* (switch over token-kind = an if). Instead **every type ships its own `serializer/Reader.cs`** — one path, total-by-coverage registry, no fallback, each reader IS its type (nothing threaded in, `ReadContext` never grows). See [[feedback_no_generic_fallback_path]].
- **The serializer is the sole reader-maker.** `new json.Reader(reader)` (`Wire.cs:409`) is a format leak → the serializer makes its own `IReader`; nothing in `Wire`/`source` says "json".
- **`source` holds only inert data:** raw slice (`reader.RawValue()`) + **format tag** (`reader.Format`, a string) + type + kind + context. No live serializer pinned — at `.Value()` it resolves the stateless serializer from the registry by the tag, gets a reader over the slice, calls the type's one `Read`. Nothing held, no outside resource.
- **Kills** `Of`, `type.Convert`-as-door, `json.Parse`, the `JsonDocument` DOM — all collapse into `type.Read(ref reader)`.

### The 6 readers to port (Of static-Read → ITypeReader)
`path, code, object(json), item(json), table(csv), image`. Adding each makes the WIRE path (`Wire.cs:407` `Typed`) use it (replacing the `json.Parse` fallback for that type) — so each is exercised + testable now. Port one, build + both suites green, repeat. `item.json` already has `ReadSlot<TReader>` (token pull) — object/item lean on it.

### Totality map (Of static-Read vs ITypeReader)
- Pure `X.Convert(raw,kind,ctx).Peek()` (→ generic reader): number, guid, duration (+ date/datetime/time/primitive — verify).
- `ITypeReader` already present: number, bool, dict, list, guid, text, duration.
- `text` reader carries `ctx.Template` (the `%ref%` path) — stays specific.
- No reader yet, need specific ITypeReaders: table(csv), object(json), image, path, code, item.
- Render-only (no Read, never a value slot): url, directory, file, permission.

### Sequencing note (real dependency, not the rejected 1a/1b/1c)
Deleting `Of` (this stage's exit) needs `source.Value` off `Of`, which needs `source` to carry the format tag + raw-via-`IReader` — that capture is **Leg A (Stage 2)**. So the natural order is: (1) generic reader + total `App.Type.Reader` [additive, green], (2) serializer `Reader(raw)` opener + `source` format tag + `source.Value` through it, (3) delete `Of` once no caller remains. `Of`-deletion lands at the end of that arc, which may straddle the Stage 1/2 line — will note where it actually falls.

_(below: line-number corrections + what diverged, filled as it lands)_
