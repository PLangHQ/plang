# Lazy deserialize — implementation plan

## Why

The read side of the value model is fragmented and eager, while the write side is one clean registry. Three-plus mechanisms turn bytes into a typed value (`type.Convert`, the per-family `Convert` hook reached through `OwnerOf`, `FromWire`/`WireReader`, `path.JsonConverter`, `type.json`, and a set of per-type `JsonConverter<T>`), and reads parse at different moments in different ways (`file.read` by extension, `http.get` by `Content-Type` into a second type, `channel.read` ignores the channel's `Mime`). A second wire format would have a finished write half and a from-scratch read half. On top of that, several types name a format or transport they shouldn't know (`path.JsonConverter`, `FromWire`), and the number model silently drops C# type information (`float` collapses to `double`; `uint`/`ulong`/`Int128`/`BigInteger` don't exist).

The fix is one symmetric reader registry that mirrors the renderer, plus lazy materialization. Two payoffs drive it:

- **Verbatim passthrough.** A value that is never touched serializes its raw bytes straight back out — no parse-then-reserialize. Couriers (variable memory, callstack, channel routing, signing) cannot force materialization, so the OBP courier rule (only leaves touch `.Value`) holds by construction.
- **Verify a signature without materializing.** The signature is over the raw bytes; holding them verbatim lets verification check exactly what arrived instead of a re-serialized form that might not match.

## You own this

Every signature, file path, and shape below is a concrete suggestion so the design is legible. You (coder/tester) own the final form. If a cleaner structure falls out while building — a different registry signature, a different field split on Data, a better placement — take it and note what changed and why. The fixed points are the eight Decisions in `architect-verdict.md`; those came from Ingi directly and don't move.

## The target shape

```
                    channel  — the I/O layer: a stream + Mime + serializer
                       │   every read enters here
      ┌────────────────┼────────────────┐
   file             http            stream / native
(fs-backed,    (http-backed,      (in-memory, session, …)
 Mime from      bidirectional,
 extension)     Mime from C-Type)
      └────────────────┼────────────────┘
                       ▼
                 channel.read                       ← the ONE boundary
                       │   stamps type/kind from Mime; produces lazy Data.
                       │   when Mime is application/plang the serializer reads the
                       │   Data container (binary or json) — defers the value slot too
                       ▼
   Data { raw, type, kind, value:lazy, properties }
                       │   first touch of .Value  (and only then)
                       ▼
   app.type.reader.Of(type, kind) → Read(raw, kind, ctx) → value   ← mirror of app.type.renderer
                       │   raw stays authoritative until a mutation;
                       │   serialize emits raw verbatim when the value was never touched
                       ▼
```

Channel is the foundation: file and http aren't peers of channel — they're channels. Everything reads through the one `channel.read` boundary; "wire" isn't a separate source, it's the case where the channel's Mime is `application/plang` and the serializer reads the Data container (binary, or json by default).

The whole plan is: build that registry (Part 1), make Data hold the raw and materialize through it (Part 2), broaden numbers so a type can read toward its exact kind (Part 3), make file and http channels so everything reads through one boundary (Part 4), and let the access pattern drive resolution without guessing (Part 5).

## Code file structure

Where the work lands. `+` new file, `~` changed, `−` deleted, `·` unchanged reference. Paths under `PLang/app/`.

```
type/
  reader/this.cs                     +  the reader registry — mirror of type/renderer/this.cs; Of(type, kind) → Read
  renderer/this.cs                   ·  the mirror it copies
  <family>/serializer/Default.cs     ~  gains a static `Read` next to `Write`
                                          (text, number, path, image, datetime, duration, code, choice)
  this.cs                            ~  Convert(raw) → registry dispatch entry; FromWire/WireReader fold in
  this.json.cs                       −  deletes (folds into the reader registry)
  convert/this.cs                    ~  OwnerOf switch → per-family "I own these CLR types" declarations
  path/this.JsonConverter.cs         −  deletes (path's Read → path/serializer/Default.cs; 6 registration sites drop it)
  number/this.cs                     ~  exact-CLR value + full scalar tower; drop _i/_d/_f union, NumberKind enum, Kinds
  number/this.Build.cs               ~  kind from the exact type (no float→double collapse)
  number/this.Convert.cs             ~  KindToClr covers the tower; promote-then-narrow for arithmetic
  table/this.cs + serializer/        +  NEW type — grid (rows/columns/headers); Read: (table,csv) now, (table,xlsx) follow-on

data/
  this.cs                            ~  add `raw`; .Value materializes via reader when value is null; drop ConvertValue
  this.Navigation.cs                 ~  navigation reads .Value (materializes); ConvertValue call removed
  Wire.cs                            ~  Wire.Read captures the value slot raw and defers it (lazy wire read)

channel/
  this.cs                            ~  Read stamps type/kind from Mime → lazy Data (the boundary)
  type/                              ~  all channel kinds move under here (stream, session, message, event, goal, noop)
    stream/this.cs                   ~  Read produces lazy Data
    file/this.cs                     +  filesystem channel kind; Mime from extension; bytes via path.ReadBytes (AuthGate)
    http/this.cs                     +  http channel kind, bidirectional; Mime from Content-Type
  serializer/
    IWriter.cs                       ·  write abstraction
    IReader.cs                       +  read abstraction — mirror of IWriter
    json/writer.cs                   ·  json write — the text representation of the plang container
    json/reader.cs                   +  json read — mirror of json/writer.cs
    plang/this.cs                    ~  the application/plang container: read header → (type,kind) → defer body
    TimeSpanIso8601.cs               −  folds into type/duration/serializer/Default.cs Read

format/list/this.cs                  ~  TypeFromMime/TypeFromExtension: shape-based — json/xml/yaml→object, csv/xlsx→table
http/response/this.cs                −  deletes (dissolves into Data: body=value, status/headers/duration=properties)
error/IError.Wire.cs                 ~  ErrorWire folds into error's Read

module/
  file/read.cs                       ~  opens a file channel and reads (stops converting at read time)
  http/code/Default.cs               ~  opens the http channel; body→value, metadata→properties
  crypto/type/hash/this.cs           ~  FromWire → hash's Read
  signing/Signature.cs               ~  HashDataConverter folds into the reader path
```

Carved out — another branch owns snapshot: `app/snapshot/this.Wire.cs` (`FromWire`) and `app/this.SnapshotWire.cs` (`SnapshotToWire`/`SnapshotFromWire`/`ResumeFromWire`) keep their signatures; touch internals only to compile.

The shape of the whole change is symmetry — every write file gains its read mirror:

```
write                                 read
──────────────────────────────────   ──────────────────────────────────
type/renderer/this.cs              →  type/reader/this.cs
channel/serializer/IWriter.cs      →  channel/serializer/IReader.cs
channel/serializer/json/writer.cs  →  channel/serializer/json/reader.cs
<family>/serializer/Default.cs Write → <family>/serializer/Default.cs Read
```

## Part 1 — the reader registry

A new type mirroring `app.type.renderer.@this` (`app/type/renderer/this.cs:49`):

```csharp
namespace app.type.reader;

public sealed class @this
{
    // Mirror of renderer's `delegate void Write(object value, IWriter writer)`.
    // Read turns the value's own raw form into the value, using kind to pick the variant.
    public delegate object? Read(object raw, string? kind, ReadContext ctx);

    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _generated = new();
    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _runtime  = new();

    public Read? Of(string typeName, string? kind);   // "*" wildcard from Default.cs, like the renderer
}
```

Discovery mirrors the renderer exactly: a static `Read` sits next to `Write` in `app/type/<name>/serializer/<format>.cs`. `Default.cs` is the wildcard; a type that reads a kind specially gets a per-kind file. Same file, both halves of the type's serialization — not a separate `reader/` tree (that would split a type's two halves across folders).

