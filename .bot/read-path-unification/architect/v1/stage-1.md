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
_(coder fills as it lands: the totality map, the generic reader's final shape, any line-number corrections, anything that diverged.)_
