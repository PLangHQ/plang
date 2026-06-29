# Value-construction redesign — architect plan (v1)

**Branch:** `value-construction-redesign` (off `read-path-unification` @ `3ddcdb17f`)
**Author:** architect. Settled with Ingi 2026-06-29; builds on the coder's investigation (`../../coder/v1/report.md`). For coder review before any staging — nothing is sequenced into stage files yet.

## Why

Constructing a typed value does the job twice. `new Data("n", "5", type: number)` lifts the string to a throwaway `text` (because `Create` is called *without* the declared type), then converts that `text` to `number` — `string →(text.Convert)→ text →(number.Convert)→ number`. The `text` exists only to be torn back down to its raw string and reconverted. `set %x% = "5" as number` is worse: it converts eagerly (`set.cs:264`), then constructs a Data that converts *again* (`set.cs:273` → ctor `Build`) — up to three touches.

The read path already does this job once. It wraps the raw form in a lazy carrier (`source`) declared as its type and parses once, on first use, through the per-type reader. The eager construction path (`Build`/`Judge`) is an older, redundant second door that never moved onto the read mechanism. Both doors bottom out at the *same* per-type `Convert` hook — the read reaches it through a clean lookup table (`number/serializer/Reader.cs`), the eager path reaches it through `MethodInfo.Invoke` (`convert/this.cs:55`) plus the wasted `text` hop.

**This branch deletes the eager door.** Construction mints a `source` declared as the type — the same carrier the read uses — and materializes once, lazily, through the reader. `Build`, `Judge`, the dead `Deserialize`, and the eager `type.Convert(value, ctx)` dissolve. The Data constructor's "have I got context? `Build` : `Judge`" fork — the gnarliest branch in the type — disappears, because a `source` defers its parse to first use, where context is always present.

## The decision Ingi settled

`source` is lazy: a bad value fails at first *use*, keyed `MaterializeFailed` (`source.cs:109`), not at the `set`. **Accepted.** `set %x% = "abc" as number` defers its failure to first read. The builder already materializes authored values to validate them (`validateResponse.cs:222`), so a bad literal is still caught at build; only genuinely dynamic conversions defer — and that is already how every *read* behaves. One door, consistent semantics.

## The shape — one construction door

```
TODAY — two doors bottom out at the same hook

  read  "5" + {number}  ──► source(raw, number) ──first use──► number/serializer/Reader.cs ──► number 5     (lazy, lookup table)
  set   "5"  ──► guess shape ──► text "5" ──► type.Convert ──► Conversions.Of ──► MethodInfo.Invoke ──► number 5   (eager, reflection, + throwaway text)

PROPOSED — one door

  both  "5" + {number}  ──► source(raw, number) ──first use──► number/serializer/Reader.cs ──► number 5
```

The Data constructor, when a non-polymorphic type is declared:
- **raw form (`string`/`byte[]`) + declared type** → mint `source(value, type)` and hand it to the born-typed holder ctor (`data/this.cs:222`). One carrier, lazy, parsed once.
- **already a native value** (a built `number`, a `dict`, an `image`) → hold it as-is. No `source`, no re-convert.
- **no value + declared type** → the typed absence (`null.@this(type.Name, type.Kind)`) — unchanged.
- **polymorphic stamp / no declared type** → the natural lift (`Create(Parse(value))`) — unchanged; `string → text` is correct there, no double.

The predicate "raw form to defer vs already-born to hold" is the one judgement the coder must nail — it replaces the `Build`/`Judge` fork. See the leaf-trace.

## Invariants (must hold at the end)

1. **One construction door.** A declared-type value is born exactly one way — `source(raw, type)` for a raw form, held as-is for an already-native value. No second eager path beside the lazy read.
2. **One conversion.** No value passes through two `Convert` calls to reach its declared type. The throwaway `text` is gone.
3. **No context fork in construction.** The constructor has no "with-context `Build` / without-context `Judge`" branch. Construction never parses synchronously; the parse defers to `.Value()`, where context is present.
4. **Hooks stay; the eager route to them dies.** The per-type `Convert` hooks (the leaves) are unchanged. What dies is the eager *route* (`type.Convert` → `Conversions.Of` → reflection) and the throwaway-`text` lift on the way.

## Reader-coverage worklist (Stage 1 — the precondition)

The lazy path materializes a `source` by resolving the type's `serializer/Reader.cs` (`channel/serializer/Text.cs:82` → `Readers.Reader(name, kind)` → throws if absent). It works **today** for the 13 types that ship a `Reader.cs`. Four types have a `Convert` hook but **no `serializer/` folder at all** — reachable today only via the eager route:

