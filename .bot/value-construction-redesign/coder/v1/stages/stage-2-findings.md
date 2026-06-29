# Stage 2 — findings (source covers Build's special cases; case-2b pinned)

**Branch:** `value-construction-redesign` · coder · 2026-06-29
Stage 2 is **verification + pinning**, not new machinery — both targets the ctor flip (Stage 3) needs already exist (read-path-unification built the readers; the 2-arg `Convert` is case-2b's engine). Confirmed below; nothing added to production code.

## 2.1 — `source` already absorbs Build's two special cases ✅

Build (`type/this.cs:232`) handles two cases before its `Convert` call. On the from-raw path these are the reader's job, and the readers **already exist**:

| Build special case | Covered by (already in tree) |
|---|---|
| Variable-name target (`%s%`, `IRawNameResolvable`) → `Variable.Resolve` | `app/variable/serializer/Reader.cs` — an `ITypeReader` doing `Variable.Resolve(reader.String(), ctx.Context)`. Registered under type name `variable`. |
| `%ref%` template (a text with holes) → live template, never coerced | `app/type/text/serializer/Reader.cs` — borns `new text.@this(reader.String(), ctx.Template)`; `ctx.Template = "plang"` makes a hole-bearing leaf a live template. |

⇒ Stage 3 can route a raw form to `source` and **delete Build's Variable/`%ref%` branches** without dropping behavior. No additive code needed in Stage 2. (Existing read-path tests in `LazyDeserialize/**` already exercise variable resolution + template materialization through `source`.)

## 2.2 — case-2b's engine is the existing 2-arg `type.Convert(item)` ✅

Re-typing an already-built value (`text "5"` → `number 5`) is the per-type `Convert` hook applied to the built item — exactly what `type.Convert(object?, ctx)` (`type/this.cs:177`) does (unwrap the leaf via `leaf.Clr<object>()` → family hook). `Build`'s tail already bridges its `Data` → `item` (`built.Peek() is item.@this`). Stage 3's `Build` keeps that bridge for the built-but-different branch; no new overload needed in Stage 2 (the from-raw `TryConvert` tail is thinned in Stage 5).

Pinned by `ConvertBuiltValueTests` (3 green):
- built `text "5"` + number → converts to `number 5`.
- built `text "abc"` + number → **fails (Error), not held** — the build-time safety net validateResponse depends on (§7).
- built `number 5` + number → round-trips (Stage 3's case 2a holds this).

## 2a sub-rules — lift from `Judge` (`type/this.cs:561-570`) into Stage 3's "hold" branch

Stage 3's case 2a ("built value already the declared type → hold") is **not** a bare `Name == declared`. `Judge` carries two refinements Stage 3 must preserve:

1. **same name, missing kind → re-kind:** `if (Kind != null && minted.Kind == null)` → `text.Kinded(Kind)` / `new binary(b.Value){Kind}`. A `text` declared `text/md` gains the kind without re-parsing.
2. **facet match → hold:** `value.Facet(Name) != null` → return value. An image bound to a `path` slot satisfies `path` (the `keepAsIs` semantics `set.cs:255-257` mirrors).

## Finding to carry into Stage 3 — `Build` and `Judge` DISAGREE on the built-but-different case

Surfaced by the trace; Stage 3 must implement deliberately, not copy one incumbent blindly:

- **`Build` (context path today):** different type → **eager `Convert`** via the hook (converts now).
- **`Judge` (no-context path today):** different type → for a **string-backed leaf**, `new source(backing, Name, Kind)` (**re-source**, lazy); for a structured/binary value, **hold** (instance wins).

The plan settled on **eager-convert** for case 2b (the §7 decision; validateResponse needs the failure to surface — it does so by constructing then `await Value()`). `Judge`'s lazy re-source is the more lazy-consistent alternative **but is not taken**, for a concrete reason: the `Declare` caller (`builder/code/Default.cs:927→934→943`) reads `p.Peek() as text.@this` (a text face) between two `Declare` calls; re-sourcing would turn `_item` into a `source` whose `Peek()` is a raw string, nulling that cast and mis-firing the `%var%` guard. Eager-convert keeps `_item` a built value, so the guard holds. (Stage 3 already records "keep Declare's input a built value, not a source.")

If a future cleanup fixes that guard to read the materialized value, re-sourcing (one lazy door for everything) becomes attractive — note it, don't do it here.

## Exit

- [x] 2.1 confirmed — variable + text-template readers exist; source covers both Build special cases.
- [x] 2.2 pinned — `ConvertBuiltValueTests` (convert / fail-not-hold / round-trip) green.
- [x] 2a sub-rules documented (re-kind, facet) with source lines.
- [x] Build-vs-Judge divergence documented for Stage 3.
- [x] `Build`/`Judge`/`Deserialize` untouched; no production code changed this stage.
- [x] Build clean; Data suite green.
