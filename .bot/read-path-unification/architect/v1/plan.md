# Read-path unification — architect plan (v1)

**Branch:** `read-path-unification` (from `context-never-null`)
**Author:** architect. Settled with Ingi; incorporates the coder's v1 response (`../../coder/response-to-architect-v1.md`). Authoritative; `../../coder/plan.md` is the handoff.

## Why

`context-never-null` made `type.Create(raw, context)` throw on a null context. The last holdout is the Wire read path: making its value births carry context regressed 15 core tests, because the `Data` ctor forks on context-presence — `Build` (eager) vs `Judge` (deferred) — and `Judge`, the branch context-never-null kills, is the only one that handles `path`/`%ref%`. That fork is dead code born from the nullable-context assumption, and under it sit many more doors. The read — *given (raw, type, kind, context), produce the born value, lazily* — is spread across `Wire.ReadBody`, `source.Value`, `type.Build`, `type.Judge`, `type.Deserialize`, `FromWireShape`, and two reader registries. Ingi: "there are many two-way doors, I really need to clean that up."

**Law:** every plang value is lazy — parse the `.pr` to learn the type; never load or parse a value (or a property's value) until `.Value()`.

## The center — two types, everything else delegates

- **One lazy carrier: `source`** (`app/type/item/source.cs`). Holds the raw form + declared `{type, kind}` + context + authored-mode flag. Parses nothing until `.Value()`. It is a transient placeholder — once it materializes into a real type it is replaced and gone.
- **One creation door: `app.type.Create(source) → Task<(item?, Error?)>`** — pass the whole carrier (no decompose); it builds the type entity and reads its raw in one call. Absorbs the existing `Deserialize` (`type/this.cs:486`, *"Replaces Judge"*); no separate `type.Read`/`Deserialize` door. Inside, one line: `await App.Type.Reader(source).Read(source)` over a **total** registry. On a bad parse it returns the error (see Error model), it does not throw.

The read is **serializer-independent**: `read(IReader)` — the mirror of `value.Write(IWriter)` — over the **existing** `app.channel.serializer.IReader`. `json` is one `IReader`.

**The read is async — `read(IReader, View) → Task<Data>`** (Ingi). Verify-on-read is async (today sync-over-async at `Wire.cs:248` `.GetAwaiter().GetResult()`, tolerated only inside the sync `JsonConverter.Read`). The symmetry with `Write` is "read pulls / write pushes," **not** "both sync": **reads do I/O** (signature verify now, `path`/`url` content later), writes don't. So the whole `read` / `App.Reader(schema).Read(r, v)` / `app.type.Create` chain returns `Task` and the signature reader `await`s verify. The one surviving sync-over-async is the thin `JsonConverter<Data>` STJ adapter (its `Read` signature is sync) — it `.GetAwaiter().GetResult()`s the async `read` at the STJ perimeter, where there is no choice.

## Invariants (must hold at the end)

1. **No value parse at load.** Reading parses the envelope (`name`, `type`, `kind`) and captures the `value` — **and every property's value** — as raw bytes via `IReader.RawValue()` (no DOM). The single parse happens in `source.Value()`, per value, on first `.Value()`.
2. **No type-discrimination fork.** All "which envelope / which type" choices are registry dispatch (`@schema` → envelope reader, `(type, kind)` → value reader); the only value-path branch left is the narrow (F2), keyed on `Cacheable`.

## Error model — `(item?, Error?)`, not a throw

A malformed value is **bad data, not an invariant violation** (the same read serves http-inbound, not just `.pr`), and PLang's model is `on error` over `Data.Error`. The failure originates inside `app.type.Create` (the reader/parse), so it authors the error **there** — keyed `MaterializeFailed`, with the binding name, `{type, kind}`, JSON path+line, and the inner exception attached (everything today's `source.cs:123-126` has) — and returns `(null, Error)`. `source` forwards it to `Data`; nobody throws, nobody enumerates catching seams:

```
app.type.Create(source) -> Task<(item?, Error?)>
source.Value(data):                                       // already async
    (item, err) = await app.type.Create(this)
    if (err != null) { data.Fail(err); return Absent; }   // error set on Data at the source seam
    return item
```

## Leg A — LOAD: bytes → lazy `Data` (no value parse)

