# Read-path unification — design + dual-path map

**Branch:** `read-path-unification` (from `context-never-null`)
**Author:** coder, for architect review
**Status:** design only — no code yet. The point of this doc is the *dual-path map* (§3): every place where two code paths do the same job and should be one. Ingi asked me to flag them; architect to spot more.

---

## 1. Why this exists — the triggering trace

`context-never-null` made `type.@this.Create(raw, context)` throw on a null context, and made the value types born-with-context. The last blocker is the **Wire read path** (`Data` deserialization). Trying to make the Wire's value births carry context regressed 15 core tests (`RunGoalAsync_*`, `ResolveValue_*`, `FilePaths_*`, `Defaults_*`). The trace showed why:

The `Data` ctor reconciles a value to its declared type two different ways, **forked on context-presence**:

```
type is declared, not polymorphic:
    if (_context != null)  _type = type.Build(_type)    // EAGER — resolve now
    else                   _type = type.Judge(_type)    // DEFERRED — keep raw, resolve at .Value()
```

`type.Build` calls `Convert(value, Context)` **at read time**. But the values that must defer to runtime can't resolve then:

- `path "../sub/x.txt"` — relative resolution needs the **goal folder** (runtime), so eager `Build` declines → null.
- `%ref%` / variable — must stay a reference and resolve at `.Value()`, not be `Convert`ed to a literal now.

The ctor comment says it outright: *"the kind-blind reconciliation (Judge) is still the home for variable-name targets and %ref% templates, which Build does not yet handle."* So `Judge` (the **no-context** branch) is the one that actually worked — and `context-never-null` makes it unreachable, routing everything through the incomplete `Build`.

**Conclusion:** the `Build`/`Judge` fork is *dead code created by the nullable-context assumption*. Under context-never-null there is no "no-context" branch. The honest model (Ingi): **every plang value is lazy — it resolves only at `.Value()`** — so there should be **one** read path, and it should defer.

---

## 2. Target state — one lazy read path

Everything the Wire reads becomes a **`source`-backed `Data`** (the existing lazy carrier, `app/type/item/source.cs`). A `source` holds `{raw, type, kind, context, template}` and materializes **on `.Value()`** by asking the declared type to make itself from the raw. No eager resolution at read time; no `Build`/`Judge` fork; `%ref%`/path/scalar/dict all ride the same slot.

`source.Value()` collapses to one dispatch (today it is 3 branches — see §3-F):

```
read   = Readers.Of(_type, _kind)                          // TOTAL — never null (clr floor, §3-A/E)
parsed = read(_raw, _kind, new ReadContext(Context, _template))
return Create(parsed, Context)                             // ← this second step should disappear (§3-B)
```

…and with the type-reader returning the born value directly (§3-B), it is literally:

```
return Readers.Of(_type, _kind).Read(_raw, _kind, new ReadContext(Context, _template))
```

For this to hold: (1) `Readers.Of` is **total**, (2) the reader **returns the born value** (no `Create` finish), (3) the **type owns** its raw→value materialization (incl. the `%ref%`/template judgement — Ingi's call: it lives in the type's reader, option (a)), (4) the Wire stops deciding eagerly and just hands raw+type+context+template to a `source`.

---

## 3. DUAL-PATH MAP  (the part to review)

Each item is **two paths doing one job**. The unification is to collapse to the right-hand one.

### A. `Readers.Of` (delegate) **vs** `Readers.Typed` (ITypeReader)
`app/type/reader/this.cs`. Two parallel registries + lookups:
- `Of(type,kind) → Read` delegate, `Read = object? (object raw, string? kind, ReadContext)` — returns an **intermediate** `object?`.
- `Typed(type,kind) → ITypeReader`, `Read<TReader>(ref reader,…) → item.@this` — returns the **born value**.

The `Typed` XML doc literally says *"Null when the type has not been converted to a typed reader yet (the caller falls back to `Of`)."* → **mid-migration; two registries, two lookups, two return shapes.** Target: one (`Typed`), `Of` retired.

