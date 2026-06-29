# Output unification — retire Normalize / Wire / WireLocal / PrWrite

**Origin:** design session with Ingi. The read path unified on `Data.Output` already (the channel
egress writes via `await data.Output(...)`). The WRITE path still has a parallel synchronous track
(STJ → `Wire`/`WireLocal` → `Normalize`) kept alive only because STJ `JsonConverter`s are sync and
can't `await Output`. This spec retires that track so there is ONE write path: each value writes
itself via `Output`.

## The one rule

**A value writes itself through `Output`. There is no second write method, no central normalizer,
no STJ converter for Data.**

- `Output(IWriter writer, View view, context)` — async, the **I/O-layer** write. The only write verb.
- `Write(IWriter)` (the old sync "bare form") is **folded into `Output`** and deleted. Ingi: prefer
  `Output` — it's the I/O-layer call; a separate sync `Write` is a needless second door.

## Dispatch is polymorphism, never `IsLeaf`

No `if (IsLeaf)` anywhere in the write path. The override IS the answer to "do I have a special
wire shape?":

```
item.Output  [base default]   = reflect via Tagged.PropertiesFor(GetType(), view)
                                 → for each [Store]/[Out] property: writer.Name(p); await p.value.Output(...)
                                 → for each property the VIEW selects (no hardcoded [Store])

leaf scalars  (number, text, bool, date, datetime, time, duration, guid, binary, choice, archive,
               null, source, …)  → override Output = write the bare value   (old Write body moves in)
dict / list / clr / signature                       → override Output (special shape; already have it)
goal / step / action                                → NO override — base reflect handles them
```

The general rule: **an object's wire form is its tagged property bag, and the View picks which
attribute set** — there is no hardcoded `[Store]`. `Tagged.PropertiesFor(type, view)` (already exists;
today it drives `NormalizeObject`) maps the View to the attribute set:

```
View.Out   → [Out]   properties   (wire, third-party-facing; [Sensitive] excluded, [Masked] → "****")
View.Store → [Store] properties   (.pr / sqlite / disk; [Sensitive] included, [Masked] ignored)
View.Debug → every public property (diagnostics)
```

The same `Output` serves every destination; the `view` flowing into `Output(writer, view, ctx)`
decides. A `.pr` write is `goal.Output(w, View.Store, ctx)` → `[Store]` props; the wire is `View.Out`.
`Tagged` moves from the deleted Normalize switch into the base `Output`.

## Everything that writes a value is an `item.@this`

The two collections that aren't yet become `item.@this` (the existing IList-cleanup todo), so they
self-write like `actions` already does:

- `goal.steps`  : `IList<Step>`     → `item.@this` (a `list`-shaped Output: array of `step.Output`)
- `action.modifiers` : `IList<PrAction>` → `item.@this`
- `table` is already `item.@this` but has neither Write nor Output — give it a structural `Output`
  (its rows/columns shape), do NOT let it fall to the reflect default.

## Fold `Write` into `Output`

Every `x.Write(w)` caller becomes `await x.Output(w, view, ctx)`:
- `directory` (writes child `path.Write`), `permission` serializer, `signature.Output` (writes
  `Algorithm`/`Nonce`/`Created`/… via `.Write`) → `await child.Output(...)`.
- `Wire.cs:335` verbatim relay → dies with Wire.

A leaf's `Output` writes synchronously and returns `CompletedTask`, so the `await`s are cheap.

## Route the `.pr` WRITE through the channel

Today: `builder/code/Default.cs:274  JsonSerializer.Serialize(goal, PrWrite)` (sync STJ). This is the
last thing forcing `Wire`/`WireLocal`/`Normalize` to exist.

Change: write the `.pr` through the **channel** (async), symmetric with how `.pr` is **read** through
the channel — `goal.Output(writer, View.Store, context)`. The goal self-writes top-to-bottom; no STJ,
no `PrWrite` options.

## `@schema` is a deliberate choice, not an STJ side-effect

`Data.Output` (this.Normalize.cs:79) already gates `@schema` + `name` on `layer && View.Store`. Once
the `.pr` write is `Data.Output`, whether a `.pr` carries `@schema` is a one-line decision there —
not an unavoidable consequence of reusing the wire converter.

**DECIDED (Ingi): the `.pr` stays structural — no `@schema` on disk.** A param is recognized as a
Data by its `List<Data>` schema position (STJ/the reader knows the slot type), not by a marker.
`@schema` is reserved for the **wire**, where a bare JSON object is genuinely ambiguous. So the Store
view emits `{name, type, value, properties}` without `@schema`; the Out/wire view emits it.

## Deletions (the payoff)

