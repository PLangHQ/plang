# Value-construction redesign — architect plan (v1)

**Branch:** `value-construction-redesign` (off `read-path-unification` @ `3ddcdb17f`)
**Author:** architect. Settled with Ingi 2026-06-29; incorporates the coder's plan review (`../../coder/v1/plan-review.md`), the codeanalyzer review (`../../codeanalyzer/v1/plan-review.md`), and the investigation (`../../coder/v1/report.md`). Codeanalyzer verdict: PASS — ready to sequence into stages.

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

PROPOSED — one door, the reader chosen by the declared type

  "5"       + {number}  ──► source(raw, number, text/plain)        ──first use──► number reader (scalar) ──► number 5
  '{"a":1}' + {dict}    ──► source(raw, dict,   application/plang) ──first use──► dict reader (json)     ──► dict
  {a:1}     + {dict}    ──► json.Parse already returned a native dict ──► hold as-is (no source)
```

The Data constructor, when a non-polymorphic type is declared, runs `json.Parse(value)` first (it natives-out a JSON `JsonElement`/`JsonNode`, leaves a plain string untouched — `json.cs:131`), then forks **four** ways:
1. **no value + declared type** → typed absence `null.@this(type.Name, type.Kind)` — the declaration survives with nothing to lift (tool-param slots, typed nulls). **unchanged** — this is the *first* arm of the live block (`data/this.cs:195-199`); it must survive the rewrite (`json.Parse(null)` matches none of cases 2–4, so a literal three-case rewrite would drop a typed null to a bare null citizen).
2. **already a native value** (Parse returned a `dict`/`list`/`number`/…) → hold it as-is. No `source`, no re-convert. Containers already skip coercion today.
3. **still-raw string / `byte[]` + scalar type** (number, date, bool, guid, …) → mint `source(value, type, format = text/plain)` → the scalar (value) reader. **This is the double-convert win.**
4. **still-raw string + container type** (dict/list/object/item) → mint `source(value, type, format = application/plang)` → the json reader (`BeginObject`). A dynamic `%jsonStr% as dict` keeps working (Ingi, 2026-06-29).

(A polymorphic stamp / no declared type is outside this block entirely — the natural lift `Create(Parse(value))`, unchanged; `string → text` is correct there, no double.)

A still-raw string **never becomes a `text` *value***. It stays raw under its declared type until that type's reader parses it once. The one judgement the coder must nail is the `source`'s **format, picked from the declared type's reader mode** — scalar → `text/plain`, container → `application/plang` (`byte[]` follows `FromRaw`'s existing format logic). That replaces the `Build`/`Judge` fork. See the leaf-trace.

## Invariants (must hold at the end)

1. **One construction door.** A declared-type value is born exactly one way — `source(raw, type)` for a raw form, held as-is for an already-native value. No second eager path beside the lazy read.
2. **One conversion.** No value passes through two `Convert` calls to reach its declared type. The throwaway `text` is gone.
3. **No context fork in construction.** The constructor has no "with-context `Build` / without-context `Judge`" branch. Construction never parses synchronously; the parse defers to `.Value()`, where context is present.
4. **Hooks stay; the eager route to them dies.** The per-type `Convert` hooks (the leaves) are unchanged. What dies is the eager *route* (`type.Convert` → `Conversions.Of` → reflection) and the throwaway-`text` lift on the way.

## Reader-coverage worklist (Stage 1 — the precondition)

The lazy path materializes a `source` by resolving the type's **`ITypeReader`** `serializer/Reader.cs` — reached via `channel/serializer/Text.cs:82` → `Readers.Reader(name, kind)`, which **throws `NotSupportedException` if absent** (`type/reader/this.cs:119`). The shape to mirror is **`number/serializer/Reader.cs`** (the `ITypeReader` pull), **not** `Default.cs` (a different `Of()`/whole-payload contract — do not send the coder there). For a scalar it is a one-liner: `reader.Null() ? null-typed : <family>.@this.Convert(reader.String(), kind, ctx.Context)`.

The gate is **has a reader**, not has a hook. `object`/`item`/`code` have no `Convert` hook yet ride the lazy path via their own readers; `table` already ships a `Reader.cs`. The gaps reachable today only via the eager route:

| Gap | Status | Action |
|---|---|---|
| **date, datetime, time** | `Convert` hook, no `serializer/` folder | add an `ITypeReader` `serializer/Reader.cs` mirroring `number`'s — pull the string, delegate to the family `Convert` hook |
| **file, directory, url, permission** | `serializer/` folder with **only `Default.cs`**, no `Reader.cs` | **if reachable** by `as file`/`as url`/… construction, `Readers.Reader` throws `NotSupportedException` — which `source.Value`'s catch (`source.cs:98`: only Json/Format/InvalidOperation) does **not** cover, so it escapes to the courier instead of failing the Data as `MaterializeFailed`. Add a reader, or prove it is not a construction target. |
| **choice** | `Convert` in `choice/this.cs`, no serializer folder | **own sub-task** — see below |

**choice — its own sub-task.** `choice<T>` is keyed under the *enum's* name, not `"choice"` (`type/this.cs:288-296`). Its reader must register under the enum name in the **runtime** table (`_runtimeTyped`, not the generated convention scan), take the scalar string token, and validate against **`ValidValues` membership** — not a string-ctor (closed-enum rule). First confirm `as <Enum>` construction even reaches this ctor; if it doesn't, choice needs no reader — record the trace. The scalar one-liner will **not** fit.

**Stage 1 exit:** enumerate the *actual* `(type, kind)` set reachable by `as T` construction (don't assume the rows above are complete — `file`/`directory`/`url`/`permission` especially), map each to its reader, and add or justify-excluding each. Also decide whether `source.Value` should catch the missing-reader throw as defense — totality is the primary fix; this is belt-and-suspenders. Log the map before anything is deleted.

Two reachable-set edges to resolve, not leave silent (codeanalyzer v1):
- **`byte[]` + container type** has no arm — the fork pairs `byte[]` with the scalar case. Confirm it's unreachable, or assign it a format; don't let it fall through.
- **`as binary` with a kind narrows** to the kind's inner type when that type owns a reader, else rides as `binary` (`type/reader/this.cs:111-118`). The reachable-`(type, kind)` trace must account for this branch — the flat per-type table won't surface it.

## Leaf-trace — incumbents and where each goes

| Incumbent (leaf) | Where it lives now | Disposition |
|---|---|---|
| **The ctor's `Build`/`Judge`/context fork** | `data/this.cs:193-212` | **rewritten** — declared type → the four-case fork (see The shape). **Preserve** the `value == null` typed-absence guard (`:195-199`); mint `source(value, type)` (format by type) for a raw form; hold native as-is; delete the `if _context != null … Build : Judge` branch. This is the core change. |
| **`Declare`'s `Build`/`Judge` fork** | `data/this.cs:241-253` | **rewritten** — the after-the-fact type stamp routes through the same single door (mint `source` / re-hold), no `Build`/`Judge`. Callers: `builder/code/Default.cs:927,943`. |
| **`set %x% = … as T` eager pre-convert** | `variable/set.cs:248-269` | **reroute** — drop the `type.Convert(converted, ctx)` call (`:264`); construct once with the declared type, handing the ctor the **raw** value (`sourceValue`), not the eagerly-`converted` one. `sourceValue` may itself already be native (`RawUntouched → Value()`) — that is just the already-native → hold case, no special handling. **Keep** the `keepAsIs` richer-type rule (`:255-257`) — an image bound to a `path` slot stays an image; that is real semantics, not redundancy (and `keepAsIs` already passes `type = null`, so it never enters typed construction). |
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

## Relationship to read-path-unification (settled — this branch lands first)

This branch **owns the value-ctor retirement** that read-path-unification's Stage 6 stub deferred. **Decided (Ingi, 2026-06-29): this branch lands first, then merges into `read-path-unification`.** So there is **no rebase gate on Stage 5** — delete freely against the current base (`3ddcdb17f`); reconciling these changes with read-path-unification's own in-flight edits to `source.cs` / the reader registry happens at *that* merge, and is read-path-unification's problem to absorb, not a constraint on this branch. Build, delete, and finish here on its own terms.

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

The coder reviewed v1 and approved; its four corrections are folded above — the predicate (now **four** cases: typed-absence + three value-bearing, format by type), the `Reader.cs` (not `Default.cs`) template, the wider reachable set + the `NotSupportedException` leak, and the `choice` sub-task. The codeanalyzer reviewed v2 and passed, with one required fix — the dropped **typed-absence** arm, now restored (case 1). Its two non-blocking notes (`byte[]` + container, `as binary` narrowing) are in the Stage-1 exit. Merge order is settled (this branch lands first). The demolition worklist names a stage ordering as the *intended* sequence (readers → source → ctor flip → reroute → delete → OBP); it is the shape of the work, not committed stage files — sequence it when ready.