| Type | Has Convert hook | Has `Reader.cs` | Action |
|---|---|---|---|
| number, text, bool, duration, guid, path, binary, image, list, dict, object, item, code | yes | yes | none — lazy path already covers |
| **date** | yes | **no** | add `serializer/Reader.cs` — pull the raw string, delegate to `date.@this.Convert(raw, kind, ctx)` |
| **datetime** | yes | **no** | add `serializer/Reader.cs` — delegate to `datetime.@this.Convert` |
| **time** | yes | **no** | add `serializer/Reader.cs` — delegate to `time.@this.Convert` |
| **choice** | yes | **no** | **investigate first** — `choice<T>` is keyed on the *enum's* name, not `"choice"` (see `type/this.cs:293` "a choice surfaces under its enum's name"). Confirm whether `as <Enum>` construction even reaches this ctor; if so, the reader registration must key by the enum name, not `choice`. Do not assume the scalar-3 shape fits. |

The construction `source` is `text/plain` format carrying a raw string, so a thin string-delegate reader (mirror of `number/serializer/Default.cs:62`, in the `ITypeReader` shape) is enough for construction. Verify each type's *wire* read separately — that is read-path-unification's concern, not a blocker here, but note any gap.

**Stage 1 exit:** every `(type, kind)` reachable by `as T` construction materializes through a `source`. Prove it — enumerate the reachable set, map each to its reader, log the map before anything is deleted.

## Leaf-trace — incumbents and where each goes

| Incumbent (leaf) | Where it lives now | Disposition |
|---|---|---|
| **The ctor's `Build`/`Judge`/context fork** | `data/this.cs:193-212` | **rewritten** — declared type → mint `source(value, type)` for a raw form, hold as-is for a native value; the `if _context != null … Build : Judge` branch is deleted. This is the core change. |
| **`Declare`'s `Build`/`Judge` fork** | `data/this.cs:241-253` | **rewritten** — the after-the-fact type stamp routes through the same single door (mint `source` / re-hold), no `Build`/`Judge`. Callers: `builder/code/Default.cs:927,943`. |
| **`set %x% = … as T` eager pre-convert** | `variable/set.cs:248-269` | **reroute** — drop the `type.Convert(converted, ctx)` call (`:264`); construct once with the declared type (`new Data(name, rawValue, type)`). **Keep** the `keepAsIs` richer-type rule (`:255-257`) — an image bound to a `path` slot stays an image; that is real semantics, not redundancy. |
| **build-validate eager convert** | `builder/validateResponse.cs:222` | **reroute** — construct the value with the declared type and force materialization (`await data.Value()`) to surface a bad-literal failure at build, replacing the eager `type.Convert` check. |
| **`Build(object?)`** | `type/this.cs:232` | **delete** — no callers after the ctor + `Declare` rewrite. |
| **`Judge(item.@this)`** | `type/this.cs:538` | **delete** — Build's no-context twin; same two call sites. |
| **`Deserialize(object?)`** | `type/this.cs:516` | **delete** — labelled "Replaces Judge" but **zero callers today**. Already dead; sweep it. |
| **`type.@this.Convert(object?, ctx)`** (2-arg family construct) | `type/this.cs:177` | **delete** — loses every caller (Build, set, validateResponse, the source fallback). The per-type static `Convert` *hooks* it dispatched to are NOT this method — they stay. |
| **`source` context-less fallback** | `source.cs:120-129` (the string branch) | **delete** — already flagged to die with born-with-context; construction now always carries context, removing its last reason to exist. |

## Demolition worklist — what dies, when, and what must NOT

**Organized by the stage that removes it (nothing dies before its callers are rerouted):**