**Two read layers, mirroring the two write layers.** On write, the channel serializer owns the format (`IWriter`, `json/writer.cs`) and the type owns its value (`Write(value, IWriter)`). Read mirrors that: the channel serializer decodes bytes → a structure (`IReader`, `json/reader.cs`), and the type builds its value from that (`Read(raw, kind, ctx)`). So `IReader` is the format-decode surface, `type.Read` is the value-materialize step — different layers, same split the write side already has. For reading a raw file/http payload there's no surrounding wire structure, so `raw` is the decoded-or-undecoded source form and the type reads it directly per `kind`. The layer-1 wire container is `application/plang` — the Data shape `{name, type, kind, value, properties, signature}` — in a binary *or* a text representation (json is the default text form, but it's configurable). "json on the wire" is just plang's text representation, not a peer format. A bare `application/json` body, by contrast, isn't the container — it's a value, stamped from its Mime like any other.

**Decision — type is the data's shape, kind is the encoding, raw is the source.** `type` names what the value *is* by shape — `object` for hierarchical/tree data, `table` for a grid of rows and columns, `text` for prose, plus `image`, `number`, etc. — not how it was encoded. `kind` is the encoding within that shape: `json`/`xml`/`yaml` for object, `csv`/`xlsx` for table, `png`/`jpg` for image, `int`/`uint` for number. `raw` is the source bytes/string, held lazily. The reader dispatches on `(type, kind)`. So `config.json` → `{object, json}` (this keeps today's `Format.TypeFromMime` behavior — `app/format/list/this.cs:443`); `report.csv`/`.xlsx` → `{table, csv}`/`{table, xlsx}`. **Stamping the type does not parse** — `type=object` is a promise about the shape on materialization, not an instruction to produce it now. `%cfg%` untouched is the raw json string; `%cfg.port%` navigates, which runs the `(object, json)` reader and parses on the spot. Grouping by shape (not encoding) is what makes csv and xlsx the *same* type and lets the renderer draw a table by dispatching on `type=table` alone. The wire-container axis (the `application/plang` representation, above) is separate and owned by the serializer — it doesn't enter this keying. Confirm with a round-trip (write a path into json, read it back) before the rest.

