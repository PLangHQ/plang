# Host-carrier spec — fix `clr`, don't delete it

**Status:** supersedes the v15 "remove `clr` → hard error" decision and the
`external` deferral. Settled with Ingi 2026-06-16 after his gut flagged
"itemizing the engine" as over-engineering.

## The decision in one line

The engine handles (`%!app%`, `%!callStack%`, `%!serializers%`, `%!channels%`,
`%!variables%`, `%!context%`, `%!trace%`, `%!test%`) are **windows into the live
host**, not PLang values. They ride a **single closed foreign-object carrier**
that reflect-reads, reflect-writes-where-a-setter-exists, and reflect-serializes.
That carrier is `clr`, **fixed** — not deleted, not turned into per-class items.

## Why this, not items

`item.@this` is the value lattice: truthiness, narrowing, `ICreate`
("construct yourself from a value"), a leaf/wire form, immutable-rebind. None
of that means anything for an `Engine` or a `CallStack`. Forcing them into
`item` only to pass `Lift` makes a live system pretend to be a value. That
mismatch is the over-engineering. A host object needs three reflective
operations, nothing more.

Items stay for genuine PLang values (text, number, dict, list, …) and the
domain entities that really are values (`goal`, `step`, `error` — already
items; leave them).

## What the carrier is

A small, **closed** type holding one live host object and owning three
operations over it by reflection. "Closed" = no consumer ever sees the carried
object except through the carrier's own door or the explicit `.Clr<T>()` exit.

### The three operations

1. **navigate (read)** — reflect-get the named property; re-wrap the result in a
   `Data`. A nested host object becomes another carrier; a `string`/`int`/etc.
   becomes a real item (it Lifts on the way back). Deep paths
   (`%!callStack.Current.Caller.Tags.owner%`) just recurse the carrier.

2. **write** — reflect-set **iff the property has a setter**. No setter → the
   write declines (returns false; the caller surfaces the failure). Read-only
   vs writable is **not a map we author** — it is inherited from the C# shape.
   `CallStack.Current` is `{ get; }`, so it is read-only, full stop;
   `App.Serializer` is writable iff it is `{ get; set; }`.

3. **serialize** — the carrier's wire form = reflect its carried object's
   `[Out]` properties into the writer (the property bag). This **is** the
   snapshot: `write %!app% to %snapshot%` walks the engine's `[Out]` graph.
   Replaces today's "clr has no wire form → throws".

### Behavior trace

```
- read %!app.callStack.Current.Depth%      / navigate("Current")→carrier; navigate("Depth")→number
- set  %!app.callStack.Current.Depth% = 5  / write: Depth has no setter → declines (correct)
- set  %!app.serializer% = "json"          / write: setter exists → reflect-set the LIVE engine
- write %!app% to %snapshot%               / Write(IWriter): reflect [Out] graph → property bag
```

Note: writes **mutate the live engine in place** — you are configuring the real
running system, not a clone. (This is the deliberate divergence from the old
`external` clone-on-write note, which only ever applied to genuinely foreign
*data* you are handed and choose to treat as an immutable value — a separate,
later concern, not the engine handles.)

## What changes in `clr` (the fix)

Today `clr` is **half-built** — its own comment admits the door was left open:
*"Tightening the door to answer the carrier itself is deferred — too many
raw-shape consumers remain."* Two concrete defects fall out of that:

- **Defect 1 — open box.** `Peek()` returns the raw carried object, so the
  engine reaches *past* the carrier and branches on `is clr` / `.Value is X`
  (OBP smell #7). The leak is the half-migration, not the concept.
- **Defect 2 — no wire form.** `Write(IWriter)` throws, which blocks every
  snapshot.

The fix:

1. **Own navigate** — add the carrier's reflect-get + re-wrap (move the host
   reflection out of the generic `Object` navigator and into the carrier).
2. **Own write** — add reflect-set-if-setter; decline otherwise.
3. **Own serialize** — `Write(IWriter)` reflects `[Out]` props instead of
   throwing.
4. **Close the box** — `Peek()` returns the carrier itself; the only raw-object
   door is `.Clr<T>()` (leaf actions only). Then nothing in any relay layer can
   branch on the carried value — the `is clr` smell cannot recur.
5. **Delete the courier-label cruft** — `_declared` / `Labeled` /
   `_declaredStrict` (schema-layer transitional state the comments already mark
   as dying).

## What keeps it from re-rotting