- **Stage 1 (readers):** nothing dies — additive.
- **Stage 2 (source absorbs Build's special cases):** nothing dies — additive.
- **Stage 3 (ctor + Declare flip):** the ctor's `Build`/`Judge`/context fork (`data/this.cs:193-212`); `Declare`'s `Build`/`Judge` fork (`:241-253`).
- **Stage 4 (caller reroute):** the eager `type.Convert` call in `set.cs:264` (+ the now-dead pre-convert scaffolding around it); the eager convert check in `validateResponse.cs:222`.
- **Stage 5 (delete dead machinery):** `Build` (`type:232`), `Judge` (`type:538`), `Deserialize` (`type:516`), the eager `type.Convert(object?, ctx)` (`type:177`), the `source` context-less string fallback (`source:120-129`).
- **Stage 6:** no code death — OBP pass, docs, test sweep.

**Stays-list — looks like the eager machine, isn't. Do NOT delete:**

- **`Create(object?, context)`** (`type:372`) — the general CLR→plang lift, used by the read path and every list/dict slot. Untouched.
- **The per-type static `Convert(raw, kind, ctx)` hooks** (number, text, date, …) — the destination leaves. The reader calls them; the eager route to them dies, they don't.
- **`convert.OwnerOf` + the ownership table + `OwnedClr`** (`convert/this.cs:84+`) — the genuine CLR-interop seam (`Create`'s scalar lift, `Compare`, `TryConvert`).
- **`convert.OfStatic` + `_staticCache` + `Discover`** (`convert/this.cs:43,133`) — `Create`'s raw-scalar lift and the context-free deserialize.
- **`convert.Conversions.Of` (instance) + `_cache`** — **survives.** I was wrong earlier that the reflection dispatch disappears: `catalog/Conversion.cs:231` uses it to marshal a PLang value into a specific **C# type** at the action boundary (`%x%` → an `int` parameter). That is a different job (marshalling out to CLR, not construction). It stops being on the construction path; it does not die. Folding marshalling onto the reader table too is a *separate* cleanup — do not bundle it here.
- **The born-typed holder ctor** (`data/this.cs:222`) — the clean "Data is a dumb holder of an already-built value" path. This is what the construction ctor delegates into.
- **`source` + the reader registry** — these ARE the surviving door; they grow to absorb construction (Stages 1–2).

## Relationship to read-path-unification (open Q7, settled)

This branch **owns the value-ctor retirement** that read-path-unification's Stage 6 stub deferred. That stub stays a no-op on its branch (already restored, per the coder's branch-out). Sequencing: this branch assumes the `source` + reader mechanism as the construction door; it should rebase onto / merge after whatever read-path-unification settles for the reader registry. **Confirm the merge order with Ingi before Stage 5 deletes anything** — the two branches touch `source.cs` and the reader registry, so a late rebase is cheaper than a conflict-heavy one.

## Settled questions (from the coder's open list)

1. **Inflow across shapes.** Confirmed: scalars double-convert (string → text → target); already-typed scalars get a redundant re-`Convert` (number → number); containers return native from `Create` and skip the coercion. The fix targets the scalar/raw-form path; containers are already correct.
2. **Two materialization mechanisms — one or two?** **One.** The reader registry and the `Convert` hooks are the same mechanism: `number/serializer/Default.cs:62` *is* `number.Convert`. There are two *routes* to it (lazy reader / eager reflection); the eager route dies. There is no second table to build.
3. **`Create(raw, type)` lazy or eager?** **Lazy** — mint a `source`, reuse the read path. Settled with the lazy-failure decision above.
4. **Registration mechanism (source-gen a `typeName → Factory` table vs `CreateDelegate`)?** **Dissolves.** The reader registry is already that table — discovered by namespace convention (`reader/this.cs` IndexAssembly), zero per-call reflection. Do not build a second one. The per-call reflection lives only in the dying eager route.
5. **Return-`item` error semantics.** `source.Value` already owns the `MaterializeFailed` story (`source.cs:109`). Construction inherits it for free — no new exception type, no `Data.Error` round-trip in construction.
6. **`text` raw accessor.** Moot under this design — construction mints a `source` (which has `.Raw`), never a throwaway `text`. No leaf is handed a `text` to re-read.

## OBP validation pass

| Surface | Verb+Noun / object-decomposition check | Verdict |
|---|---|---|
| New `date`/`datetime`/`time`/`choice` `serializer/Reader.cs` | Names follow the existing `serializer/Reader.cs` convention (no new verb+noun). Each reader reads its own raw and delegates to its own `Convert` hook — a leaf touching its own value, not decomposing someone else's. | OK |
| Ctor flip (`data/this.cs`) | Passes the **whole** value to `source(value, type)` — no decompose into primitives. The "raw vs native" predicate is a shape question on the value, not a reach into `.Value`. | OK |
| `source` absorbs the write-target case | Lives inside `source`/its reader — the carrier owns its own materialization; no caller reaches into `source` to special-case `%s%`. | OK — verify the coder keeps it inside `source`, not in the ctor |
| `set` reroute | `set` hands the type to the ctor and stops doing the conversion itself — removes an allocate-here/convert-there split (the value was converted in `set`, then re-converted in the ctor). Fewer files own the transform. | OK — improves |
| Stays: `Conversions.Of` for marshalling | Not a new surface; flagged so it is not mistaken for dead. The marshalling-via-reflection is a pre-existing shape, out of scope. | Noted |

No new verb+noun names introduced. No value decomposed at a call site. The change *removes* a cross-file convert split (`set` ↔ ctor) rather than adding one.

## You own the final shape

Any code shape implied here (the string-delegate reader, the ctor predicate, the `set` rewrite) is a **suggestion to anchor the work, not a spec**. Coder and test-designer own the final form — match the established `Reader.cs` pattern and the born-with-context discipline, and correct anything here that the live code contradicts. Line numbers drift; re-verify before cutting.

## For the coder

Read this over and push back before we sequence anything into stages — challenge the predicate boundary (raw-vs-native), the `choice` keying, and the read-path-unification merge order in particular. The demolition worklist names a stage ordering as the *intended* sequence (readers → source → ctor flip → reroute → delete → OBP); it is the shape of the work, not committed stage files.
