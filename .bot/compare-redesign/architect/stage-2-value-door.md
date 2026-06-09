# Stage 2: The typed value door + the `.`/`!` resolver

**Goal:** One async, lazy door that returns the **typed value** (a PLang `item` subtype), never raw CLR; and the two access planes — **`.` = the content/data**, **`!` = the value's own properties + the envelope**.
**Scope:** The door, `Peek()`, the `_raw` → `binary`/`text` rung, the `.`/`!` resolver, the no-`ToRaw` rule + gate stub. The reference types (Stage 3), per-type compare (Stage 4), and the full `!`-surface typing (Stage 7) are separate.
**Deliverables:**
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async only when it must **read** (I/O). Remove the public sync `.Value` property; keep a private `_value` + `_present`.
- **The async value source (the largest net-new piece — Part A).** Today `Materialize()` is **sync** (`this.cs:316`, parses an in-memory `_raw`); the only async content load is per-type `ILoadable.LoadAsync()` (`image`). The async **read** seam is net-new (`ILoadable` folds into it); the **parse** logic folds *into* the door — `Materialize()` as a separate sync method **disappears**.
- **Navigation is async, `ValueTask`-shaped — lazy read survives.** The chain `Data.GetChild`/`GetChildValue` → `Variable.Get` → `Variable.Resolve` → `Value()` is `ValueTask` (sync-completing, zero-alloc when in memory; awaits only the **first** content read of a reference). **No I/O at `read X`** — the bytes load on first navigation, always reached through an `await`. Most callers are async handlers already.
- **The three sync surfaces that can't `await`, each handled** (coder v3 finding A): **(1)** source-gen lazy param getters → `Action.GetParameter<T>(name)` returns a **lazy `Data<T>`** (no navigate), handler resolves via `await this.Param.Value()`; **(2)** `ToString`/`Equals`/`GetHashCode` → read the already-materialised backing only, never navigate; **(3)** template render (Fluid) → already async, materialise up-front at `SetValue`. See the design section.
- `ScalarValue` → `Peek()` (the current rung, no forced materialise).
- `_raw` byte slot **removed** — a bare value's unparsed form is a `binary`/`text` value (the rung).
- The `.`/`!` resolver: **`.` = the content** (for a reference, `.` forwards into its headline content); **`!` = the value's own properties + the envelope**, with the reserved core (`@schema`/`type`/`error`/`success`) protected and **`!` resolved chain-wide** (`!type` = headline, `!type.list` = the chain, `!facet`/`!facet!prop` walk `.Is()`). See "The two planes."
- No generic `item.ToRaw()`; `text.Value` (public raw string) → private. Gate **stub** (warning) → Stage-7 error.
- **`data.Type` getter → `return _type;`.** Once the door normalises `_value` to a typed `item`, the type (name + `number`-kind) is stamped at **construction**, from the value itself — so the lazy CLR-sniffing block in the getter (`leaf.ToRaw()…GetType()` + name-mapping + kind-stamping) is **deleted**, not migrated. The narrow updates `.Type` through the setter. (The other `ToRaw` sites are Stage 6.)
- **The value is always a typed PLang value** — set at creation, never a bare C# `string`/`List`/`Dictionary`. A `%var%` → `text`, JSON → native `dict`/`list`; close the leftover raw-C# paths so the door's typed promise holds everywhere.
**Dependencies:** None to start. **Stages 2–6 are one green unit** (green gate at the 2→6 boundary).

## Design

**Part A — the async value source: `Materialize` disappears (do this first; it's the bulk, net-new).** `await _source.Read()` reads as if it exists — it doesn't. There is **no async I/O read path on `Data`** today; `Materialize()` (`this.cs:316`) is sync and parses an *already-in-memory* `_raw`, and the only async content load is per-type `ILoadable.LoadAsync()` (`image` only). The door is **the one async path**, and it does both steps:
- **read (async, net-new)** — fetch a file/url's bytes into memory; `ILoadable.LoadAsync()` folds into this (a reference's source does the scheme I/O). Almost nothing exists; design it here.
- **parse (folds into the door)** — bytes → `dict`/`list`/scalar, via the reader registry / `Narrow`. This was the sync `Materialize()`; it moves *inside* the door's load path, so `Materialize()` as a separate method **goes away**. The parse **narrows** the value: `.Type` is mutated **in place** on the same `Data` (no new instance), and the prior type is retained so `.Is()` walks the accumulated chain (`item|file|dict`). Single-storage — the parsed item replaces the raw, it is not stored alongside it (the reference specifics are Stage 3).

