# Value-construction redesign — architect plan (v1)

**Branch:** `value-construction-redesign` (off `read-path-unification` @ `3ddcdb17f`)
**Author:** architect. Settled with Ingi 2026-06-29; incorporates the coder's plan review + addendum v2 (`../../coder/v1/plan-review.md`), the codeanalyzer review (`../../codeanalyzer/v1/plan-review.md`), the investigation (`../../coder/v1/report.md`), and a cross-check against the full `read-path-unification` plan (6 phases). Codeanalyzer: PASS. Coder addendum v2 corrected one over-claim — `Build`/`Judge` do **not** fully dissolve (case 2b survives) — now folded throughout.

## Why

Constructing a typed value does the job twice. `new Data("n", "5", type: number)` lifts the string to a throwaway `text` (because `Create` is called *without* the declared type), then converts that `text` to `number` — `string →(text.Convert)→ text →(number.Convert)→ number`. The `text` exists only to be torn back down to its raw string and reconverted. `set %x% = "5" as number` is worse: it converts eagerly (`set.cs:264`), then constructs a Data that converts *again* (`set.cs:273` → ctor `Build`) — up to three touches.

The read path already does this job once. It wraps the raw form in a lazy carrier (`source`) declared as its type and parses once, on first use, through the per-type reader. The eager construction path (`Build`/`Judge`) is an older, redundant second door that never moved onto the read mechanism. Both doors bottom out at the *same* per-type `Convert` hook — the read reaches it through a clean lookup table (`number/serializer/Reader.cs`), the eager path reaches it through `MethodInfo.Invoke` (`convert/this.cs:55`) plus the wasted `text` hop.

**This branch deletes the eager *from-raw* door.** Construction mints a `source` declared as the type — the same carrier the read uses — and materializes once, lazily, through the reader. The throwaway `text` lift, the `MethodInfo.Invoke` reflection on the from-raw path, the dead `Deserialize`, and the constructor's "have I got context? `Build` : `Judge`" fork (the gnarliest branch in the type) all go — because a `source` defers its parse to first use, where context is always present.

**One thing does not dissolve, contrary to the first draft of this plan:** re-typing an *already-built* value to a declared type (`text "5"` → `number 5`). Three live sites do this on a materialized value, and a built value can't be re-sourced (no clean raw). That operation — the per-type `Convert` hook applied to a built item, which is what `Build`/the 2-arg `type.Convert` does today — **survives** (thinned and renamed). The win is real and large, but it is "delete the eager from-raw route," not "delete `Build` outright." (This is the coder's §7 catch, verified against all three call sites; it also resolves read-path-unification's deferred Q4 — *can* `Build`/`Judge` be deleted? Not fully.)

## The decision Ingi settled

`source` is lazy: a bad value fails at first *use*, keyed `MaterializeFailed`, not at the `set`. **Accepted.** `set %x% = "abc" as number` defers its failure to first read. The builder already materializes authored values to validate them (`validateResponse.cs:222`), so a bad literal is still caught at build; only genuinely dynamic conversions defer — and that is already how every *read* behaves. One door, consistent semantics.

**This safety net depends on case 2b** (the convert-a-built-value path). `validateResponse` hands a *materialized* value to the declared type; if construction merely *held* it (the naive "native → hold"), the validation convert would no-op and `"abc" as number` would pass build. The bad-literal catch holds **only because** a built-but-wrong-type value is *converted*, not held — and that convert fails honestly on `"abc"`. So case 2b is not an edge case; it is what makes the lazy-failure decision sound.

## The shape — one construction door

```
TODAY — two doors bottom out at the same hook

  read  "5" + {number}  ──► source(raw, number) ──first use──► number/serializer/Reader.cs ──► number 5     (lazy, lookup table)
  set   "5"  ──► guess shape ──► text "5" ──► type.Convert ──► Conversions.Of ──► MethodInfo.Invoke ──► number 5   (eager, reflection, + throwaway text)

PROPOSED — one door; format by declared type; a built-but-wrong-type value converts

  "5"        + {number} ──► source(raw, number, text/plain)        ──first use──► number reader ──► number 5
  '{"a":1}'  + {dict}   ──► source(raw, dict,   application/plang) ──first use──► dict reader    ──► dict
  {a:1}      + {dict}   ──► already a native dict      ──► hold as-is (no source)
  text "5"   + {number} ──► already built, wrong type  ──► convert via number's hook ──► number 5
```

