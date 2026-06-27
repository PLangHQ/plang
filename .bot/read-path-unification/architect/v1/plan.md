# Read-path unification — architect plan (v1)

**Branch:** `read-path-unification` (from `context-never-null`)
**Author:** architect. Settled with Ingi in design review. Supersedes the coder dual-path map (`../../coder/plan.md`), which carried pre-decision framing (`Of` as open, a `clr` reader floor) that the changes below overturn.

## Why

`context-never-null` made `type.Create(raw, context)` throw on a null context. The last holdout is the Wire read path: making its value births carry context regressed 15 core tests, because the `Data` ctor forks on context-presence — `Build` (eager, context) vs `Judge` (deferred, no-context) — and `Judge`, the branch context-never-null kills, is the only one that handles `path`/`%ref%`. That fork is dead code born from the nullable-context assumption.

Underneath it sit many more doors. The read — *given (raw, type, kind, context), produce the born value, lazily* — is spread across `Wire.ReadBody`, `source.Value`, `type.Build`, `type.Judge`, `type.Deserialize`, `type.Convert`, `FromWireShape`, and two reader registries. Ingi: "there are many two-way doors, I really need to clean that up." The law that collapses them: **every plang value is lazy — parse the `.pr` to learn the type, never load or parse the value until `.Value()`.**

## The center — two types, everything else delegates

- **One lazy carrier: `source`** (`app/type/item/source.cs`). Holds the raw form + declared `{type, kind}` + context + authored-mode flag. Parses nothing until `.Value()`.
- **One creation door: `app.type.Create(source) → item`** — pass the whole carrier (a `source`), no decompose. It absorbs the existing `Deserialize` (`app/type/this.cs:486`, *"Replaces Judge"*); no separate `type.Read`/`Deserialize` door survives. Inside it is one unconditional line — `App.Type.Reader(source).Read(source)` — over a **total** registry (a specific reader ‖ one generic default reader). A bad parse throws.

Two invariants that must hold at the end:

1. **No value parse at load.** Reading a `.pr` parses the envelope (`name`, `type`, `kind`) and captures the value **raw** — no `JsonDocument`, no DOM. The single parse happens in `source.Value()`.
2. **One door per type.** A type makes its value from raw exactly one way (`app.type.Create(item)`, which uses an `ITypeReader` or constructs directly). No `Build`/`Judge` fork, no second `Create(parsed)` finish step, no `Of` delegate registry.

## Leaf-trace — the incumbent behaviors and where each goes

| Incumbent (leaf) | Where it lives now | Disposition |
|---|---|---|
| `Build`/`Judge` context fork | `Data` ctor `data/this.cs:194-213`; `Data.Declare` `:242-254`; `Wire.ReadBody` EndObject `:327-334` | → one `app.type.Create` call. Fork deletes in all three. |
| `type.Build` (eager value build) | `type/this.cs:217`; callers ctor `:206`, `Declare` `:250`, `Wire` `:332`, **and `Deserialize`'s own variable branch `:497`** | delete — but only after `Deserialize`'s variable case re-points to the `variable`/`text` reader (it calls `Build` today). |
| `type.Judge` | `type/this.cs:508`; callers ctor `:212`, `Declare` `:253` only | deletes cleanly with the fork. |
| `Readers.Of` (delegate registry) | `reader/this.cs:71`; callers `source.Value:76,82`, `Read:500` | → the `ITypeReader` registry, renamed `App.Type.Reader(name, kind)` (from `Readers.Typed`). `Of` + the delegate type + `_generated`/`_runtime` delete. |
| eager Wire value branches | `Wire.ReadBody:386-441` (`refValue`, `IsDeferrableShape`, `Typed.Read` inline, `goal.call`, `json.Parse`) | → raw capture → `source` for every value. Branches delete (goal.call stays as TEMP). |
| `source.Value` 3 branches | `source.cs:87-110` | → `source` becomes a thin placeholder: `Value(data) => app.type.Create(this)` (whole source in, no decompose; `_raw` field renamed `_value`). Branches 2/3 delete. |
| `Data.Value` rebind fork + `Cacheable` flag | `data/this.cs:272`; `item.Cacheable` base `item/this.cs:127` + overrides (`text`/`dict`/`list`/`path`/`computed`) | → `Data.Value` becomes single-step `result = await item.Value(this); if (!item.IsFinal) item = result; return result`. `Cacheable` deletes; the rebind re-keys onto `IsFinal` (is-`source` vs real value). Field `_type` → `item`. |
| `IsFinal` conflates "renders" with "is the raw carrier" | `item/this.cs:247` (`=> Template == null`) + `path` override | → re-point `IsFinal` to *"I am a real value"*: `false` only for `source`, `true` for everything else (`path`/`file`/`dict`/`text`/templates). Only `source` is the placeholder that gets swapped. |
| `%ref%` → variable eager special-case | `Wire.ReadBody:294-303,386-392` | → into the `text`/`variable` reader, gated on `ReadContext.Template`. Wire special-case deletes. Requires `source` to carry the template flag. |
| STJ entry + `@schema` `if signature` probe | `Wire.Read:158-181`; `ReadSignatureLayer:206` | → `read(IReader)` (format-agnostic); `@schema` dispatches via `App.Reader(schema)`. `signature`/`data` become registered readers; `ReadSignatureLayer` → the `signature` reader; the STJ `JsonConverter` shrinks to a thin adapter. |
| `WireLocal` (context-less Wire) | `data/WireLocal.cs`; `[JsonConverter]` on `Data`/`Data<T>` `data/this.cs:24,976`; consumers `json.cs:87,157` | deletes; `Wire._context` non-null; the `_context==null` fail-closed branch + tripwire `Wire.cs:211+` delete. |