**Navigation is async (`ValueTask`), lazy read survives, sync surfaces handled (coder v3 finding A).** Keep the read lazy — **no I/O at `read X`**; bytes load on **first navigation**, reached through an `await`. The chain `Data.GetChild` → `Variable.Get` → `Variable.Resolve` → `Value()` is `ValueTask` (sync-completing in memory; awaits only the first content read). Most callers are async handlers already (`output/write`, `file/read`, `list/*`, `variable/set`, `GoalCall`, the async condition/`Compare` pipeline) — they just `await`. The work is the **three sync surfaces that can't `await`**, each with a technique:

- **Source-gen lazy param getters.** A C# property `get` can't `await`. So `Action.GetParameter<T>(name)` hands back a **lazy `Data<T>`** — wraps the param `%var%`, sets context, does **not** call `As<T>`/navigate (sync, cheap) — and the handler resolves with `await this.Param.Value()` in its async `Run`; navigation + the content read land in that `await`. This collapses the old `__ResolveData(name).As<T>(Context)` into one typed call — the OBP cleanup: it removes the fetch-raw-then-convert smell **and** the redundant `__ResolveData` wrapper (`GetParameter` already returns `NotFound`, and already takes — then ignores — `Context`). The `AsT_Impl` resolution body moves *into* the async `.Value()`.
  - **Consequence — the resolution-error guard moves *after* the await (coder v4).** Today handlers guard eagerly: `if (!Path.Success) return Path;` *before* the first `await`, because the getter resolved on access. With the lazy `Data<T>`, resolution (and its `.Success`/`.Error`) only fires at `await Param.Value()` — so a pre-`await` `if (!Param.Success)` would silently inspect the *unresolved* Data and stop guarding (a bad scheme / unset `%var%` / convert failure slips past and surfaces later as an NRE on `.Value!`). So the ~42 `param.Value!` sites migrate as **await → guard → use**: `var p = await Path.Value(); if (!Path.Success) return Path; … p …`. The guard-reorder is part of each site's migration, **not** just the `.Value` → `await .Value()` swap.
- **`ToString` / `Equals` / `GetHashCode`.** Sync framework methods — read the **already-materialised** backing only (a field read), never navigate or parse (throw if asked to do more). So they never enter the async chain.
- **Template render (Fluid).** Already async (`RenderAsync`); materialises param values up-front at `SetValue` (`FluidValue.Create(await kvp.Value.Value(), …)`), then renders over **in-memory** views — render-time navigation is sync over materialised dicts, and a nested reference is a `path` (a location), not a hidden read.

The one sync-navigate-inside-compare site (`list/this.cs:250`) becomes the **two-phase sort** (Stage 6): keys materialised in async phase 1, sync compare on materialised keys. So the rule: **async to read / navigate / produce; sync only to read what's already in hand** — and the *only* place a read fires is behind an `await`.

**Secondary (coder v3 B, C):**
- **B — the in-place `.Type` narrow is read-causes-write; name the semantics.** A narrow on a **live variable's** Data is *meant* to be visible (the variable narrowed to `dict`). The parse is **idempotent**, so two async navigations racing the same un-narrowed reference do redundant work, not corruption (last write wins on `_type`/`_value`); a transient courier/clone narrows its own copy. Stated in Stage 3 so the implementer doesn't pick aliasing by accident.
- **C — confirm nothing reads the envelope `name` on the wire read-path** before dropping it (`FromWireShape`/`TypeFromWire` `this.cs:781,793`, the nested-Data recognizer, `ResolveParameter`/nested-dict keying). A nested `Data` takes its name from the **dict key**, not a `name` field. Grep-and-verify, not a code change.

**The door.** `ValueTask`, not `Task`: the common case (value already in memory) must allocate nothing, and it's the hottest accessor. Returns the typed value (see the promise scope below).

```csharp
private object? _value;     // a PLang item subtype (or null)
private bool _present;
public ValueTask<object?> Value()
{
    if (_present) return new ValueTask<object?>(_value);   // in memory: sync, zero alloc
    return Load();                                          // pending: async read + parse, allocates only here
}
```
`ValueTask` rule: **await once** (no store-and-await-twice, no `.Result` before completion). Back it with an analyzer/grep gate, not just prose — easy to violate in a loop at hundreds of sites.