The Data constructor, when a non-polymorphic type is declared, runs `json.Parse(value)` first (it natives-out a JSON `JsonElement`/`JsonNode`, leaves a plain string untouched — `json.cs:131`), then forks:
1. **no value + declared type** → typed absence `null.@this(type.Name, type.Kind)` — the declaration survives with nothing to lift (tool-param slots, typed nulls). **unchanged** — the *first* arm of the live block (`data/this.cs:195-199`); it must survive the rewrite (`json.Parse(null)` matches none of the value-bearing cases, so a literal "raw→source / native→hold" rewrite would drop a typed null to a bare null citizen).
2. **already a built value** — split on whether its type already matches the declared one:
   - **2a. type == declared** → hold as-is. No `source`, no re-convert. (A raw-backed value of the matching type stays lazy — `set.cs:240-246` already short-circuits this before the ctor.)
   - **2b. type != declared** → **convert** the built value to the declared type (apply the declared type's `Convert` hook to it). This is **not** from-raw — it re-types an already-materialized value. **Three live sites land here** (Declare, validateResponse, `set`'s type-differs fall-through — see the leaf-trace). A built value can *not* be re-sourced: a built `text` exposes only `ToString()` (display) and `Clr` (lowering), and a built non-text (a re-declared `dict`) has no string raw at all — so the convert is real, and the operation that does it **survives** the redesign.
3. **still-raw form + declared type** → mint a `source(value, type)` whose **format comes from the declared type**: a scalar → `text/plain` (the scalar reader); a container (dict/list/object/item) → `application/plang` (the json reader, `BeginObject`); a `byte[]` → the type's **kind→mime** (`App.Format.Mime("." + kind)`, as the binary-family readers already do — **never `text/plain`**). The parse defers to first use. **This is the double-convert win** — `"5" as number`, `'{"a":1}' as dict`, bytes `as image`.

(A polymorphic stamp / no declared type is outside this block — the natural lift `Create(Parse(value))`, unchanged; `string → text` is correct there, no double.)

A still-raw string **never becomes a `text` *value***; it stays raw under its declared type until the reader parses it once. Two judgements the coder must nail: **(a)** the `source`'s format, picked from the declared type (scalar → `text/plain`, container → `application/plang`, bytes → kind→mime); **(b)** case 2's type-match split — hold when the built value already is the declared type, convert when it isn't. The from-raw eager route dissolves into `source`; the **convert-a-built-value** operation does **not** dissolve (see the leaf-trace).

## Invariants (must hold at the end)

1. **One construction door.** A declared-type value is born through one ctor with one fork: `source(raw, type)` for a raw form (deferred), held as-is for a built value already of the type, converted for a built value of a *different* type. No second eager *from-raw* path beside the lazy read.
2. **One conversion, never two.** No value passes through two `Convert` calls to reach its declared type. The from-raw path does zero-or-one (lazy, in the reader); the re-type path does exactly one (the hook, on the built value). The throwaway `text` middle hop is gone in both.
3. **No context fork in construction.** The constructor has no "with-context `Build` / without-context `Judge`" branch. From-raw never parses synchronously (defers to `.Value()`, where context is present); the re-type path reaches the hook directly.
4. **Hooks stay; the eager *from-raw route* dies.** The per-type `Convert` hooks (the leaves) are unchanged. What dies is the eager from-raw *route* (`type.Convert(object?,ctx)` → `Conversions.Of` → reflection → hook) and the throwaway-`text` lift on the way. The hooks are still reached two ways that remain: the reader (from-raw, lazy) and the re-type-a-built-value op (case 2b).

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

Edges to resolve, not leave silent (codeanalyzer v1 + coder v2):
- **`byte[]` format comes from the declared type's kind→mime** (case 3), never `text/plain`. That assumes a binary-family declared type; a `byte[]` declared as a *structural container* (`byte[] as dict`) has no clean arm — confirm unreachable, or define it. (Drop the old `FromRaw` reference — that's the dict/list container factory, not a source-format chooser; there is no `source.FromRaw`.)
- **`as binary` with a kind narrows** to the kind's inner type when that type owns a reader, else rides as `binary` (`type/reader/this.cs:111-118`). The reachable-`(type, kind)` trace must account for this branch — the flat per-type table won't surface it.

