# Defining a PLang type in C# — and its read/write (I/O) layer

This is the guide for adding a new value type to PLang (a `number`, `path`, `choice`,
`image`, …) and wiring how it crosses the wire. Two ideas run through everything:

1. **A type knows how to create itself.** All construction lives *on the type* — never in
   a static helper class beside it. A static helper (`FooMeta`, `FooUtil`) that a type
   leans on to build itself is a smell; dissolve it onto the type.
2. **Format never leaks into a type.** A value reads and writes through the abstract
   `IReader`/`IWriter` — it never names STJ, JSON, CSV, or any concrete serializer. If you
   see a `[JsonConverter]`, a `Utf8JsonReader`, or `System.Text.Json` inside a value type,
   that is the alarm: the format has leaked in. The one home for format is the
   `channel/serializer/*` layer, which hands your type an `IReader`/`IWriter`.

---

## 1. The type class

Every value type is a `sealed class @this : app.type.item.@this` living at
`app/type/<name>/this.cs` (the folder name is the PLang type name — `app/type/number`,
`app/type/path`, …). It implements `ICreate<@this>` so it can build itself, and it holds
its own backing value.

```csharp
namespace app.type.myvalue;

public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly <backing> _value;      // the CLR backing (string, long, byte[], a record, …)
    public @this(<backing> value) { _value = value; }
```

### Members you implement on `item.@this`

| Member | What it does | Notes |
|---|---|---|
| `Mint()` | Returns the type's `{name, kind}` self-description. | This is what `Data.Type` reports and what the wire writes as the value's declared type. |
| `Write(IWriter w)` | Renders the value to the wire — **format-independent** (see §3). | A leaf writes one token (`w.String(...)`); a container brackets its elements. |
| `IsLeaf` | `true` for a single-token scalar; `false` for a container (dict/list). | Drives whether Normalize brackets it. |
| `Clr(Type target)` | The CLR exit door — hand the backing to a .NET boundary. | Only at a real .NET/3rd-party edge (`ClrConvert(_value, target)`). Never lower mid-flight. |
| `ToString()` | The display/text face. | |
| `Value(Data)` | The value door — resolve/materialize (lazy read, template render, reference deref). | A plain scalar answers itself; a lazy/reference value resolves here. |
| `IsTruthy()` / `AsBooleanAsync()` | The value's boolean meaning. | Implement `IBooleanResolvable` if truthiness needs I/O. |
| `IsVariable` / `Get(ctx)` | Only if the value is a reference (a `%x%` naming a binding). | Content answers `false`/`null`; see the variable-as-value work. |

You do **not** implement `Peek()`, `Cacheable`, `Navigate`, etc. unless your type needs to
override the base behavior.

---

## 2. Construction — the type creates itself

Two entry points, both **static, on the type** (no helper class):

### `ICreate<@this>.Create(item value, Data data)` — the typed door (`As<T>`)

Called when a `Data<MyValue>` slot resolves (`GetParameter(...).As<MyValue>()`). Return the
built value, or `data.Fail(...)` + `null` to decline. Pass-through and facet handling come
free from the default implementation; override only if your type has a special construction
(e.g. re-tagging a `list` into a specialized collection).

### `Convert(object? value, string? kind, context)` — the family hook

The catalog discovers this static by reflection (`Conversions.Of`) and uses it to build your
type from a raw value (a string, a number). This is the legitimate plang-type hook — keep
it. It should delegate to the type's own build logic, not reimplement it:

```csharp
public static data.@this Convert(object? value, string? kind, context)
    => context.Ok(FromRaw(value, context));   // one build path, on the type
```

**Rule:** if you find yourself writing a `MyValueMeta` static class that the type calls to
build itself, stop — move that code onto the type (per-`T` static fields cache fine on a
closed generic). A worked example is `choice<T>`: its enum-vs-named-set resolution lives on
`choice<T>` itself (`Names`, `FromName`), not in a helper.

---

## 3. The I/O layer — read/write, format-free

This is the heart. A value crosses the wire through **two type-owned, format-agnostic**
methods. Neither knows what serializer is driving it.

### Writing — `Write(IWriter w)`

`IWriter` is the abstract sink: `Null()`, `Bool()`, `Long()`, `String()`, `Bytes()`,
`BeginArray(count)`/`EndArray()`, `BeginObject()`/`Name()`/`EndObject()`, `Raw()`. A leaf
writes one token; a container brackets. Example (a scalar):

```csharp
public override void Write(IWriter w) => w.String(ToString());
```

The concrete writer (a JSON writer, a text writer) is chosen by the *channel/format* layer —
your type only ever sees `IWriter`.

