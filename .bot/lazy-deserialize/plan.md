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
SOURCE (file / http / channel / wire)
   │  produces
   ▼
Data { raw, type, kind, value:lazy, properties }
   │
   │  first touch of .Value  (and only then)
   ▼
app.type.reader.Of(type, kind) → Read(raw, kind, ctx) → value     ← mirror of app.type.renderer
   │
   │  raw stays authoritative until a mutation; serialize emits raw verbatim when value was never touched
   ▼
```

The whole plan is: build that registry (Part 1), make Data hold the raw and materialize through it (Part 2), broaden numbers so a type can read toward its exact kind (Part 3), route the I/O sources through one boundary (Part 4), and let the access pattern drive resolution without guessing (Part 5).

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

**The one subtlety to validate first — type vs kind vs format.** The renderer keys on `(type, channel-format)` because it encodes a value *into* a channel (json, plang). The reader keys on `(type, kind)` because it decodes the value's *own* raw form: `kind` is the type's refinement — for text it's the textual format (`json`, `csv`, `md`, `plain`), for number the precision (`int`, `uint`), for image the format (`png`, `jpg`). The channel-format axis (how the surrounding `Data{name,type,value,…}` structure is wire-encoded) stays the channel serializer's job and doesn't change. Confirm this keying holds for a round-trip (write a path into json, read it back) before building the rest — it's the load-bearing choice.

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

**Wire.Read goes lazy too.** Today `Wire.Read` (`app/data/Wire.cs:141`) eagerly deserializes the value slot (`LiftDataIfShaped` / `Deserialize<object?>`). Change it to capture the value slot's raw json (e.g. the raw token text) into `_raw`, stamp `type`/`kind` from the type slot, and defer. This is what makes wire-sourced Data pass through verbatim and verify against the original bytes.

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

`channel.read` becomes the single place raw + type/kind become a lazy Data.

- **`channel.read`** (`app/channel/stream/this.cs:69`) stamps `type`/`kind` from the channel's `Mime` (`app/channel/this.cs:37`) using the existing mime mapping (`Format.Mime`, `Format.TypeFromMime`, `ClrFromMime` — already used by `FilePath.ReadText`), and produces `Data { raw = stream content, type, kind, value: lazy }`. It stops returning bare text.
- **`file.read`** (`app/module/file/read.cs:27`, `app/type/path/file/this.Operations.cs:61`) stops converting at read time. It becomes a source: open the stream, hand it to the boundary with the extension-derived mime. The read-time `Context.App.Type.Convert(text, materialized, …)` call goes away.
- **`http.get`** (`app/module/http/code/Default.cs:463`) stops deserializing by `Content-Type`. The http channel is bidirectional — write the request, read the response. The response **body** becomes the lazy value (type/kind from `Content-Type`); **status, headers, duration** become Data **properties** (read with `!`), which is what `BuildProperties` already attaches. `http.response.@this` (`app/http/response/this.cs`) **dissolves** — the result is plain Data:

  ```
  Data {
    value:      <lazy body>                 // %response%        → body (materializes on touch)
    type/kind:  text / json  (Content-Type)
    properties: { status:200, headers:{…}, duration:0.12s }   // %response!status%, %response!content-type%
  }
  ```
  ```
  - get http https://api/...     write to %response%
  - if %response!status% == 200   / property read — body untouched
      - write out %response.name%  / body materializes: raw → json → .name
  ```

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
| Renderer registry + `Normalize` gate | `app/type/renderer/this.cs:49`, `this.Normalize.cs:33,156` | Reference shapes for the reader. |
| `file.read` + `FilePath.ReadText` | `app/module/file/read.cs:27`, `path/file/this.Operations.cs:61` | Become a byte source; stop converting at read. |
| `http.get` / `ParseResponseAsync` | `app/module/http/code/Default.cs:463,518,551` | Stop deserializing; body→value, metadata→properties. |
| `http.response.@this` | `app/http/response/this.cs:10` | Dissolves into Data. |
| `channel.read` (raw text, ignores Mime) | `app/channel/stream/this.cs:69` | The single boundary; stamps type/kind from Mime. |
| Number union + float collapse | `app/type/number/this.cs:27,48,216`, `Build.cs:25`, `Convert.cs:40`, `data/this.cs:242` | Replaced by Way 3. |

## Behavior changes to watch

- **Parse errors move from read-time to touch-time.** A malformed json file no longer errors at `read` — it errors at first touch (navigation / `As<T>`). This is the point of laziness and is acceptable, but it relocates where a developer sees the failure. Make the touch-time error name the source ("failed to read %x% as json").
- **Signature verification fires on the raw, before materialization** (Decision 8). When a signed Data arrives at a boundary, verify `_raw` against the signature there; materialization is independent and later. Confirm the verify path never forces `.Value`.
- **Verbatim passthrough depends on `_raw` surviving materialization.** If anything clears `_raw` on read, passthrough and signing break. The invalidate-on-mutation rule is the one to test hard.

## Stages

Full stage docs and tests come later; this is the sequence and the hard dependency.

1. **Reader registry + OBP cleanup** — Part 1. No behavior change; pure consolidation. Green before anything else.
2. **Numbers (Way 3)** — Part 3. Independent of 3–5; can land right after 1.
3. **Lazy Data** — Part 2. Depends on 1.
4. **One I/O boundary** — Part 4. Depends on 1–3.
5. **Access-driven resolution** — Part 5. Depends on 4.

The only hard ordering constraint is **1 before everything**. Each stage green before the next.

## Out of scope

- `.plang` self-describing header format (Decision 7). Stage 4 may stub "type/kind from the payload's own header."
- Snapshot rename to OBP names — another branch; snapshot's `FromWire` / `app.Snapshot*` signatures stay.
- SIMD / vector numeric types — a `uint4`-style value is a list of numbers.