## Leaf-trace — incumbents and where each goes

| Incumbent (leaf) | Where it lives now | Disposition |
|---|---|---|
| **The ctor's `Build`/`Judge`/context fork** | `data/this.cs:193-212` | **rewritten** — declared type → the fork in The shape. **Preserve** the `value == null` typed-absence guard (`:195-199`); mint `source(value, type)` (format by type) for a raw form; **case 2 splits** — hold a built value already of the type, **convert** a built value of a different type (2b); delete the `if _context != null … Build : Judge` branch. This is the core change. |
| **`Declare`'s `Build`/`Judge` fork** | `data/this.cs:241-253` | **rewritten** — the after-the-fact stamp routes through the same logic: a built value already of the type holds; a different type **converts** (case 2b). Callers `builder/code/Default.cs:927,943` hand it an already-built `text`, so this is a case-2b site. **Keep `Declare`'s input a built value, not a `source`** — the `%var%`-skip guard at `:934-935` reads `Peek() as text.@this` + `StartsWith("%")`; a `source` input makes `Peek()` a raw string, nulls the cast, and mis-fires the guard. |
| **`set %x% = … as T` eager pre-convert** | `variable/set.cs:248-269` | **reroute onto case 2b.** The match case already returned (`:240-246`), so the fall-through is a *materialized, type-differs* value. Construct with the declared type and let the ctor's **case 2b** convert it — dropping both set's own `type.Convert(converted)` (`:264`) and the redundant re-convert the old ctor `Build` did. set stops owning the transform. **Keep** the `keepAsIs` richer-type rule (`:255-257`) — an image bound to a `path` slot stays an image (and `keepAsIs` passes `type = null`, so it never enters typed construction). |
| **build-validate eager convert** | `builder/validateResponse.cs:222` | **reroute onto case 2b — this IS the safety net.** `resolved` is a materialized value; construct with the declared type so case 2b converts it, and a bad literal (`"abc" as number`) **fails the convert** → caught at build. "Construct + hold" would no-op the check and let bad literals pass. The convert must run. |
| **`Build(object?)`** | `type/this.cs:232` | **gut, not delete-wholesale.** Its from-raw scaffolding (throwaway-`text` lift, the `%ref%`/Variable special-cases now owned by `source`) goes; its core — apply the type's `Convert` hook to a built value — is case 2b's surviving op. Whether that lands as a thinned `Build`, the renamed 2-arg `Convert`, or a method on the value is staging's call; the *behavior* survives. |
| **`Judge(item.@this)`** | `type/this.cs:538` | **delete** — Build's no-context twin. Its job folds into case 2b (which reaches the hook directly / via the context-free static hook). Verify case 2b covers Judge's `%ref%`-template and Variable cases — those are `source`'s job on the *raw* path; a *built*-value re-type never templates. |
| **`Deserialize(object?)`** | `type/this.cs:516` | **delete** — labelled "Replaces Judge" but **zero callers today**. Already dead; sweep it. |
| **`type.@this.Convert(object?, ctx)`** (2-arg family construct) | `type/this.cs:177` | **survives, thinned + renamed — it IS case 2b's engine** (unwrap a leaf item → apply the family hook). It loses its *from-raw* callers (the ctor's raw path, the source fallback) but stays as the convert-a-built-value op used by Declare/validateResponse/`set`-type-differs. Name it for what it does (re-type a built value); final name is staging's. The from-raw *route into it* dies; the method does not. |
| **`source` context-less fallback** | `source.cs:120-129` (the string branch) | **delete** — already flagged to die with born-with-context; construction now always carries context, removing its last reason to exist. |

## Demolition worklist — what dies, when, and what must NOT

**Organized by the stage that removes it (nothing dies before its callers are rerouted):**

