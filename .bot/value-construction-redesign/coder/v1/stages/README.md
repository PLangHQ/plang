# Value-construction redesign — stage files (coder)

**Branch:** `value-construction-redesign` (off `3ddcdb17f`)
**Author:** coder · 2026-06-29 · sequences `../../architect/v1/plan.md` (v3) into executable stages.
**Status:** sequence, not yet started. Each stage is independently buildable + green before the next.

These translate the architect's design into an executable order. The architect owns *why* + *what must hold* (invariants); these own *order*, *exact edits*, and *proof*. The plan's "you own the final shape" applies — line numbers drift, re-verify before cutting.

## The one-line shape being built

Construction stops doing the job twice. A declared-type value is born through **one ctor fork**:

```
json.Parse(value) first, then:
 1  no value            → typed absence  null.@this(type.Name, type.Kind)        (unchanged)
 2a built, type==decl   → hold as-is                                             (no convert)
 2b built, type!=decl   → CONVERT via the type's hook (re-type a built value)    (survives — §7)
 3  still-raw + decl     → mint source(raw, type, format-by-type), parse lazily   (the win)
```

- **From-raw (case 3)** converges on `source` + the reader — the double-convert + throwaway-`text` + reflection all dissolve.
- **Re-type-a-built-value (case 2b)** does NOT dissolve — three live sites hand the ctor a materialized wrong-typed value (Declare, validateResponse, `set`-type-differs). It keeps the per-type `Convert` hook applied to a built item (today's 2-arg `type.Convert(item)`, thinned).

## Stage order (dependency-gated; additive → flip → reroute → delete → OBP)

| # | File | Kind | One-liner |
|---|---|---|---|
| 1 | `stage-1-readers.md` | additive | reachable-set TRACE (the gate) → add missing `ITypeReader` readers |
| 2 | `stage-2-source-and-2b.md` | additive | `source` absorbs Build's `%ref%`/Variable cases; pin case-2b's named home |
| 3 | `stage-3-ctor-flip.md` | flip | rewrite the ctor + `Declare` to the four-case fork |
| 4 | `stage-4-caller-reroute.md` | flip | `set` + `validateResponse` drop their eager convert; route onto case 2b |
| 5 | `stage-5-delete.md` | subtract | delete `Judge`/`Deserialize`/Build's from-raw scaffolding/source fallback; thin `Convert` |
| 6 | `stage-6-obp-docs-tests.md` | — | OBP scan, docs, full test sweep |

## Global exit gates (every stage)

1. `dotnet build PlangConsole` clean — **zero PLNG002** (System.IO ban), zero PLNG001.
2. C# suite green: `dotnet run --project PLang.Tests` (or `./dev.sh full` before commit).
3. PLang suite green: rebuild from clean (stale-binary trap), then `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.
4. Commit + push the green stage before starting the next (next reviewer reads origin).

## Invariants (must hold at the end — from the plan)

1. One construction door (one ctor, one fork; no second eager from-raw path).
2. One conversion, never two (from-raw: 0–1 lazy in the reader; re-type: exactly 1 hook). No throwaway `text`.
3. No context fork in construction (no `Build`:`Judge` on `_context != null`).
4. Hooks stay; the eager *from-raw route* dies. Hooks reached two surviving ways: the reader (lazy) + the case-2b op.

## Merge order

This branch **lands first**, then merges into `read-path-unification`. No rebase gate on Stage 5 — delete freely against `3ddcdb17f`. Where it's cheap, build in read-path-unification's end-state shape (see plan "Relationship" section): readers as `ITypeReader` `Reader.cs`, don't depend on `serializer/Default.cs` static `Read` (it deletes there), `MaterializeFailed` authoring may move into `app.type.Create(source)` — don't hard-couple to `source.Value`'s current try/catch.