```
source bytes (any front-end: .pr file, http inbound, …)
  │  open → IReader r, View v          existing app.channel.serializer.IReader (json.Reader OWNS the buffer); v = Store (local) | Out (transport)
  ▼  async read(r, v)                   format-agnostic — mirror of value.Write(IWriter); pulls tokens
  │     r.BeginObject(); r.NextName(out _)        first name is always "@schema" (written first)
  │     schema = r.String()
  │     return await App.Reader(schema).Read(r, v)   ⑂ registry dispatch on the tag — no `if signature`   [F1 GONE]
  │
  ├─ "data" reader.Read(r, v):          r positioned after @schema
  │     while r.NextName(out name):     forward pull of known slots
  │        name       → r.String()
  │        type       → read the {name, kind?, strict?} entity
  │        value      → r.RawValue()    raw bytes — NO parse, NO DOM (existing IReader primitive)
  │        properties → for each prop: read name + r.RawValue() (each prop value RAW too — invariant 1)
  │     r.EndObject()
  │     return new Data(slotName, new source(value, type))   holder ctor — lazy, value untouched
  │
  └─ async "signature" reader.Read(r, v):   the OUTER wrapper; carries context (Phase 5 → non-null) + View v
        read the wrapper
        v == Out  → await verify (actor from context); bad signature → Error         // transport: auto-verify (async)
        v == Store→ peel without verify                                              // local copy
        inner = await read(r, v)        recurse → the "data" reader; return the inner Data (verified)
  ▼  Data (lazy)  →  Leg B on first .Value()
```

`@schema` is a tag dispatched through the registry — `signature`/`data` are registered readers; a future envelope plugs in, no `if`. The `signature` reader is a reader that *does work* (like a `path` reader doing I/O): it verifies using the carried context + `View`. Fail-closed-on-null-context dies (Phase 5 guarantees context); fail-closed-on-bad-signature stays.

## Leg B — USE: `await data.Value()` → born item

```
Data.Value():
    answer = await item.Value(this)                          item = the held value (field was _type)
    if (answer != item && item.Cacheable) item = answer      ⑂ the narrow [F2] — source caches its parse; a template never caches
    return answer

item.Value(data)  — virtual, per type:
    source → (it, err) = await app.type.Create(this); if err { data.Fail(err); return Absent } else return it
    dict   → re-render entries whose `!e.IsFinal` → fresh dict        // IsFinal picks WHICH inner elements re-render
    list   → same, by index
    text   → Template == null ? this : interpolate(data.Context)
    path   → return this        // stays a path — content loads at output; existence is IsTruthy
    scalar → return this

async app.type.Create(source) -> Task<(item?, Error?)>:
    return await App.Type.Reader(source).Read(source)       reader is NEVER null — no fork
       App.Type.Reader(source):  specific reader (dict / list / table / object) — string-raw goes to the THIN generic reader
                              ‖   generic reader (the default) → DELEGATES to type.Convert(raw) (per-type hook, type/this.cs:573)
       byte[] raw never reaches the generic reader → born `binary` (type=binary, kind=mime/.ext) → binary-family reader
       kind-narrowing (json→item, jpg→image) lives INSIDE App.Type.Reader's lookup, not a source branch
       <bad parse> → returns (null, Error) with MaterializeFailed + path/line
```