### Reading — `serializer/Reader.cs` (an `ITypeReader`)

Put the read at `app/type/<name>/serializer/Reader.cs`. It **auto-registers** by namespace
convention: a class in `app.type.<name>.serializer` implementing `ITypeReader` with a
parameterless ctor is registered under the `(name, Kind)` key at boot — no manual wiring.

```csharp
namespace app.type.myvalue.serializer;

public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;   // or "json"/"csv"/… per variant

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("myvalue", kind);
        // pull the value off the abstract reader — you never name a format:
        return new @this(reader.String());     // or reader.Long(), reader.Bytes(), BeginArray()…, per shape
    }
}
```

`IReader` is the abstract source, mirroring `IWriter`: `Peek()` (the current `TokenKind`),
`Null()`, `Bool()`, `Long()`, `String()`, `Bytes()`, `BeginArray()`/`NextElement()`/`EndArray()`,
`BeginObject()`/`NextName()`/`EndObject()`. The `ref TReader … allows ref struct` signature
lets a stack-only reader (`json.Reader` over `Utf8JsonReader`) cross with no boxing,
monomorphized per format. Read from the first token; leave the cursor on the last.

**The same `Reader` serves every format** — a `.pr` JSON token, a CSV payload, a byte blob.
That is the whole point: `bool`'s reader reads a bool the same way whether the bytes were
JSON or anything else.

### Kinds — the `(type, kind)` registry

A type that reads differently per kind (`table` reads `csv` vs `xlsx`; `choice` reads
`operator` vs `httpmethod`) uses `Kind` to name the variant, or `AnyKind` (`"*"`) to read
uniformly and switch on the passed `kind` inside `Read`. The registry key is `(typeName, kind)`
with an `AnyKind` fallback.

---

## 4. The born path — how a `.pr` value reaches your reader

```
.pr slot { name, type:{name, kind}, value }
  → Wire.ReadBody                         (data/reader/this.cs)  — reads the token
  → Data.FromRaw → type.Build             (type/this.cs)         — a string/bytes value DEFERS to…
  → item.source                           (a lazy carrier holding the raw + declared {type, kind})
  → …first touch (.Value())…
  → source.Read → serializers[format].Read (channel/serializer/*) — picks the format serializer
  → your ITypeReader.Read(ref IReader, kind, ctx)                 — the type reads itself
```

So a scalar value is born **lazy** (a `source`), and materializes through *your reader* on
first use. The declared `{type, kind}` in the `.pr` picks which reader — which is why the
wire type must name a registered type. (A container's raw is JSON; a scalar/value's raw is
its own content; a full-match `%ref%` is a reference and resolves through the variable door,
never a reader.)

---

## 5. Registering the type name

The reader auto-registers, but the **type name** must resolve in the catalog so the born path
can build the `{type, kind}`. Built-in types register from their `app.type.*` namespace;
a family type (like `choice`, whose closed forms surface under a kind) is registered
explicitly (`catalog.Register("choice", typeof(choice<>))`). If `type.Build` throws
`No PLang type registered under name 'X'`, the name isn't in the registry — register it.

---

## 6. Smells — stop if you see these

- **A `[JsonConverter]` / `System.Text.Json` / `Utf8JsonReader` inside a value type.** Format
  has leaked in. The wire form is `Write(IWriter)` + `serializer/Reader.cs`, nothing else.
- **A static helper class the type calls to build itself** (`FooMeta.Build`, `FooUtil.Parse`).
  The type creates itself; move the code onto the type. Static helper classes need explicit
  sign-off — default to *not* having one.
- **`.Clr` / `value.Clr<object>()` mid-flight.** Lowering to CLR then re-lifting is the
  raw-CLR-era smell. Stay in plang types; `Clr` is only for the final `.NET`/3rd-party edge.
- **The reader/writer knowing the concrete format.** If `Read`/`Write` names a serializer,
  the abstraction is broken — they see only `IReader`/`IWriter`.

## 7. Worked examples in the tree

- **Simplest scalar:** `app/type/bool/serializer/Reader.cs` — one token in, `bool` out.
- **Bytes / kinded:** `app/type/binary`, `app/type/image` — read bytes, kind narrows the type.
- **Container:** `app/type/list/serializer/Reader.cs` — `BeginArray()` / `NextElement()`.
- **Family + kind:** `app/type/choice/serializer/Reader.cs` — reads the option name off any
  reader, resolves the closed `choice<T>` from the kind, and lets the type build itself
  (`choice<T>.FromName`). No converter, no helper class.