**What folds in** (each becomes a type's `Read`, reached through the registry):

- `type.Convert(raw)` (`app/type/this.cs:257`) → the registry's dispatch entry.
- The per-family `Convert` hook invoked by `app.type.convert.Of` (`app/type/convert/this.cs:28`) → each family's `Read`.
- `FromWire` / `WireReader` (`app/type/this.cs:282`; impls on crypto.hash, snapshot) → each type's `Read`. **Snapshot carve-out:** leave `snapshot.FromWire` and `app.Snapshot*` signatures; another branch owns snapshot. Adjust their internals only if needed to compile.
- `path.JsonConverter` (`app/type/path/this.JsonConverter.cs`) → path's `Read`; the 6 registration sites (`Diagnostics/Format.cs:31`, `channel/serializer/Json.cs:47`, `channel/serializer/plang/this.cs:51`, `module/builder/this.cs:50`, `this.cs:420`, `type/list/Conversion.cs:42,64`) stop wiring a path-specific JSON converter.
- `type.json` (`app/type/this.json.cs`) and the domain-coupled `JsonConverter<T>` set (`ErrorWire`, `signing.HashDataConverter`, `TimeSpanIso8601`) → `Read` entries. The genuinely STJ-shape plumbing for JSON itself stays inside the json reader.

**Distribute `OwnerOf`.** The `clr → (family, kind)` switch in `app/type/convert/this.cs:58` becomes a declaration on each family — `number` declares the numeric CLR types it owns, `text` declares `string`, `path` declares its subclasses. The registry composes the routing from those declarations. The central `if u == typeof(int) …` ladder dies. (This is also what lets Part 3 add new numeric kinds by editing only `number`.)

**Error handling.** A `Read` that fails (malformed json, wrong shape) produces an error rather than throwing into the courier. Materialization failures surface through `As<T>()` / navigation, which already return `Data` and carry an `Error`. The bare `.Value` getter is best-effort (caches the error, returns null). See "Behavior changes to watch" — failures move from read-time to touch-time.

The residual generic plumbing in `TryConvert` (`app/type/list/Conversion.cs`) that is *not* a type-owned read — nullable unwrap, the assignable fast-path, list element-walk — stays as the registry's fallback. Only the type-owned branches move onto types.

## Part 2 — lazy Data

Data gains a raw backing and materializes through the Part 1 registry on first touch.

```csharp
private object? _raw;      // the undecoded source form: string for text, byte[] for binary
private object? _value;    // materialized; null until first touch of a raw-backed Data
private type?   _type;     // carries Name + Kind

public virtual object? Value
{
    get
    {
        if (_valueFactory != null) { _value = _valueFactory(); _valueFactory = null; }
        if (_value == null && _raw != null)
            _value = Materialize();   // reader.Of(_type.Name, _type.Kind)?.Read(_raw, _type.Kind, ctx)
        return _value;
    }
}
```

Key rules:

- **Materialize only when `_value` is null and `_raw` is non-null.** Inline-authored values (`set %x% = 5`) populate `_value` and leave `_raw` null — they never hit the byte path, and the existing `%var%`-resolves-fresh-per-read contract (`app/data/this.cs:152`) is untouched. Which field is set tells you the origin; no mode flag.
- **`_raw` is `string | byte[]`, not always bytes.** Per Decision 3, text stays text — a json file's raw is the json string, an image's raw is `byte[]`. No utf-8 encode tax on the common path. The proposal called this field `bytes`; it actually holds the raw source form, so name it `raw` (or similar) to avoid implying everything is bytes.
- **`_raw` stays authoritative until a mutation.** Materialization is a read-through: it sets `_value` but does **not** clear `_raw`. On serialize, if the value was never touched (`_value` null), emit `_raw` verbatim — this is the free passthrough and the signature-safe form. A mutation (`SetValueDirect`, navigation-set) invalidates `_raw` so serialize then renders from `_value` via the renderer.
- **Fold `ConvertValue`.** The string→typed-on-first-navigate path (`app/data/this.cs:199`, `this.Navigation.cs`) is subsumed: a raw-backed Data materializes through the reader. Remove `ConvertValue` once navigation reads `.Value` (which materializes).
- **Keep `_valueFactory` / `DynamicData`** (`app/data/this.cs:186`, `:1205`). It is a *different* laziness — recompute-on-every-access (a live view), versus materialize-once-and-cache. Two lazinesses that mean different things; the design keeps both and says why rather than forcing them into one.

**Wire.Read goes lazy too.** Today `Wire.Read` (`app/data/Wire.cs:141`) eagerly deserializes the value slot (`LiftDataIfShaped` / `Deserialize<object?>`). Change it to capture the value slot's raw json (e.g. the raw token text) into `_raw`, stamp `type`/`kind` from the type slot, and defer. This is what makes wire-sourced Data pass through verbatim and verify against the original bytes. It also **deletes `LiftDataIfShaped`** (`app/data/Wire.cs:346`): that method sniffs the value's json shape — "does it have both `name` and `value` keys?" — to guess whether it's a nested Data, with a `GetRawText` double-parse on top. That guess (a courier reading the value's shape, smell #7, and the read-side twin of the sniffing we cut) goes away — the `type` slot says what the value is, and a genuinely nested Data is reconstructed by the containing type's own reader (e.g. `Signature` rebuilds its Data field), not by a key-shape heuristic.

## Part 3 — numbers (Way 3)

Replace the `_i/_d/_f` union (`app/type/number/this.cs:27`), the `NumberKind` enum (`:216`), the `Kinds` list (`:48`), and the `float`→`double` collapse (`this.Build.cs:25`, `app/data/this.cs:242`).

- **Store the exact C# value; the kind is its type.** A `uint` is held as a `uint`, a `BigInteger` as a `BigInteger`. The kind is derived from the value's CLR type — no separate label to drift. (Boxing is already the baseline since `Data.Value` is `object`; an internal struct optimization is the coder's call, but the model is "exact type, kind derived.")
- **Full C# scalar tower as kinds:** `sbyte byte short ushort int uint long ulong`, `Int128 UInt128`, `Half float double`, `decimal`, `BigInteger`. Under lazy, an untouched number off the wire is just its text (lossless) carrying a kind hint, materialized to the exact type on touch.
- **Arithmetic — promote then narrow** (the area to validate against C# semantics during this stage):
  - integers → promote to `BigInteger`, compute, narrow to a result kind.
  - binary floats (`Half`/`float`/`double`) → promote to `double`.
  - `decimal` → stays `decimal`.
  - integer ⊕ binary float → `double` (C#'s rule). integer ⊕ `decimal` → `decimal`.
  - `double` ⊕ `decimal` → **error, requires an explicit cast.** Neither represents the other exactly; C# forbids it without a cast and so should PLang. This is the "correct not easy" edge — don't silently pick one.
  - **Result kind** = the wider of the two operand kinds, widened further only if the value overflows it. So `int + int` stays `int`, but `3000000000u + 2000000000u` lands as `long` (not a silent `uint` wrap). Division producing a fraction → `decimal` or `double` per operand kinds.
- **`number` declares its CLR types** (the distributed `OwnerOf` from Part 1) — adding `uint`/`ulong`/`BigInteger` is an edit to `number` alone.
- Anything "N-wide" (a `uint4`-style vector) is a **list of numbers**, not a kind. No vector type is introduced.

This lands on top of Part 1 (number reads toward its exact kind through the reader) and is independent of Parts 4–5.

## Part 4 — one I/O boundary

Channel is the foundational I/O layer — a stream with a `Mime` and a serializer (`app/channel/this.cs`). There is one read verb, `channel.read`. file and http stop being self-contained I/O actions and become channel kinds; all kinds (the new ones and the existing `stream`/`session`/`message`/`event`/`goal`/`noop`) live under `channel/type/`. So every read enters through the one boundary.

- **`channel.read`** — the base channel `Read` (`app/channel/this.cs:101`) becomes the boundary. It stamps `type`/`kind` from the channel's `Mime` (`:38`) via the existing mime mapping (`Format.Mime`, `Format.TypeFromMime`, `ClrFromMime` — already used by `FilePath.ReadText`) and produces `Data { raw = stream content, type, kind, value: lazy }`. Today the stream channel returns bare text (`app/channel/type/stream/this.cs:69`); that stops.
- **file becomes a channel** — a filesystem channel kind (`app/channel/type/file/`), `Mime` from the extension. It reads bytes through `path.ReadBytes` — which holds the AuthGate, so the channel does no `System.IO` of its own (PLNG002 stays clean). `file.read` (`app/module/file/read.cs:27`) opens the channel and calls `channel.read` instead of converting at read time; the `Context.App.Type.Convert(text, materialized, …)` call in `FilePath.ReadText` (`app/type/path/file/this.Operations.cs:61`) goes away. (It can reuse the existing `stream/` kind if a separate one isn't worth it — the coder's call.)
- **http becomes a channel** — a bidirectional channel kind (`app/channel/type/http/`): write the request, read the response. The response **body** is the lazy value (type/kind from `Content-Type`); **status, headers, duration** become Data **properties** (read with `!`) — what `BuildProperties` already attaches. `http.get` (`app/module/http/code/Default.cs:463`) opens the channel and reads; it stops deserializing by `Content-Type`. `http.response.@this` (`app/http/response/this.cs`) **dissolves** — the result is plain Data:

  ```
  Data {
    value:      <lazy body>                 // %response%        → body (materializes on touch)
    type/kind:  object / json  (Content-Type)
    properties: { status:200, headers:{…}, duration:0.12s }   // %response!status%, %response!content-type%
  }
  ```
  ```
  - get http https://api/...     write to %response%
  - if %response!status% == 200   / property read — body untouched
      - write out %response.name%  / body materializes: raw → json → .name
  ```

- **Shape-based MIME mapping** — `Format.TypeFromMime` / `TypeFromExtension` (`app/format/list/this.cs:415,446`) stamp by shape: json/xml/yaml → `object` (keeping today's json→object), csv/xlsx → the new `table` type. `config.json` → `{object, json}`; `report.csv` → `{table, csv}`. An `application/json` http body stamps the same way.

## Part 5 — access-driven resolution (no guessing)

The kind of access decides materialization; nothing sniffs content.

- **Scalar / output** (`%x%`, `write out %x%`) → if `_raw` is bytes, decode utf-8 (stay bytes if it doesn't decode); if text, the string.
- **Navigation** (`%x.field%`) → materialize through the known type's reader; `kind` says how. If the type is unknown → **clear error**: "value has no type; add `as <type>`."
- **`as <type>`** → read toward that type.
- **Property** (`%x!prop%`) → read from Data.Properties; never touches the value (so status checks don't materialize the body).

No content sniffing. A guess at json/xml/yaml/csv contradicts "the type reads itself" and PLang's determinism — when the type is unknown, nothing reads it, so we error and ask for a cast.

## Leaf trace — incumbents and disposition

| Mechanism | Where | Disposition |
|---|---|---|
| `type.Convert(raw)` | `app/type/this.cs:257` | Dispatch entry into the reader registry. |
| `AppTypes.TryConvert` (14-branch) | `app/type/list/Conversion.cs` | Type-owned branches move onto each type's `Read`; generic plumbing stays as residual. |
| `app.type.convert.Of` (family `Convert` hook) | `app/type/convert/this.cs:28` | Becomes the type's `Read`. |
| `OwnerOf` (clr→family switch) | `app/type/convert/this.cs:58` | Distributes onto each family. |
| `FromWire` / `WireReader` | `app/type/this.cs:282`, crypto.hash, snapshot | Become `Read`. Snapshot signatures carved out. |
| `path.JsonConverter` (+6 sites) | `app/type/path/this.JsonConverter.cs:24` | Deletes; path gets a format-agnostic `Read`. |
| `type.json` converter | `app/type/this.json.cs:20` | Folds into the registry. |
| `ErrorWire`, `HashDataConverter`, `TimeSpanIso8601`, `EmptyStringToNull…` | `IError.Wire.cs:33`, `Signature.cs:49`, `TimeSpanIso8601.cs:15`, `JsonString.cs:244` | Domain-coupled ones → `Read`; pure json plumbing stays. |
| `Data.ConvertValue` + lazy-on-navigate | `app/data/this.cs:199`, `this.Navigation.cs` | Folds into materialize-from-raw. |
| `_valueFactory` / `DynamicData` | `app/data/this.cs:186`, `:1205` | Stays (different laziness). |
| `Wire.Read` (eager value slot) | `app/data/Wire.cs:141` | Captures value slot raw, defers materialization. |
| `LiftDataIfShaped` (sniffs value for `name`+`value` keys) | `app/data/Wire.cs:346` | Deleted — type slot drives reconstruction; nested Data rebuilt by the containing type's reader. No shape-guess, no `GetRawText` double-parse. |
| Renderer registry + `Normalize` gate | `app/type/renderer/this.cs:49`, `this.Normalize.cs:33,156` | Reference shapes for the reader. |
| `file.read` + `FilePath.ReadText` | `app/module/file/read.cs:27`, `path/file/this.Operations.cs:61` | Become a byte source; stop converting at read. |
| `http.get` / `ParseResponseAsync` | `app/module/http/code/Default.cs:463,518,551` | Stop deserializing; body→value, metadata→properties. |
| `http.response.@this` | `app/http/response/this.cs:10` | Dissolves into Data. |
| `channel.read` (raw text, ignores Mime) | `app/channel/stream/this.cs:69` | The single boundary; stamps type/kind from Mime. All channel kinds move under `channel/type/`. |
| `Format.TypeFromMime` / `TypeFromExtension` | `app/format/list/this.cs:415,446` | Shape-based: json/xml/yaml → `object` (unchanged), csv/xlsx → new `table` type. |
| Number union + float collapse | `app/type/number/this.cs:27,48,216`, `Build.cs:25`, `Convert.cs:40`, `data/this.cs:242` | Replaced by Way 3. |

## Behavior changes to watch

- **Parse errors move from read-time to touch-time.** A malformed json file no longer errors at `read` — it errors at first touch (navigation / `As<T>`). This is the point of laziness and is acceptable, but it relocates where a developer sees the failure. Make the touch-time error name the source ("failed to read %x% as json").
- **Signature verification fires on the raw, before materialization** (Decision 8). When a signed Data arrives at a boundary, verify `_raw` against the signature there; materialization is independent and later. Confirm the verify path never forces `.Value`.
- **Verbatim passthrough depends on `_raw` surviving materialization.** If anything clears `_raw` on read, passthrough and signing break. The invalidate-on-mutation rule is the one to test hard.
- **Nested signed Data round-trips without the shape sniff.** Deleting `LiftDataIfShaped` means nested Data is rebuilt by the type slot / containing type's reader, not a `name`+`value` key guess. Test that a signed, nested Data round-trips and its inner signature still reaches `signing.verify` — that's exactly the case the sniff was covering.

## Stages

Full stage docs and tests come later; this is the sequence and the hard dependency.

1. **Reader registry + OBP cleanup** — Part 1. No behavior change; pure consolidation. Green before anything else.
2. **Numbers (Way 3)** — Part 3. Independent of 3–5; can land right after 1.
3. **Lazy Data** — Part 2. Depends on 1.
4. **One I/O boundary** — Part 4. file/http become channel kinds under `channel/type/` (file via `path.ReadBytes`); `json⇒text` mapping; `http.response` dissolves. Depends on 1–3.
5. **Access-driven resolution** — Part 5. Depends on 4.

The only hard ordering constraint is **1 before everything**. Each stage green before the next.

## Out of scope

- **`.plang` container internals** (Decision 7) — the self-describing `application/plang` format whose header carries type+kind, in a binary or text (json by default, configurable) representation. This branch only assumes the boundary can read type+kind from such a payload; designing the container is a separate branch.
- **Rendering `table` to a UI** — the *write* mirror: a `(table, html)` renderer that draws a grid. With `type=table` this is a plain renderer entry (dispatch on `type=table`), not the kind-awareness the earlier todo described — that's the payoff of typing csv/xlsx by shape. The read half (the value carries `type=table` to the render boundary) is here; the grid renderer is a follow-on. Captured in `Documentation/Runtime2/todos.md`.
- **`(table, xlsx)` reader** — xlsx is binary (zip + xml, needs a library). The `table` type and the `(table, csv)` reader land here; xlsx parsing is a follow-on. A `.xlsx` still stamps `{table, xlsx}` and rides as raw bytes until its reader exists.
- **Snapshot rename to OBP names** — another branch; snapshot's `FromWire` / `app.Snapshot*` signatures stay.
- **SIMD / vector numeric types** — a `uint4`-style value is a list of numbers, not a number kind.