- **Stage 1 (readers):** nothing dies — additive.
- **Stage 2 (source absorbs Build's special cases):** nothing dies — additive.
- **Stage 3 (ctor + Declare flip):** the ctor's `Build`/`Judge`/context fork (`data/this.cs:193-212`); `Declare`'s `Build`/`Judge` fork (`:241-253`).
- **Stage 4 (caller reroute):** `set.cs`'s own `type.Convert(converted)` call (`:264`) + its eager pre-convert scaffolding — the conversion **moves into** the ctor's case 2b; `validateResponse.cs:222`'s eager check — rerouted to construct-with-type so case 2b converts.
- **Stage 5 (delete dead machinery):** `Judge` (`type:538`); `Deserialize` (`type:516`, already dead); `Build`'s **from-raw scaffolding** (`type:232` — throwaway-`text` lift + `%ref%`/Variable cases); the **from-raw route** into `type.Convert(object?, ctx)` (`type:177` — the *method* survives as case 2b's engine); the `source` context-less string fallback (`source:120-129`). **NOT deleted:** the convert-a-built-value op.
- **Stage 6:** no code death — OBP pass, docs, test sweep.

**Stays-list — looks like the eager machine, isn't. Do NOT delete:**

- **`Create(object?, context)`** (`type:372`) — the general CLR→plang lift, used by the read path and every list/dict slot. Untouched.
- **The per-type static `Convert(raw, kind, ctx)` hooks** (number, text, date, …) — the destination leaves. The reader calls them; the eager from-raw route to them dies, they don't.
- **The convert-a-built-value op (case 2b)** — the per-type `Convert` hook applied to a *built item* (today's 2-arg `type.Convert` / `Build`'s core). Re-types `text "5"` → `number 5` for the three materialized-input sites (Declare, validateResponse, `set`-type-differs). Renamed/relocated, but the behavior **stays** — this is the §7 correction to the first draft's "Build/Judge dissolve."
- **`convert.OwnerOf` + the ownership table + `OwnedClr`** (`convert/this.cs:84+`) — the genuine CLR-interop seam (`Create`'s scalar lift, `Compare`, `TryConvert`).
- **`convert.OfStatic` + `_staticCache` + `Discover`** (`convert/this.cs:43,133`) — `Create`'s raw-scalar lift and the context-free deserialize.
- **`convert.Conversions.Of` (instance) + `_cache`** — **survives.** I was wrong earlier that the reflection dispatch disappears: `catalog/Conversion.cs:231` uses it to marshal a PLang value into a specific **C# type** at the action boundary (`%x%` → an `int` parameter). That is a different job (marshalling out to CLR, not construction). It stops being on the construction path; it does not die. Folding marshalling onto the reader table too is a *separate* cleanup — do not bundle it here.
- **The born-typed holder ctor** (`data/this.cs:222`) — the clean "Data is a dumb holder of an already-built value" path. This is what the construction ctor delegates into.
- **`source` + the reader registry** — these ARE the surviving door; they grow to absorb construction (Stages 1–2).

## Relationship to read-path-unification (settled — this branch lands first)

This branch **owns the value-ctor retirement** that read-path-unification's Stage 6 stub deferred. **Decided (Ingi, 2026-06-29): this branch lands first, then merges into `read-path-unification`.** So there is **no rebase gate on Stage 5** — delete freely against the current base (`3ddcdb17f`); reconciling these changes with read-path-unification's own in-flight edits to `source.cs` / the reader registry happens at *that* merge, and is read-path-unification's problem to absorb, not a constraint on this branch. Build, delete, and finish here on its own terms.

**Build in read-path-unification's shape** — it's the merge target, so matching its end-state cuts friction (verified against its full plan + 6 phases):
- Construction rides the **same** carrier + materialization door read-path-unification is building — `source` + `app.type.Create(source) → Task<(item?, Error?)>`. No parallel construction-materializer: the ctor mints a `source`, and at first use it flows through that one door. This is convergence, not a second way. Seam/name moves to expect: `Readers.Typed` → `App.Type.Reader`; `serializer/Default.cs` static `Read` **deleted** (build readers as `Reader.cs`); `MaterializeFailed` authoring **moves into** `app.type.Create(source)`.
- The `date`/`datetime`/`time`/`url` readers this branch adds **fill a gap read-path-unification also has** — its no-generic-reader rule needs every type to ship a `Reader.cs`, and it ported only 6 (path/code/object/item/table/image), not these. Build them as `ITypeReader` `Reader.cs` so they *are* its readers at merge, not throwaways — neither side writes them twice.
- **Checked, not a smell:** after merge there are two `Create`s — `Create(object?, ctx)` (lift a raw CLR value, no declared type) and `Create(source)` (materialize a declared source). Different jobs; read-path-unification keeps both (its read path uses the lift for json leaves). Not a "two ways" violation.
- This branch **answers read-path-unification's deferred Q4** (settled-question 7): `Build`/`Judge` cannot be fully deleted — the convert-a-built-value op survives.

## Settled questions (from the coder's open list)

1. **Inflow across shapes.** Confirmed: scalars double-convert (string → text → target); already-typed scalars get a redundant re-`Convert` (number → number); containers return native from `Create` and skip the coercion. The fix targets the scalar/raw-form path; containers are already correct.
2. **Two materialization mechanisms — one or two?** **One.** The reader registry and the `Convert` hooks are the same mechanism: a type's `serializer/Reader.cs` reads through (or delegates to) its `Convert` hook. Two *routes* to that hook (lazy reader / eager reflection); the eager *from-raw* route dies. No second table to build. (read-path-unification deletes the per-type `serializer/Default.cs` static `Read`; the `ITypeReader` `Reader.cs` is the surviving shape — build new readers there.)
3. **`Create(raw, type)` lazy or eager?** **Lazy** for a raw form — mint a `source`, reuse the read path. (A *built-but-wrong-type* value is the exception: it converts now, case 2b — it has no raw to defer.)
4. **Registration mechanism (source-gen a `typeName → Factory` table vs `CreateDelegate`)?** **Dissolves.** The reader registry is already that table — discovered by namespace convention (`reader/this.cs` IndexAssembly), zero per-call reflection. Do not build a second one. The per-call reflection lives only in the dying from-raw eager route.
5. **Return-`item` error semantics.** The from-raw path inherits the source-materialization failure story (`MaterializeFailed`) for free — no new exception type. **The seam moves:** read-path-unification relocates that authoring out of `source.Value`'s try/catch into `app.type.Create(source) → Task<(item?, Error?)>`; construction gets it either way, so don't hard-couple to `source.Value`'s current internals. Case 2b's convert errors on its own bad value — that's the build-validate catch.
6. **`text` raw accessor.** The from-raw path mints a `source` (which has `.Raw`), never a throwaway `text`, so the asymmetry the report flagged doesn't bite there. But it is **why case 2b converts instead of re-sourcing**: a built `text` has no clean raw (only `ToString()`/`Clr`), a built non-text has none at all — so a built-but-wrong-type value is re-typed via the hook, not turned back into a `source`. Sidestepped by *not* re-sourcing built values, not by adding a raw accessor.
7. **Can `Build`/`Judge` be deleted outright? (read-path-unification's deferred Q4.)** **No.** From-raw construction converges on `source` and that part dissolves, but re-typing an already-built value (three live sites) keeps a thin convert-a-built-value op — the per-type hook applied to a built item. Delete the eager from-raw route; keep that op (renamed). This answers the question read-path-unification's Phase 6 left open.

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

Review history, all folded above:
- **Coder v1** — four corrections: format-by-type predicate, `Reader.cs` (not `Default.cs`) template, wider reachable set + `NotSupportedException` leak, `choice` sub-task.
- **Codeanalyzer v1** — PASS + one fix: restore the dropped typed-absence arm (case 1).
- **Coder addendum v2** — the big one: the fork converts from-raw only, but **three live sites (Declare, validateResponse, `set`-type-differs) hand it an already-built wrong-typed value** that "native → hold" would silently no-op — so `Build`/`Judge` can't be deleted outright. Folded as **case 2b** (convert a built value) throughout: The shape, Invariants, the leaf-trace, the demolition (Build gutted not deleted; the 2-arg `Convert` survives), and the safety-net note. Plus the `byte[]`/`FromRaw` fix (case 3 → kind→mime).
- **Architect cross-check** vs read-path-unification's full plan — convergence confirmed (same `source` + `Create(source)` door, no parallel path, no use of a removed method); seam-alignment notes added to the Relationship section; this branch answers read-path-unification's deferred Q4.

Merge order settled (this branch lands first). The demolition worklist names the *intended* sequence (readers → source → ctor flip → reroute → delete → OBP) — the shape of the work, not committed stage files. Sequence it when ready.