**Call-site counts** (verified): `Declare` — 2 callers (`builder/code/Default.cs:920,936`). Value-ctor with a declared type — ~7 sites (incl. `Wire.cs:339`). Both are small enough to migrate by hand.

**Stays — do not delete:** static `type.Create(object)` and `type.Create(string)` (the natural-lift primitive, used by the generic reader and ~15 native-wrap sites), the per-type coercion (the `Convert` *logic*, now the generic reader's `Read` — the `Convert` name goes), the holder ctor `Data(name, instance)`, the `ITypeReader` registry (`_generatedTyped`/`_runtimeTyped`/`TypeOf`/`Register`, lookup renamed `Readers.Typed` → `App.Type.Reader`), `ITypeReader`, the existing `app.channel.serializer.IReader` (used as-is). **Re-homed, not kept:** `FromWireShape`/`TypeFromWire`/`WireSlot`/`IsWireShape` and `ReadSignatureLayer` → the `data`/`signature` readers (`read(IReader)`); the signed-envelope handling stays but as the `signature` reader, not a hardcoded method.

## Capturing the value raw without a DOM — the existing `IReader.RawValue()` already does it

To defer, the value slot must be captured as **raw bytes without parsing**. Today's deferred path calls `JsonDocument.ParseValue` + `GetRawText()` (`Wire.cs:397`) — a DOM (it parses the value), then re-parses at `.Value()` — because a `JsonConverter<Data>.Read` has the `ref Utf8JsonReader` but not the buffer.

Reading through the **existing `IReader`** (Leg A) removes that: `app.channel.serializer.IReader` already exposes **`RawValue()`** (and `Skip()`), and the `json.Reader` impl owns the buffer, so the `data` reader does `value = r.RawValue()` — one raw slice, no DOM, uniform for scalar or structured. No new primitive, no buffer-threading puzzle. The constraint stands: **the value slot becomes a raw slice on the `source`, parsed once in `source.Value()`.**

## Flow — input to output (target), every method + every fork

The principle behind the unification: **every fork on the read path is a discrimination** ("which envelope / which type / which shape / has-context") and the cure is uniform — **registry dispatch** (`@schema` → envelope reader, `(type, kind)` → value reader) plus **virtual `Value()` / `Read()` on the item** replace the if-chains. The read is format-agnostic (`read(IReader)`, mirror of `value.Write(IWriter)`). Below is the target flow with every method call; `⑂` marks a branch and says whether it dies or stays.

### Leg A — LOAD: raw bytes → lazy `Data` (no value parse)

```
source bytes (any front-end: .pr file, http inbound, …)
  │  open → IReader r                          the EXISTING app.channel.serializer.IReader (json.Reader wraps Utf8JsonReader, OWNS the buffer)
  ▼  read(r)                                    format-agnostic — mirror of value.Write(IWriter); pulls tokens, never invents Field/Raw
  │     r.BeginObject(); r.NextName(out _)      first name is always "@schema" (written first)
  │     schema = r.String()
  │     return App.Reader(schema).Read(r)       ⑂ registry dispatch on the tag — no `if signature`   [F1 GONE]
  │
  ├─ "data" reader.Read(r):                     r positioned after @schema
  │     while r.NextName(out name):             forward pull of known envelope slots
  │        name       → r.String()
  │        type       → read the {name, kind?, strict?} entity
  │        value      → r.RawValue()            raw bytes — NO parse, NO DOM (existing IReader primitive)
  │        properties → read the props
  │     r.EndObject()
  │     return new Data(slotName, new source(value, type))   holder ctor — lazy, value untouched
  │
  └─ "signature" reader.Read(r):               the OUTER wrapper
        read + verify the wrapper; inner = read(r)   recurse → the "data" reader; return the inner Data, verified
  ▼  Data (lazy)  →  Leg B on first .Value()
```

### Leg B — USE: `await data.Value()` → born item (the single parse)

```
consumer: await data.Value()
  │
  ▼ Data.Value()                                  item = the held value (field was _type)
  │     result = await item.Value(this)
  │     if (!item.IsFinal) item = result          ONLY a source flips: parse-once, the source is replaced
  │     return result                             no catch — a parse throw propagates to the boundary seam
  │
  ▼ item.Value(data)  — virtual, per type
  │     source (IsFinal == false) → return app.type.Create(this)       throws on bad parse; source never catches
  │     dict   (IsFinal == true)  → resolve %holes% on its leaves → fresh dict     renders, stays put
  │     text   (IsFinal == true)  → Template == null ? this : interpolate(data.Context)
  │     path   (IsFinal == true)  → return this        stays a path — content loads at output; exists = IsTruthy
  │     scalar (IsFinal == true)  → return this
  │
  ▼ app.type.Create(source)       whole source in — no decompose
  │     return App.Type.Reader(source).Read(source)       reader is NEVER null — no fork, no Convert
  │        App.Type.Reader(source):  specific reader (dict / list / table / object)
  │                               ‖   generic reader (the default — its Read holds the old Convert logic)
  │        <bad parse> → Read THROWS → propagates out (caught at the boundary seam: Navigate / typed-ask)
  ▼ resolved value  →  returned to consumer (output)

  source→dict: read 1 swaps item = dict (parse once, source gone); later reads → dict.Value renders, dict kept.
  path:        `if %path% exists` → path.IsTruthy (no load);  `write out %path%` → output loads content; path stays a path.
```

### Fork ledger — every branch on the path, and its fate

| ID | Branch (current code) | Where | Fate |
|---|---|---|---|
| — | `Build` vs `Judge` (context null?) | `data/this.cs:201-212`, `:245-253` | **gone** — context never null; one door |
| — | `Readers.Of` null-check + kind-fallback | `source.cs:76-83` | **gone** — `App.Type.Reader` is total (specific ‖ generic default), so the null-check disappears; `source.Value` just calls `app.type.Create` |
| — | `source.Value` 3-way (reader / string→Convert / bytes→binary) | `source.cs:87-110` | **gone** — one dispatch through `app.type.Create` |
| — | value-slot 5-way (ref / deferrable / typed / goal.call / else) | `Wire.cs:386-441` | **gone** — uniform `CaptureRaw` → `source` |
| — | EndObject 4-way (ref / born / deferred / build) | `Wire.cs:292-342` | **gone** — always `source` + holder ctor |
| — | `%ref%` full-match special-case | `Wire.cs:294-303,386-392` | **gone from Wire** — moves into the `variable`/`text` reader (registry dispatch on type name) |
| — | scalar-vs-structured capture | (new, Leg A) | **avoided** — `IReader.Raw` slices *any* value kind as raw bytes uniformly (no token-kind branch) |
| ~~F1~~ | `signature` vs `data` (`@schema` probe) | `Wire.cs:175-181` | **gone** — `@schema` dispatches through the reader registry (`App.Reader(schema).Read(r)`), same pattern as a value type. `signature` and `data` are just two registered readers; a future envelope plugs in. No `if signature`. Its inner `_context==null` sub-fork also dies (Phase 5). |
| — | STJ entry `JsonSerializer.Deserialize<Data>` | `Wire.Read` | **gone** — the read is `read(IReader)`, format-agnostic (mirror of `value.Write(IWriter)`); `json` is one `IReader`. A `JsonConverter<Data>` survives only as a thin STJ adapter (when STJ drives an outer object). |
| **F2** | the narrow `if (… && _type.Cacheable) _type = answer` | `data/this.cs:272` | **kept, re-keyed** — `Cacheable` deleted; becomes `if (!item.IsFinal) item = result`. `Data` overwrites `item` only when it just resolved a **placeholder**, and `source` is the only placeholder (path/file/dict/text are real values, kept). Single-step, not a type-switch. |
| ~~F3~~ | `App.Type.Reader(...)` null? → reader-or-`Convert` | `app.type.Create` | **gone** — `App.Type.Reader` is **total** (specific reader ‖ one generic default reader = the old `Convert` logic), so it's always `reader.Read(source)`. No null-check, no `Convert` name. |
| — | envelope field reads (`name`/`type`/`value`/`properties`) | the `"data"` reader | **kept** — pulling known slots via `IReader.Field`/`Raw`; structural, format-agnostic, not a value fork |

### F2 — the narrow: source is the only placeholder (no `Cacheable`)

`Data.Value` overwrites `item` only when it just resolved a **placeholder** — and `source` is the only one: raw bytes that *are* the value, unparsed. It parses once into a real type and is replaced. Every other value is real and stays: a `path` stays a `path` (its content loads at `output`, existence is `IsTruthy` — it is not narrowed away), a `dict`/`text` renders in place and re-resolves next read.

```
Data.Value():
    result = await item.Value(this)
    if (!item.IsFinal) item = result   // ONLY a source flips — parse-once, source replaced
    return result                      // no catch; a parse throw propagates to the boundary seam
```

The decider is **`IsFinal`**, re-pointed to mean *"I am a real value (not the raw source carrier)"* — `false` only for `source`, `true` for everything else (`path`/`file`/`dict`/`text`/`number`, **including** templates). That kills `Cacheable`: the rebind keyed on it (`data/this.cs:272`) becomes the `!IsFinal` swap. Today `IsFinal => Template == null` conflates "renders" with "is-a-placeholder" — split them: a template `dict`/`text` **is** final (a real value that renders); only `source` is non-final. (`module.Cacheable`, action-result caching, is unrelated and stays.)

No loop, no multi-hop: `source` resolves to its real type in one parse; a `path` is already a real value (content is the consuming op's concern, not a `Data.Value` narrow). `Data` mutates only its own field, keyed on `IsFinal` — no `Data.Narrow`, no item reaching into `Data`, no caching flag. The field rename `_type` → `item` stands (it holds an `item.@this`; the `Instance` alias already exists).

### F3 — gone: the reader is total

`app.type.Create(source)` is one unconditional line — `return App.Type.Reader(source).Read(source)`. `App.Type.Reader` is **total**: it returns a **specific** reader (`dict`/`list`/`table`/`object`) or, for everything else, **one generic default reader** whose `Read` holds the logic that used to live in `Convert`. Never null. So there is no `reader != null ? Read : Convert` branch and no method named `Convert`.

My earlier "accept the fork" was based on a wrong cost — I thought total meant a thin reader **per scalar**. It doesn't: it's a **single** generic reader. So total is cheap *and* fork-free. `Convert`'s logic survives inside the generic reader's `Read` (it dispatches to each type's coercion through the convert registry — registry lookup, not an `if`). The `already-item` and `%ref%`/variable guards still collapse the same way (a read `source` carries raw, never an item; the `variable`/`text` reader handles a full `%x%` via `ReadContext.Template`). A bad parse **throws** out of `Read` — `Create`/`source`/`Data` never catch; the boundary seam (`Navigate` / typed-ask) authors the developer error.

**Bottom line:** the read path keeps **one** value branch — **F2** (the `!IsFinal` source swap) — plus the format-agnostic structural read (`IReader` pulling fields). F1 and F3 are gone (both registry dispatch); `Cacheable` is gone; `Convert` is gone (its logic is the generic reader's `Read`); the STJ-specific entry is gone (`read(IReader)`). All discrimination rides registries — `@schema` → envelope reader, `(type, kind)` → value reader — plus virtual `Value`; the narrow rides `IsFinal`; rendering is each value's own `Value`; a bad parse throws to the boundary seam.

## Demolition worklist — by phase

Each phase: what **dies** (methods + fields), what **stays**. Nothing in "stays" may be deleted; nothing in "dies" may survive the phase.

### Phase 1 — `ITypeReader` is the only reader, and the registry is total
- **Build:** wrap a stored raw (`source`'s string/bytes) as a one-shot `IReader` (json.Reader over the string; a bytes reader over the blob), plus the one `IReader` primitive for whole-payload decode (consume-value-as-string/bytes — json.Reader already has `GetRawText`/base64). **`App.Type.Reader(source)` is total — never null:** a specific reader (`dict`/`list`/`table`/`object`) or **one generic default reader** whose `Read` holds the logic that lived in `Convert` (coerce raw → scalar / lift native, dispatching to each type's coercion via the convert registry). So `app.type.Create(source) => App.Type.Reader(source).Read(source)` is one unconditional line — no null-check, no `Convert` name, no per-scalar reader files, no `clr` floor.
- **Dies:** `Readers.Of` (`reader/this.cs:71`); the `Read` delegate type (`:37`); `_generated`/`_runtime` dictionaries (`:39-40`); the static-`Read` discovery branch in `IndexAssembly` (`:181-202`).
- **Stays (renamed):** the `ITypeReader` registry — `_generatedTyped`/`_runtimeTyped`, `TypeOf`, `Register`, the `ITypeReader` discovery branch. Rename the lookup `Readers.Typed(name, kind)` → `App.Type.Reader(source)` (total; whole carrier in, reads the `(type, kind)` key off it). Add the generic default reader.

### Phase 2 — read through `IReader`; `@schema` dispatch; value captured raw
- **Build:** the read becomes `read(IReader)` — format-agnostic, the mirror of `value.Write(IWriter)`, over the **existing** `app.channel.serializer.IReader` (json.Reader wraps `Utf8JsonReader`, **owns the buffer**). `read(r)`: `BeginObject` → `NextName` (first is `@schema`) → `String()` → `App.Reader(schema).Read(r)` — `data`/`signature` are registered readers, no `if signature`. The `"data"` reader pulls slots with `while r.NextName(out name)` (`name`→`String()`, `type`→nested entity, **`value`→`r.RawValue()`** raw bytes no DOM, `properties`→read) → `new source(value, type)` → holder `Data`. The `"signature"` reader verifies then `read(r)` recurses to `data`. **No new `IReader` primitives** — `RawValue`/`Skip`/`NextName`/`Peek` already exist. `source` gains `_template` (authored mode) for the `ReadContext` at `.Value()`.
- **Dies:** the STJ read entry `JsonSerializer.Deserialize<Data>`; the `@schema` `if signature` probe (`Wire.cs:175-181`) → registry; `Wire.ReadBody` (`:280`) + `ReadSignatureLayer` (`:206`) as hardcoded methods → the `data`/`signature` readers; `ReadPropertiesObject` (`:485`) + `ReadPropertyPrimitive` (`:505`) → the `data` reader via `IReader`; `IsDeferrableShape` (`:460`); the eager value branches (`Typed.Read` inline `:406-413`, `json.Parse`-to-`value` `:438-440`, EndObject `Build` `:327-334`); the value DOM `JsonDocument.ParseValue`+`GetRawText` (`:397`); `EmitRawVerbatim` (`:467`); `FromWireShape` + `IsWireShape` + `WireSlot` + `TypeFromWire` (`data/this.cs:741-790`) — the nested-`@schema:data` reconstruct is now just `read(r)` recursing (the `data` reader); their `json.cs:87,157` callers switch to `read(r)`.
- **Stays / re-homed:** the invalid-schema throws (now inside the `data` reader); a **thin** `JsonConverter<Data>` STJ adapter (wraps a `json.Reader`, calls `read(r)`); `WrapAsTyped` (`:258`) — the `Data<T>` wrap still needed when STJ asks for a typed `Data<T>`, moves onto the adapter; the `_readDepth`/`MaxReadDepth` recursion guard (`:144-145`) → moves into `read(IReader)`.
- **Temp:** the `goal.call` inline branch (`:419-424`) and the **`action`/`GoalCall` `WireSlot`/`FromWireShape` reconstruct** (`action/this.FromWire.cs`) stay until `goal.call` gets a reader — then they migrate to `read(r)` too (follow-on, not a blocker).
- **Open:** whether `@schema` dispatches through the same `App.Type.Reader` registry or a sibling keyed by the envelope tag (same pattern either way).

### Phase 3 — `source` becomes a thin placeholder; the narrow keys on `IsFinal`
- **Build:** `source.Value(data) => app.type.Create(this)` — whole source in, no try/catch (a bad parse **throws** out of the reader; `source` never catches). `source._raw` field → `_value` (the raw *is* its value). `source` never touches templates — the materialized type owns rendering. `Data.Value` becomes single-step: `result = await item.Value(this); if (!item.IsFinal) item = result; return result` (field `_type` → `item`; no catch — the throw propagates). Re-point `IsFinal` to *"I am a real value"* — `false` only for `source`, `true` for everything else including `path`/`file`/templates. Move the `%ref%` full-match → variable judgement into the `text`/`variable` reader (`ReadContext.Template == "plang"` + a full `%x%`). Rename the `item.Value(asking)` parameter → `data` (`item/this.cs:47`, `source.cs:74`, every override).
- **Dies:** `source.Value` branches 2 (`string`→`Convert`) and 3 (`byte[]`→`binary`) + its `try/catch`/`MaterializeFailed` (`source.cs:85-127`); the `refValue` capture + EndObject variable-reference branch in `Wire.ReadBody` (`:294-303,386-392`); the `Data.Value` `Cacheable`-keyed rebind (`data/this.cs:272`); `item.Cacheable` — base virtual (`item/this.cs:127`) + overrides in `text`/`dict`/`list`/`path`/`computed`; `IsFinal => Template == null` (the template-conflating definition).
- **Stays:** `source.Peek`, `source.Write`, `source.Navigate`, `source.IsTruthy`, `source.Clr`; `module.Cacheable` (unrelated action-result caching). No `Data.Narrow` — the narrow is `Data.Value`'s own `!IsFinal` swap. The `MaterializeFailed` authoring moves to the boundary seam (`Navigate` / typed-ask catch the throw).

### Phase 4 — delete the forks
- **Build:** typed construction routes through `app.type.Create`. End-state (Ingi): callers build the item first and use the holder ctor `Data(name, instance)`. The pervasive no-type case `new Data(name, value)` is a plain lift — give it one factory (`Data.From(name, raw, type?, ctx)`: make the item via `source`/`app.type.Create`, then hold it) so the value-ctor can retire. `Declare`'s 2 callers route through `app.type.Create`.
- **Dies:** `type.Build` (after its `Deserialize:497` variable use re-points to the `variable`/`text` reader); `type.Judge`; the ctor fork (`data/this.cs:194-213`); `Data.Declare`'s fork (`:242-254`); the value-ctor `(name, value = null, type = null, …)` **entirely** (`data/this.cs:177`) — every call site moved to the holder ctor or `Data.From` (see Scope below).
- **Stays:** static `type.Create(object)`/`Create(string)`, the holder ctor, `Data.Value`. (`type.Convert`'s per-type coercion survives **only inside the generic reader's `Read`** — the `Convert` name and the direct `source`/`Create` calls to it are gone, Phase 1. `FromWireShape`/`WireSlot`/`IsWireShape`/`TypeFromWire` are re-homed into `read(IReader)` — Phase 2, not here.)
- **Scope (settled): retire the value-ctor fully.** Ingi confirmed — do not stop at deleting the fork. The value-ctor `(name, value, type)` is used on write/in-code paths too, so this phase reaches past the read path: every `new Data(name, value[, type])` site moves to either the holder ctor `Data(name, instance)` (when an item is already in hand) or the `Data.From(name, raw, type?, ctx)` factory (when a raw needs lifting). Trace every value-ctor call site in this phase, not just the ~7 typed ones; the no-type lift sites are the bulk of the work.

### Phase 5 — finish context-never-null for reads
- **Dies:** `WireLocal` + both `[JsonConverter(typeof(WireLocal))]` attributes; the `_context==null` fail-closed branch + tripwire (`Wire.cs:211+`); the `_context!`/`_template != null` defensive guards.
- **Build:** context flows through `read(IReader)` (the reader carries it), so the `signature` reader verifies with the actor in scope. The former `WireLocal` consumers (nested `@schema:data` reconstruct `json.cs:87,157`; clone/snapshot/debug STJ paths) go through `read(IReader)` with context.
- **Stays:** the thin `JsonConverter<Data>` STJ adapter, the channel-registered signing path, the `signature` reader (now context-guaranteed).

### Phase 6 — fixtures + the 15
- Sweep fixtures to born-with-context (continuation). The 15 core tests pass **because the read is now correct** (one lazy door), not because a branch was silenced.

## Reader-coverage worklist (for the door, not for `Of`)

Has a reader: **bool, code, dict, duration, guid, image, list, number, object, path, table, text**.
No reader: **archive, binary, clr, compare, date, datetime, directory, file, null, permission, primitive, signature, time, url**.

Decision (settled): **the registry is total** — a **specific** reader for streaming/structured types, and **one generic default reader** (holding the old `Convert` logic) for everything else. No per-scalar reader files; no `Convert` name.

- **Specific readers:** `dict`, `list`, `table`, `object` (streaming pull); `binary` (`byte[]`→`binary`) and the kinded binary forms (`image`/`archive`/`file`/`directory` content) where the decode is non-trivial.
- **Generic default reader** (everything else): the scalars — `bool, number, text, guid, duration, date, datetime, time, url, path, primitive` — coerce raw → value through the convert registry. One reader, no per-type files.
- **No raw materialization** (never read from a value slot): `null` rides through as the raw token `null` → typed absence; `compare`, `signature` (own wire), `permission`, `clr` (the value floor — reached only by lifting a foreign object via `Create(object)`). Confirm each is unreachable from a value slot.

## Settled design questions

1. **Readers only for streaming/structured?** No — the registry is **total**: specific readers for structured types + **one generic default reader** (the old `Convert` logic) for the rest. So `App.Type.Reader(source)` is never null and `app.type.Create` is fork-free (F3 gone). One generic reader, not a file per scalar.
2. **One creation door?** Yes — `app.type.Create(source) => App.Type.Reader(source).Read(source)`. The reader returns the born item; `Convert`'s logic lives inside the generic reader's `Read`; a bad parse throws.
3. **`%ref%` → variable in the reader?** Yes — in the `text`/`variable` reader, gated on `ReadContext.Template == "plang"`. Requires `source` to carry the template flag (Phase 2).
4. **Stored-raw vs streaming?** One interface (`ITypeReader`), two front-ends — wrap the stored raw as a one-shot `IReader` (Phase 1).
5. **Serializer-independent read?** Yes — the read is `read(IReader)` (mirror of `value.Write(IWriter)`); `json` is one `IReader`. No `JsonSerializer.Deserialize<Data>` entry; a thin `JsonConverter<Data>` adapter remains only for STJ-driven outer objects.
6. **`@schema` dispatch generic?** Yes — `@schema` is a tag dispatched through the reader registry (`App.Reader(schema)`), same pattern as a value type. `signature`/`data` are registered readers; a future envelope plugs in. No `if signature`.

## OBP validation pass

| Surface | Check | Verdict |
|---|---|---|
| `read(IReader)` (read entry) | format-agnostic, mirror of `value.Write(IWriter)` | **settled.** No `JsonSerializer.Deserialize<Data>` entry; `json` is one `IReader`; STJ `JsonConverter<Data>` survives as a thin adapter. |
| `IReader` (existing `app.channel.serializer.IReader`) | already has `Peek()`/`BeginObject`/`NextName(out name)`/`RawValue()`/`Skip()`/typed pulls | **use it as-is — do NOT invent `Field`/`Raw`.** The earlier sketch added a parallel surface (the smell); the real one already covers Leg A. |
| `@schema` dispatch — `App.Reader(schema)` | tag dispatched through the registry, not `if signature` | **settled** (same pattern as a value type). `signature`/`data` are registered readers. |
| `app.type.Create(source)` (door; absorbs `Deserialize`) | one line — `App.Type.Reader(source).Read(source)`; whole carrier in, no decompose | **settled.** No `type.Read`/`Deserialize` door; no `Convert` call; a bad parse throws. |
| `App.Type.Reader(source)` (was `Readers.Typed`/`Of`) | total registry; whole carrier in | **settled:** never null — specific reader ‖ one generic default reader; returns an `ITypeReader`. |
| generic default reader (was `Convert`) | one reader holding the per-type coercion | **settled:** `Convert` the name dies; its logic is the generic reader's `Read`. |
| `Readers.Of` | dies | n/a |
| `IsDeferrableShape` | verb-question that deletes itself (everything defers) | dies — good. |
| `source` `{raw, type, kind, context, template}` | object kept whole, no decomposition; `template` added so the authored-mode signal survives deferral | clean. |
| `ReadContext(Context, Template)` | noun record, mirror of `IWriter` | keep. |
| `Data.From(name, raw, type?, ctx)` (new factory) | From+Noun static factory | keep (if value-ctor retires). |
| `Data._type` field | holds an `item.@this` (a value), not a type entity | **rename → `item`** (matches what it holds; `Instance` alias already exists). |
| `source._raw` field | the raw is the source's value | **rename → `_value`** (consistent with every other item's backing). |
| `item.Value(asking)` parameter | role-name for a `data.@this` | **rename → `data`** (name the variable after its type, not its role). Applies to `item/this.cs:47`, `source.cs:74`, and every override. |
| `IsFinal` | currently `=> Template == null` — conflates "renders" with "is the raw carrier" | **re-point → "I am a real value":** `false` only for `source`, `true` for everything else (`path`/`file`/`dict`/`text`/templates). Drives the `Data.Value` `!IsFinal` swap. **Name caveat:** "final" reads as "unchanging," but a template/path is `IsFinal=true` yet re-renders/reloads — only `source` is non-final. Coder: consider a word that says "real value vs raw carrier" (the distinction is *is-this-still-the-source*), not "final." |
| `item.Cacheable` | flag `Data` read to decide the rebind | **dies** — the rebind re-keys onto `IsFinal` (is-`source` vs real value); no flag. |
| parse error | `source` try/catch authors `MaterializeFailed` | **the reader throws** — `Create`/`source`/`Data` never catch; the boundary seam (`Navigate`/typed-ask) authors it. |
| `EmitRawVerbatim` / `WrapAsTyped` | Verb+Noun helpers | `EmitRawVerbatim` dies (Phase 2); `WrapAsTyped` is a private STJ-cast helper — leave or inline. |
| `FromRaw` | From+Noun factory, used by list/dict/channel | keep. |
| `FromWireShape` / `TypeFromWire` / `WireSlot` / `IsWireShape` | the hand-rolled nested-`@schema:data` reconstruct | **re-homed → `read(IReader)`** (the `data` reader recursing); the `action`/`GoalCall` callers follow when `goal.call` gets a reader. |

**Object decomposition:** the read no longer hands back a half-decoded `object?` for a caller to finish — the door returns a born item, and `source` holds the raw whole until `.Value()`. No "producer hands back raw, consumers transform identically" smell remains on the read path.