**The two planes — `.` = content, `!` = the value's properties + envelope.** The wire makes the split concrete:

```json
{
  "@schema": "data",
  "type": { "name": "dict" },                          // headline (config narrowed file → dict on examination)
  "value": { "name": "", "size": 10 },                 // the content — what . navigates
  "properties": { "path": "/config.json", "size": 28 } // the file facet's metadata — what ! reaches
}
```

- **`.` = the `value` slot (the content/data).** `%config.size%` → `10` (the content's field); `%config.name%` → `""`. `.` navigates the **headline** type's content; for a reference whose content narrowed in (`file` → `dict`), that's the parsed data directly — `%config.database.host%`, no phantom `.content` wrapper.
- **`!` = the value's properties + the envelope, resolved across the whole identity chain** (rule 7). `%config!file!size%` → `28` (the file facet's byte size, in `properties`); `%config!file!path%` → the location; `%config!type%` → `dict` (headline); `%config!type.list%` → `[dict, file, item]`; `%config!cost%` → the bag. A value-type's own properties serialise **into the `properties` bag** and are reached by `!`.
- **The sigil picks the plane**, so the content's `size` (`.size` → 10) and the file's `size` (`!file!size` → 28) never collide — different slots, different sigils. And **`!file` resolves whether or not `config` narrowed** (it's in the chain on both branches), so the access never depends on runtime flow — headline-only resolution was the footgun.
- **Reserved core** (a value-type may **not** declare a property with these names): `@schema`, `type`, `error`, `success`. `@schema` is also blocked as a data key (it's the wire marker; `@` isn't a legal C# identifier anyway). **`name` is removed** from the envelope — it was the binding label, not the value's; dropping it stops the wire carrying the variable name and frees `name` as an ordinary field (so `%config.name%` is the content's, with nothing to shadow it). `name` was already excluded from the signed hash, so this is consistent.
- **`!` resolution (chain-wide, not headline-only):** reserved core first (`@schema`/`type`/`error`/`success`, protected — unshadowable), where `%x!type%` is the headline type and `%x!type.list%` the accumulated chain `[dict, file, item]`; else **walk the value's identity chain** (`.Is()`) for the property / the `properties` bag. So `%x!cost%` is **kept** (the bag), `%x!file!size%`/`%x!file!path%` name a facet (valid whenever `x.Is(file)`), `%x!error%` is the protected envelope. Resolving against the headline alone is wrong — a value that has narrowed *past* a facet, or *not yet to* it, must still answer `!facet`; that's why resolution walks the chain.

**Kinds are not values.** `json`/`csv`/… are *kinds* — they pick a deserializer that turns bytes into an `item` (`dict`/`list`). The door's parse step is "bytes → item via the kind's deserializer"; there is no "json value." A content key named `type` is fine — it lives in `value`, reached by `.type`; the envelope's `type` is `!type`.

**The value is always a typed PLang value — close the leftover raw-C# paths.** This is already true for born-native literals (`set %x% = 5` → `number`) and the typed direction is mostly in place: a `%var%` reference rides as `text` (`this.cs:146`), and JSON ingestion builds native `dict`/`list`. A few old paths still leave a bare C# `string` or `Dictionary`/`List` in the value slot — close them, so `Value()` **always** returns a typed `item`. The value is typed at creation; the only laziness left is **narrowing** (a typed value refining to another typed value — `file`→`dict`, finding 4 — never raw→typed). So the promise is **unconditional**, not "when the slot happens to be typed." This is what lets `data.Type` be `return _type;` (finding 5) and the narrow refine a type that's already there.

**No generic `ToRaw` — raw is private.** Raw CLR leaves a type only through its own `Write(IWriter)`, `As<T : item>` (type→type, returns a typed value), and gated per-type interop accessors (`path.Absolute`, Stage 7). `item.ToRaw()` gone; `text.Value` public-raw → private. Gate as a **warning** here; Stage 7 flips it to error. The one *internal* consumer today — the `data.Type` getter — drops it outright: with `_value` always typed (door normalisation), `data.Type` is `return _type;`, type stamped at construction, not sniffed from CLR.