- `Wire` (the STJ JsonConverter), `WireLocal` (+ both `[JsonConverter]` attrs on Data/Data<T>).
- `Normalize` / `NormalizeValue` / `NormalizeObject` (this.Normalize.cs's central type-switch).
- `PrWrite` (the STJ `.pr` options) + the `json.Converter()` Data path.
- `item.Write(IWriter)` (folded into `Output`).
- The `IsLeaf`-as-fork reads in the write path. (Build-path `IsLeaf` forks — type/this.cs:171,223 —
  are the same smell but out of scope here; follow-up.)

## Migration order (each step builds + both suites green)

1. Base `item.Output` default → reflect via `Tagged.PropertiesFor` (+ recurse via `Output`).
2. Move each leaf's `Write` body into its `Output`; convert `Write` callers to `await Output`.
3. `goal.steps` + `action.modifiers` → `item.@this`; `table` → structural `Output`.
4. Route the `.pr` write through the channel (`goal.Output`, Store view).
5. Delete `Wire`, `WireLocal`, `Normalize*`, `PrWrite`, `item.Write`.
6. Verify: `.pr` round-trips (write→read) through the channel; Data + Wire suites green; the 15 pass
   because the value is written by its own `Output`, not dropped.

## Why this fixes the 15 (the WireLocal-deletion regression)

The 15 failed because deleting WireLocal dropped the param `value` on write (Data.Value is
`[JsonIgnore]`; only WireLocal wrote it). With the `.pr` write going through `Output`, the value is
written by the value's own `Output` — there's nothing to drop, and no WireLocal to delete-and-break.

---

# OBP structure — the implementation guide (code from this, not reactively)

## The principle
Serialization is **behavior owned by each value type**, named `Output`. There is no central dispatcher
(Normalize — deleted), no second verb for the same behavior (Write — folded in), and no type-fork
(`IsLeaf` — gone from the write path). **Polymorphism IS the dispatch.**

## Ownership — who owns `Output`
| owner | `Output` does | override? |
|--|--|--|
| **base `item.@this`** | reflect its tagged bag (`OutputTagged`) — the general "an object IS its View-selected properties" rule | the DEFAULT |
| **leaf scalar** (number, text, bool, date, datetime, time, duration, guid, binary, choice, archive, null, source) | write the bare value (the old `Write` body) | yes |
| **container / special shape** (dict→`{k:v}`, list→`[e]`, clr→host, signature→layer) | its own shape | yes |
| **goal / step / action / any plain structural item** | — (base reflect handles it) | **NO** |

The single question is "**am I a plain tagged bag, or a special shape?**" — answered by *overriding or
not*, never by an `if (IsLeaf)`.

## The reflection→wire boundary (the one irreducible adapter)
`OutputTagged` reflects C# properties via `Tagged.PropertiesFor(type, view)` (the **View** picks the
attribute set; `Entry.WireName` owns the casing — computed once, never re-cased at a call site). Each
reflected value:
- a plang `item.@this` → `await value.Output(...)` — it self-writes (**polymorphism**).
- a C# scalar (string/bool/int/date/…) → `writer.Value(v)` — the **writer** owns primitive rendering.
- *[bridge]* a raw C# collection → array of self-writes — **deleted** once steps/modifiers/parameters
  become `item.@this` lists.

