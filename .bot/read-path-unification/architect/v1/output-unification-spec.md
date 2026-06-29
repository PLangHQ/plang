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
                                 (the general rule: an object's wire form IS its tagged property bag)

leaf scalars  (number, text, bool, date, datetime, time, duration, guid, binary, choice, archive,
               null, source, …)  → override Output = write the bare value   (old Write body moves in)
dict / list / clr / signature                       → override Output (special shape; already have it)
goal / step / action                                → NO override — base reflect handles them
```

`Tagged.PropertiesFor(type, view)` (already exists; today it drives `NormalizeObject`) is the
attribute-driven selector — `[Store]` for the `.pr`/Store view, `[Out]` for the wire, `[Masked]`
honored. It moves from the deleted Normalize switch into the base `Output`.

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
not an unavoidable consequence of reusing the wire converter. (Recommended: `.pr` stays structural —
params are recognized as Data by the `List<Data>` schema position, `@schema` reserved for the wire.)

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