Two narrowing facts, kept distinct (the coder's C3):
- **`Cacheable`** (`item:127`, "Data may keep my answer — parse yes, render never") drives the narrow. `source` inherits `Cacheable => true` → its parse is kept (`source → dict`, source replaced). A template (`text`/`dict`/`list` with `Template != null`) is `Cacheable => false` → never kept → re-renders every read. `path.Cacheable => _location.Cacheable` (false for `%file%.txt`) → a templated path re-resolves.
- **`IsFinal`** (`item:247`, `=> Template == null`, "my door returns myself") is a *different* axis, read by `dict:388`/`list:570` to choose which inner elements to re-render. `dict`/`list` are `IsFinal => false` **always** (a container re-walks its entries). These two axes do not merge.

## Fork ledger — every branch on the path, and its fate

| ID | Branch (current code) | Where | Fate |
|---|---|---|---|
| — | STJ entry `JsonSerializer.Deserialize<Data>` | `Wire.Read:158` | **gone** — `read(IReader)`, format-agnostic; `json` is one `IReader`; a thin `JsonConverter<Data>` adapter remains for STJ-driven outer objects |
| ~~F1~~ | `signature` vs `data` (`@schema` probe) | `Wire.cs:175-181` | **gone** — `@schema` dispatches via `App.Reader(schema)`; `signature`/`data` are registered readers, no `if signature` |
| — | `Build` vs `Judge` (context null?) | `data/this.cs:201-212,245-253`; `Wire.cs:327-334` | **gone** — context never null; one door (`app.type.Create`) |
| — | `Readers.Of` null-check + kind-fallback | `source.cs:76-83` | **gone** — `App.Type.Reader` total; `source.Value` just calls `app.type.Create` |
| — | `source.Value` 3-way (reader / string→Convert / bytes→binary) | `source.cs:87-110` | **gone** — one dispatch through `app.type.Create` |
| — | value-slot 5-way (ref / deferrable / typed / goal.call / else) | `Wire.cs:386-441` | **gone** — uniform `IReader.RawValue()` → `source` |
| — | EndObject 4-way (ref / born / deferred / build) | `Wire.cs:292-342` | **gone** — always `source` + holder ctor |
| — | `%ref%` full-match special-case | `Wire.cs:294-303,386-392` | **gone from Wire** — moves into the `variable`/`text` reader (`ReadContext.Template`) |
| ~~F3~~ | `App.Type.Reader(...)` null? → reader-or-`Convert` | `app.type.Create` | **gone** — registry is **total** (specific ‖ one generic default reader); always `reader.Read(source)` |
| **F2** | the narrow `if (answer != item && item.Cacheable) item = answer` | `data/this.cs:272` | **kept** — `source` caches its parse (replaced); a template/`computed` (`Cacheable=false`) re-renders. Field `_type`→`item`. One line, not a type-switch. |
| — | `dict`/`list` inner `!e.IsFinal` re-render | `dict:388`, `list:570` | **kept** — render logic (which inner templates to re-resolve), not value materialization |
| — | envelope field reads (`name`/`type`/`value`/`properties`) | the `"data"` reader | **kept** — `IReader` pulling known slots; structural, format-agnostic |

**Bottom line:** the read path keeps one value-path branch — **F2**, the `Cacheable`-keyed narrow — plus the structural `IReader` field reads and the `dict`/`list` render-internal `!IsFinal`. F1 and F3 became registry dispatch; the STJ entry, the value DOM, and the `Build`/`Judge` context fork are gone. Discrimination rides registries (`@schema`, `(type, kind)`) + virtual `Value`; a bad parse returns `(item, Error?)`.

## Leaf-trace — incumbents and where each goes

| Incumbent (leaf) | Where it lives now | Disposition |
|---|---|---|
| STJ entry + `@schema` `if signature` probe | `Wire.Read:158-181`; `ReadSignatureLayer:206` | → `read(IReader, View)`; `@schema` → `App.Reader(schema)`; `ReadSignatureLayer` → the `signature` reader; the `JsonConverter` shrinks to a thin adapter. |
| `Wire.ReadBody` + `ReadPropertiesObject` + `ReadPropertyPrimitive` | `Wire.cs:280,485,505` | → the `"data"` reader, pulling slots via `IReader` (`RawValue` for value + each property value). |
| eager Wire value branches | `Wire.cs:386-441` (`refValue`/`IsDeferrableShape`/`Typed.Read`/`goal.call`/`json.Parse`) | → uniform `RawValue()` capture → `source`. Branches delete (goal.call stays TEMP). |
| value DOM | `Wire.cs:397` (`JsonDocument.ParseValue`+`GetRawText`) | → `IReader.RawValue()` (no DOM). |
| `FromWireShape`/`IsWireShape`/`WireSlot`/`TypeFromWire` | `data/this.cs:730-790`; callers `json.cs:87,157` | → re-homed into `read(IReader)` (the nested `@schema:data` reconstruct is just `read(r)` recursing). |
| `source.Value` 3 branches + `try/catch`/`MaterializeFailed` | `source.cs:74-134` | → `source.Value => (item,err)=app.type.Create(this); if err data.Fail; return Absent|item`. `source._raw` field → `_value`. |
| `Readers.Of` (delegate registry) | `reader/this.cs:71`; callers `source.Value:76,82`, `Deserialize:500` | → total `App.Type.Reader(source)` (renamed from `Readers.Typed`); `Of` + the delegate type + `_generated`/`_runtime` delete. |
| `type.Convert` direct calls / central switch risk | `source.cs:95`; `type/this.cs:573` | the per-type `Convert` hook **stays** (distributed on each type, reached by the generic reader); the direct `source`/`Create` call to it goes. `catalog/Conversion.cs` (router only) stays. |
| `Data.Value` rebind + field name | `data/this.cs:272`, `:35` | → field `_type` → `item` (holds an `item.@this`; `Instance` alias exists); rebind stays, keyed on `Cacheable`. |
| `item.Value(asking)` parameter | `item/this.cs:47`, every override | → rename `asking` → `data` (variable named after its type, not its role). |
| `Build`/`Judge` + the value-ctor fork | `type/this.cs:217,508`; `data/this.cs:177,194-213,242-254` | → die in the **last** phase, with value-ctor retirement (`Build`/`Judge` are only still reachable through the value-ctor / `Declare`). |
| `WireLocal` (context-less Wire) | `data/WireLocal.cs`; `[JsonConverter]` `data/this.cs:24,976` | deletes; context flows through `read(IReader)`; the `_context==null` tripwire (`Wire.cs:211+`) deletes. |

## Demolition worklist — by phase

Each phase: what **dies**, what **stays**. Value-ctor retirement is deliberately **last** (Q4 — scope decided when we reach it).

### Phase 1 — `ITypeReader` is the only reader, and the registry is total
- **Build:** wrap a stored raw as a one-shot `IReader`. Make `App.Type.Reader(source)` **total** — specific readers (`dict`/`list`/`table`/`object`) ‖ **one thin generic default reader**. The generic reader is **string-raw scalars only** (`number`, `bool`, `guid`, `date`, `datetime`, `time`, `url`, `text`, `primitive`), one delegation — `source.DeclaredType.Convert(raw)` (the per-type hook, `type/this.cs:573`) — **zero type-branching**. So `app.type.Create(source) => await App.Type.Reader(source).Read(source)` is one line, never null, no `Convert` *door*.
- **`byte[]` is the `binary` family, never the generic reader (Ingi):** a raw `byte[]` is born `binary` (`type=binary`, `kind=mime/.ext`) and read by the `binary`/`image`/`table`/`archive`/`file`/`directory` specific readers. Decode-to-text stays the explicit `as text` — no byte→string normalization inside the generic reader. The split is by **raw shape**: string-raw → thin generic reader; byte-raw → binary-family specific readers.
- **Rule (Ingi):** if the generic reader ever wants an `if (type == …)`, that's the signal to **split a specific reader file** — it stays genuinely thin or it stops being the generic reader. No god-reader.
- **Verify before deleting:** prove every `(type, kind)` reachable today via `Of`/`Convert`/direct-binary maps to **exactly one** reader (specific or generic) — trace current paths against the reader-coverage table; delete `Readers.Of` only after the map is total.
- **kind-narrowing** (`json→item`, `jpg→image`) lives **inside `App.Type.Reader`'s lookup**, not a `source` branch.
- **Dies:** `Readers.Of` (`reader/this.cs:71`); the `Read` delegate type (`:37`); `_generated`/`_runtime` dictionaries (`:39-40`); the static-`Read` discovery branch (`:181-202`).
- **Stays:** the `ITypeReader` registry (`_generatedTyped`/`_runtimeTyped`/`TypeOf`, the discovery branch); rename the lookup `Readers.Typed` → `App.Type.Reader`. The per-type `Convert` hooks + `catalog/Conversion.cs` router (the generic reader delegates to them).
- **Q7 — `code.load` runtime registration:** `Register()` (`:125`) survives but feeds the **typed** (`ITypeReader`) table, not the dead delegate one; a DLL shipping a static `Read` is wrapped in an `ITypeReader` adapter at registration.

### Phase 2 — `read(IReader, View)`; `@schema` dispatch; value (and properties) captured raw
- **Build:** the read entry is `read(IReader, View)` over the existing `app.channel.serializer.IReader`. `@schema` dispatches via `App.Reader(schema)` (`data`/`signature` registered readers; no new `IReader` primitives — `RawValue`/`Skip`/`NextName`/`Peek` already exist). The `data` reader captures `value` **and every property value** via `RawValue()` (raw, no DOM) → `source` → holder `Data`. `source` gains `_template` (authored mode) for the `ReadContext` at `.Value()`.
- **Dies:** STJ read entry `JsonSerializer.Deserialize<Data>`; the `@schema` `if signature` probe (`:175-181`); `Wire.ReadBody` (`:280`) + `ReadSignatureLayer` (`:206`) + `ReadPropertiesObject` (`:485`) + `ReadPropertyPrimitive` (`:505`) → the `data`/`signature` readers; `IsDeferrableShape` (`:460`); the eager value branches (`:406-413,438-440,327-334`); the value DOM (`:397`); `EmitRawVerbatim` (`:467`); `FromWireShape`/`IsWireShape`/`WireSlot`/`TypeFromWire` (`data/this.cs:730-790`) — callers (`json.cs:87,157`) switch to `read(r)`.
- **Stays / re-homed:** invalid-schema throws (now in the `data` reader); a **thin** `JsonConverter<Data>` STJ adapter (wraps a `json.Reader`, calls `read(r, v)`); `WrapAsTyped` (`:258`) → onto the adapter (`Data<T>` wrap when STJ asks); `_readDepth`/`MaxReadDepth` (`:144-145`) → into `read(IReader)`; `FromRaw` (`:317`).
- **Temp:** the `goal.call` inline branch (`:419-424`) and the `action`/`GoalCall` `WireSlot`/`FromWireShape` reconstruct (`action/this.FromWire.cs`) stay until `goal.call` gets a reader, then migrate to `read(r)`.
- **Open:** `@schema` on the same `App.Type.Reader` registry, or a sibling keyed by the envelope tag (same pattern either way).

### Phase 3 — `source` becomes a thin placeholder; the narrow stays on `Cacheable`
- **Build:** `source.Value(data) => (item,err)=app.type.Create(this); if err data.Fail(err) return Absent; return item` — no try/catch, no throw. `source._raw` field → `_value`. `source` never touches templates. `Data.Value` keeps its rebind, keyed on `Cacheable`, field `_type` → `item`. Move the `%ref%` full-match → variable judgement into the `text`/`variable` reader (`ReadContext.Template == "plang"`). Rename the `item.Value(asking)` parameter → `data`.
- **Dies:** `source.Value` branches 2/3 + `try/catch`/`MaterializeFailed` (`source.cs:85-127` — authoring moves into `app.type.Create`); the `refValue` capture + EndObject variable-reference branch (`Wire.cs:294-303,386-392`).
- **Stays:** `source.Peek`/`Write`/`Navigate`/`IsTruthy`/`Clr`; **`Cacheable`** (both base + the `text`/`dict`/`list`/`path`/`computed` overrides — the narrow needs it); **`IsFinal`** unchanged (`=> Template == null`, drives `dict`/`list` inner re-render); `module.Cacheable` (unrelated). No `Data.Narrow` — the narrow is `Data.Value`'s own line.

### Phase 4 — finish context-never-null for reads
- **Dies:** `WireLocal` + both `[JsonConverter(typeof(WireLocal))]`; the `_context==null` fail-closed branch + tripwire (`Wire.cs:211+`); the `_context!`/`_template != null` defensive guards.
- **Build:** context flows through `read(IReader)`, so the `signature` reader verifies with the actor in scope. Former `WireLocal` consumers go through `read(IReader)`.
- **Stays:** the thin STJ adapter, the channel signing path, the `signature` reader (now context-guaranteed).

### Phase 5 — fixtures + the 15
- Sweep fixtures to born-with-context. The 15 core tests pass **because the read is correct** (one lazy door), not because a branch was silenced.

### Phase 6 (LAST) — retire the value-ctor + delete `Build`/`Judge`  *(scope TBD — decide here, Q4)*
- The value-ctor `(name, value, type)` and its `Build`/`Judge` fork (`data/this.cs:177,194-213`), `type.Build` (`:217`), `type.Judge` (`:508`), `Data.Declare`'s fork (`:242-254`). These reach **write/in-code paths across the codebase**, unrelated to the read. Reads already avoid them (holder ctor + `source`) after Phases 1-3.
- **Decision deferred to this point:** retire the value-ctor fully in this branch, or split to a follow-on so read-path-unification lands first. (Needs the no-type `new Data(name, value)` call-site count, which is uncounted.) Decide when we get here.

## Reader-coverage worklist

The registry is **total**, split by **raw shape**: string-raw → the thin generic reader; byte-raw → binary-family specific readers; structured → structured specific readers. No per-scalar reader files, no `Convert` *door*.

- **Structured specific readers:** `dict`, `list`, `table`, `object`.
- **Binary-family specific readers (byte-raw):** `binary` (`byte[]`→`binary`), and `image`/`archive`/`table`/`file`/`directory` when the `kind` names one. `byte[]` is born `binary` (`kind=mime/.ext`); it never reaches the generic reader; decode-to-text is the explicit `as text`.
- **Thin generic default reader (string-raw) → `type.Convert`:** the string scalars — `number, bool, guid, date, datetime, time, url, text, primitive` (and `duration`). One delegation to the per-type hook, zero branching; if it wants an `if (type==…)`, split a specific reader instead.
- **No raw materialization** (never a value slot): `null` rides as the raw token → typed absence; `compare`, `signature` (own envelope), `permission`, `clr` (value floor — only via `Create(object)`). Confirm each is unreachable from a value slot.

## Settled design questions

1. **Generic reader holds or delegates?** **Delegates** to `type.Convert` (per-type hook). It is **thin — string-raw scalars only, zero branching**; `byte[]` is the binary family, not the generic reader. The `Convert` *name* at the per-type hook stays; there is no `Convert`/`type.Read` *door*. If it grows an `if (type==…)`, split a specific reader (Ingi).
2. **One creation door?** Yes — `app.type.Create(source) => App.Type.Reader(source).Read(source)`, returning `(item?, Error?)`.
3. **`%ref%` → variable in the reader?** Yes — `text`/`variable` reader, gated on `ReadContext.Template == "plang"`; `source` carries the template flag.
4. **Serializer-independent?** Yes — `read(IReader)` (mirror of `value.Write(IWriter)`); `json` is one `IReader`; thin STJ adapter only.
5. **`@schema` dispatch generic?** Yes — `App.Reader(schema)`, no `if signature`.
6. **Error model?** `(item?, Error?)` from `app.type.Create`, set on `Data` by `source`. No throw (coder C1).
7. **`IsFinal`/`Cacheable`?** Two orthogonal axes, both kept (coder C3): `Cacheable` drives the narrow; `IsFinal` drives `dict`/`list` inner re-render.
8. **Signature reader?** A registered reader that also **verifies**, carrying context + `View` injected into `read(IReader)` — verify on `Out`, peel on `Store` (Q5).
9. **Properties lazy?** Yes — property value slots captured raw too (invariant 1, Q6).
10. **`code.load` readers?** `Register()` → the `ITypeReader` table; static `Read` wrapped in an adapter (Q7).
11. **Value-ctor scope?** Decided at Phase 6 (Q4).
12. **Sync or async read?** **Async** — `read(IReader) → Task<Data>` (Ingi). Verify is async; reads do I/O, writes don't (read pulls / write pushes, not both-sync). The one sync-over-async bridge is the thin STJ `JsonConverter<Data>` adapter at the perimeter.

## OBP validation pass

| Surface | Check | Verdict |
|---|---|---|
| `read(IReader, View) → Task<Data>` | format-agnostic, **async**; `View` injected for the signature reader | settled. Mirror with `Write` is pull/push, not sync/sync — reads do I/O. No `JsonSerializer.Deserialize<Data>` entry; the thin STJ adapter is the one sync-over-async bridge, at the perimeter. |
| existing `IReader` | already has `Peek`/`BeginObject`/`NextName`/`RawValue`/`Skip`/typed pulls | **use as-is — do NOT invent `Field`/`Raw`** (the earlier sketch was a parallel-surface smell). |
| `@schema` dispatch — `App.Reader(schema)` | tag through the registry, not `if signature` | settled. `signature`/`data` registered readers. |
| `app.type.Create(source) -> (item?, Error?)` | one line, whole carrier in, no decompose, no throw | settled. Absorbs `Deserialize`; no `Convert` door. |
| generic default reader → `type.Convert` | **delegates** to the per-type hook, does not hold a switch | settled (coder C2). Per-type hooks + router stay. |
| `App.Type.Reader(source)` | total registry; whole carrier in | settled — never null; returns an `ITypeReader`. |
| `Cacheable` + `IsFinal` | two orthogonal axes | **both kept** (coder C3). `Cacheable` → narrow; `IsFinal` → inner re-render. Not merged. |
| `Data._type` → `item` | field holds an `item.@this`, not a type entity | rename (the `Instance` alias already exists). |
| `source._raw` → `_value` | the raw is the source's value | rename — consistent with other items' backing. |
| `item.Value(asking)` → `item.Value(data)` | role-name for a `data.@this` | rename — variable after its type, not its role. |
| `source` `{value, type, kind, context, template}` | object kept whole, no decomposition | clean. |
| `FromRaw` | From+Noun factory (list/dict/channel) | keep. |
| `FromWireShape`/`WireSlot`/`IsWireShape`/`TypeFromWire` | hand-rolled nested-`@schema:data` reconstruct | re-homed → `read(IReader)`. |