This 2-branch (plang-value vs C#-scalar) is the **reflection adapter**, NOT a domain switch: it exists
only because C# primitives can't self-write, and it does **not** grow per domain type. That is the line
between "acceptable boundary" and "recreating Normalize."

## Smells to guard while coding (the 8 + the ones specific here)
- **central type-switch** — that WAS Normalize. Don't recreate it; `WriteReflected` stays 2-branch.
- **type-fork** — no `IsLeaf` in the write path. Override-or-not is the dispatch.
- **second method for one behavior** — no `Write` beside `Output` (fold it in).
- **call-site transform (#5)** — casing lives on `Entry.WireName`, not re-derived per caller.
- **courier touches `.Value` (#7)** — `Output` reads its OWN value (a leaf renders itself) or recurses
  via a child's `Output`; it never reaches into a child `Data.Value`.
- **construct-then-stamp / allocate-then-transform** — the writer streams; no intermediate tree
  (that was Normalize's `dict` tree). Write straight to the writer.

## End-state shape (what "done" looks like)
- `item.Output` base = reflect; leaves + containers override; **goal/step/action have NO override**.
- `steps`, `modifiers`, `action.parameters` are `item.@this` lists (self-write; the bridge branch is gone).
- `table` has its own structural `Output`.
- **Deleted:** `item.Write(IWriter)`, `Normalize`/`NormalizeValue`/`NormalizeObject`, `Wire`,
  `WireLocal`, `PrWrite`, the `IsLeaf` write-path fork.
- `.pr` write = `goal.Output(writer, View.Store)` via the channel (symmetric with the read) — unsigned,
  structural (no `@schema`; a param is a Data by its `List<Data>` position).

## Migration (green at each step; OBP-check each)
1. ✅ base `OutputTagged` capability + goal/step/action dormant overrides.
2. ✅ Store name-gating fix; `Entry.WireName`; `WriteReflected → writer.Value`.
3. ✅ **`.pr` write → `goal.Output(Store)` via the channel** — round-trips, suite-validated.
   (New `Output`s landed: `actions`, `GoalCall`, `type`-entity, variable Store-write; `Data` property
   routes through `Data.Output`; variable-ref resolution gated to non-Store.)
4. ✅ **WireLocal DELETED** — both `[JsonConverter]` attrs gone; the one obsolete test (context-less
   `Deserialize<Data>` pin) updated to a context-ful read. The output-unification made this a 1-test
   change, not 15.
5. **NEXT — retire `snapshot.Serialize` (the last static `Wire.Write` reference).**
   `snapshot.Serialize` (`snapshot/this.Wire.cs:35`) does `JsonSerializer.Serialize(Data, SnapshotOptions)`
   where `SnapshotOptions` registers `new Wire(Store, sign:false)` — the ONLY remaining static
   `Wire.Write` (→ `Normalize`) reference.
   **Finding:** it is reached only via `App.SnapshotToWire`, which itself has **no real caller** — only a
   reflection "still-exists" test (`App_SnapshotToWire_StillExists`). Real snapshots round-trip through
   the channel's `Output` (the Data-suite snapshot tests never touch `Serialize`). So it is effectively
   **dead legacy**.
   **Cost (traced, bigger than a domino):** the snapshot is a **dynamic key-value bag** —
   `_entries: Dictionary<string, object?>` + nested `_sections` — NOT a `[Store]`-tagged class. So
   `OutputTagged` is wrong for it (reflects `Entries`/`EntryCount`; `WriteReflected` writes a dict as an
   array and does not deep-reflect arbitrary `object?` entries — that deep reflection is exactly what
   `NormalizeObject` provides and what `Normalize` is still doing for the snapshot today). So the
   snapshot needs its **own** custom `Output` (write `_sections` + `_entries` as an object, recursing
   each entry value), not `OutputTagged`. And the Data-suite snapshot tests round-trip **in-memory**
   (never call `Serialize`), so this serialization is **untested here** — verification needs the
   hang-prone Runtime suite. **So `Normalize`/`Wire.Write` are the LIVE snapshot serializer (via the
   legacy `snapshot.Serialize`), not dead code.** Retiring them = (a) write a custom `snapshot.Output`
   over `_sections`/`_entries`, (b) extend `WriteReflected`/the writer to deep-handle `IDictionary` +
   arbitrary C# entry values (or keep that as the reflection adapter's job), (c) async-ify
   `Serialize`/`SnapshotToWire`, (d) verify the snapshot round-trip in the Runtime suite. A deliberate
   follow-up, NOT tail-of-session work. (`Wire` stays as the read bridge regardless.)
6. ✅ **snapshot → Output** (bare, via `serializer.Default`); **`Wire.Write` gutted** (read-only
   converter — 3 render tests migrated to `SerializeAsync`); **`EndRecord` off `Normalize`**. Result:
   **`Normalize` has ZERO production callers — it is retired from the prod write path.**

7. **Physically delete `Normalize`/`NormalizeValue`/`NormalizeObject`** — gated on two real things
   (found when attempting it):
   - **Cycle/depth guards.** `Normalize` had `CycleError` (visited-set) + a depth cap. `Output`
     recurses (`Data.Output → _item.Output → children`) with only a self-referential-*variable* guard
     (`_outputDepth`, cap 50) — **no general cycle/depth guard**. A cyclic value graph would stack-
     overflow under `Output` where `Normalize` threw a typed error. Add cycle/depth guards to the
     `Output` recursion BEFORE deleting `Normalize` (and before trusting `Output` on untrusted graphs).
   - **8 test files** call `Data.Normalize` directly (`NormalizeTreeShapeTests`,
     `NormalizeCycleAndDepthTests`, `NormalizePipelineHelper`, …) — obsolete (they pin the removed
     tree); migrate the cycle/depth ones to assert `Output`'s guards, delete the pure tree-shape ones.
   `Normalize` stays as prod-dead-but-present code until then (harmless, still green, still test-pinned).

8. leaves override `Output` (fold `Write` in); flip base `Output` → reflect; remove the
   goal/step/action overrides; delete `Write`. (`PrWrite`: migrate its 2 test refs + `Utils.Json`, delete.)