The discipline already on the books: **OBP Rule #9 — only leaves touch
`.Value`.** Today it is violated because the box is open. Once the carrier owns
navigate/write/serialize, no courier *needs* to look inside, so the rule
actually holds. A regression is then a single grep: `\.Value (is|as|switch)`
outside leaf files.

## Naming

`clr` hard-codes ".NET" into a runtime-independent vocabulary. Rename to
`host` (or `external`) is **cosmetic** — do it whenever; it is not the
substance and can come last.

## Consumer inventory (scan 2026-06-16) — sizing the close-the-box work

**The decisive finding: not one production reach-in reads a live host/engine
object.** Every `is clr { Value: … }` branch reads *parked data* (a nested
`Data`, a raw `JsonElement`, a raw container). The genuine host use — the
`%!...%` engine handles — navigates through the generic reflection navigator
(`variable/navigator/Object.cs`, reflects over `Peek()`) and branches on `is clr`
**nowhere**. So closing the box does not touch engine navigation at all.

### A. Construction sites — KEEP (carrier is correct here)
- `data/this.cs:252` — Lift fallback `new clr(v)` (returns the fixed carrier)
- `data/this.cs:548` — `SetValueDirect` `new clr(value)`

### B. Open-box reach-ins (7) — all read PARKED DATA, not host objects
Group by carried shape; each shape should land as a real item at Lift, after
which the reach-in is dead code and gets deleted:
- **nested `Data`** (the Data-in-Data / SetValueDirect courier debt):
  - `data/this.Navigation.cs:291` — `clr { Value: @this dataVal }`
  - `llm/code/OpenAi.cs:951` — `clr { Value: data.@this d }`
- **raw `JsonElement`** (should be a dict/parsed item):
  - `llm/code/OpenAi.cs:1013` — `clr { Value: JsonElement }`
  - `test/discover.cs:294` — `clr { Value: JsonElement }` (goal-name read)
- **raw container** (Lift already narrows these to native dict/list, so likely
  near-dead already):
  - `data/this.cs:500-503` — `clr { Value: IDictionary/IList }` (StampedForm)
  - `llm/query.cs:33` — `clr { Value: IList }` (Messages validation)
- **raw `string`**:
  - `data/this.cs:491-494` — `clr { Value: string }` (StampedForm template scan)

### C. Courier-label cruft (5) — DELETE with `_declared`/`Labeled`
All in `type/this.cs` `Judge`: `:451, :452, :464, :482, :483`
(`carrier.Labeled(...)` / `new clr(value, Name, Kind, Strict)`).

### D. Tests
- `DataTests.cs:864, :868` — cast-and-read `((item.clr)…).Value` (Data-in-Data).
- Three files carry `[Skip]` reasons that assumed clr *deletion*
  (`NormalizeTreeShapeTests`, `NormalizeFilterTests`, `DataTests`) — revisit:
  the "delegate parks in clr and the carrier leaks" skip is *fixed* by closing
  the box, not by deleting clr.

### What this means for sequencing
Close-the-box ≈ **3 data-shape families that should never ride the carrier**
(nested `Data`, `JsonElement`, raw containers) → make each land as a real item
at Lift → their reach-ins die → delete them. Plus delete the courier-label set
(C). After that, the carrier holds *only* genuine host objects, `Peek()` can
return self, and the generic navigator is the single reflective door. The
engine-handle navigation needs **no** changes — it never branched on the box.

## Out of scope (explicitly deferred, unchanged)

- Clone-on-write value semantics for handed-in foreign *data* (the original
  `external` story). The engine handles do **not** want this — they mutate in
  place. Revisit only if/when a real "treat this host POCO as an immutable
  value" need appears.

## Migration impact vs v15 plan

- **Dropped from v15:** "flip Lift fallback to a hard error", "delete `clr`",
  and the whole bucket-1 "engine handles → items" conversion. The Lift fallback
  **stays** and returns the (fixed) carrier.
- **Kept from v15:** buckets 2 & 3 already done (loop result → dict,
  builder.warning → dict, operator test sites → string). Those were genuine
  plain-data values wrongly parked in `clr`; making them dicts was correct
  regardless.
- **Still true:** a non-item *value* must never ride a value slot — but a host
  object is not a value; it rides the carrier, which is itself an item-shaped
  closed box that renders itself. No producer parks raw data in the carrier;
  the carrier is reserved for live host objects with no plang type.