### B. wire→value (reader) **vs** value→value (`ICreate` / `Convert`) — and the `Create(parsed, Context)` seam
The `Of` reader returns `object? parsed` (a half-decoded thing), so `source` must call `Create(parsed, Context)` to finish — **two steps, two doors.** Meanwhile `ICreate<T>.Create` / the type's `Convert` hook is a *separate* creation door (value→value) reached by `Data.Value<T>()`. So a type's "make yourself" logic lives in **up to three** places: its `Read` delegate, its `ITypeReader`, and its `ICreate`/`Convert`. Target: **one creation door per type** — the reader returns the born value; `Create(parsed,Context)` disappears; `ICreate`/`Convert` and the reader converge (or the reader IS the type's `Create(raw, ReadContext)`).

### C. `type.Build` **vs** `type.Judge`  (the dead context fork)
`app/type/this.cs`. `Data` ctor picks Build (eager, context) or Judge (deferred, no-context) **based on whether context is null**. Under context-never-null the Judge branch is unreachable, yet Judge is the only one that handles path/%ref%. Target: **delete the fork**; one path that defers (via `source`), Build/Judge logic absorbed into the type's reader at `.Value()`.

### D. eager Wire births **vs** lazy `source` slot  (inside `Wire.ReadBody`)
`app/data/Wire.cs`. A read value takes one of several **eager** branches — `var-match → variable.Reference(...)`, `typed.Read(...) → born`, `Build` fallback — and only `IsDeferrableShape(typeRef)` (object/table) takes the **lazy** `FromRaw → source` path. So "shape" types defer and everything else resolves eagerly: **two policies in one method.** Target: **everything is `IsDeferrableShape`** — every value → `source`; the eager branches (incl. the var-match `%ref%` special-case) collapse into the type's reader at `.Value()`.

### E. `Readers.Of` returns **null** **vs** a clr floor
`Readers.Of` ends in `return null` (`this.cs:79`). Callers null-check and fork (source branch 2/3). `clr` (`app/type/clr/this.cs`, `@this(object value)`) wraps any raw and IS a plang type — the natural floor — but **there is no clr reader registered**. Target: `Readers.Of` **never null** — a one-line clr reader (`raw → new clr(raw)`) is the bottom of the lattice. Same principle as context-never-null, applied to readers: a value is always *some* plang type, even if that type is "host object".

### F. `source.Value()`'s three branches
`app/type/item/source.cs:74-110`. (1) `read != null` → reader; (2) string raw → `Create(type).Convert(s)`; (3) byte[] typed binary → `new binary(...)`. Branches 2 and 3 exist **only because those types have no reader** (so `Of` returns null). They are missing-reader workarounds papered over in `source`. Target: with §E (total) + §3-B (born value), `source.Value()` is **one** line.

### G. `WireLocal` (context-less Wire) **vs** the context-ful Wire  (the context-never-null hook)
`app/data/WireLocal.cs` + `[JsonConverter(typeof(WireLocal))]` on `Data`/`Data<T>`. STJ instantiates attribute converters **parameterless**, so `WireLocal` is a context-less `Wire` — the reason `Wire._context` must stay nullable. Its only real consumers are the json parser's nested `@schema:data` reconstruction and http inbound — both now have a context. Once §A-F land (one lazy path, total readers), `WireLocal` deletes and `Wire._context` becomes structurally non-null (no tripwire). **This is the actual finish of `context-never-null` for the read path.** (Detail + the regression notes from the attempt: `Documentation/v0.2/todos.md` 2026-06-27 "eliminate WireLocal".)

### H. (likely, for architect to confirm) `Reader` delegate signature vs `ITypeReader` raw vs stream
`Of`'s `Read(object raw,…)` takes a **stored raw** (string/bytes); `ITypeReader.Read(ref reader,…)` pulls off a **stream** (no DOM). `source` has a stored raw; the Wire has a stream. So the "one reader" must serve both a stored-raw call and a streaming call — or we accept the type exposes one decode that both wrap. Flagging as a possible third path to reconcile.

---

## 4. Reader-coverage map (the concrete work-list for "total")

Has a reader (ITypeReader or static `Read`): **bool, code, dict, duration, guid, image, list, number, object, path, table, text**.

No reader today (materialize via `Convert` hook / inline / not at all): **archive, binary, clr, compare, date, datetime, directory, file, null, permission, primitive, signature, time, url**.

So "make `Readers.Of` total" = give the genuinely-readable ones a reader (`binary, date, datetime, time, url, file, directory, image-kinds, …`) **or** — per Ingi's lever — let **scalars materialize via the type's own `Create(raw, context)`** and reserve readers for streaming/structured types (`dict, list, table, object`). That shrinks the list substantially. `clr` gets the floor reader. `null`/`primitive`/`compare`/`signature`/`permission`/`archive` need a per-type decision (some are not raw-materialized).

---

## 5. Open design questions (for architect / Ingi)

1. **Readers only for streaming/structured types?** Scalars (`text/number/bool/path/guid/date/…`) materialize via the type's own `Create(raw, ReadContext)` (basically `new`), readers stay for `dict/list/table/object`. One idea ("the type makes itself"), less reader ceremony. (Ingi leaned this way.)
2. **One creation door:** do `ITypeReader.Read` and `ICreate.Create`/`Convert` converge into a single "the type makes itself from raw-or-value + ReadContext"? (§3-B). If yes, `Create(parsed,Context)` and the `Of`/`Convert` second doors all go.
3. **`%ref%` → variable lives in the type's reader** (settled: option (a)) — the reader, given `template=="plang"` in its `ReadContext`, returns a variable reference for a full `%x%`. Confirm the `variable`/`text` reader is where it sits.
4. **Stored-raw vs streaming reader** (§3-H) — one decode entry that serves both, or two thin wrappers over one core?

---

## 6. Sequencing (once design is settled)

1. clr floor reader; `Readers.Of` total (never null).
2. Every type's materialization → one reader-or-`Create(raw)` that returns the born value (kill the `Of`→`object?`→`Create` seam).
3. `source.Value()` → one dispatch; delete branches 2/3.
4. Wire: every value → `source` (`IsDeferrableShape` ⇒ true); delete the eager branches + the var-match special-case (now type-owned); delete the `Build`/`Judge` fork in the `Data` ctor.
5. `WireLocal` deletes; `Wire._context` non-null; the `_context==null` fail-closed branch + tripwire gone.
6. Fixture sweep + the 15 core tests pass *because the read is now correct*, not silenced.
