# Dispatch — per-(type, format) serializer files

This file locks the serialization-dispatch contract. The spine ([../plan.md](../plan.md)) locks the architectural decision; this is the implementation shape.

**The shape in one line:** one small file per (type, format), `app/types/<name>/serializer/<format>.cs`, each owning exactly one rendering; the source generator wires the dispatch table. No interface on the value, no mime switch inside a method — the value is a dumb data holder, the rendering lives beside the type, one file per output medium. (A method that switches on format internally is the smell this avoids — distinct (type × format) combinations are distinct files.)

## What exists today

The wire pipeline is already two layers, and both are the right shape to build on:

1. **`app.data.this.Normalize(View)`** walks the value-graph into a uniform tree whose runtime types are limited to: `null`, primitives, `byte[]`, `app.data.@this`, `IList`. Domain objects get decomposed by reflection into a property bag, respecting `[Out]`/`[Sensitive]`/`[Masked]` per `View`. Lives at `PLang/app/data/this.Normalize.cs:42`.
2. **`app.channels.serializers.IWriter`** is the format-encoder protocol — one impl per format (today `app.channels.serializers.json.Writer`; protobuf/CBOR later). Primitive-typed methods (`Int`, `Long`, `Decimal`, `Double`, `String`, `Bytes`, `BeginArray`/`EndArray`, `BeginRecord`/`EndRecord`). `PLang/app/channels/serializers/IWriter.cs:19`.
3. **`app.types.@this` registry** — `[PlangType("name")]` discovery + `ResolveName(Type)` / `ResolveType(name)` / `RegisterRuntime(name, type)`. `PLang/app/types/Registry.cs`. This already exists; the plang-types branch extends it, it does not invent it.

The reflection decomposition in (1) is fine for plain domain objects (Identity, Signature, a user record). It is **not** fine for format-asymmetric types — image renders as base64 in JSON, raw bytes in protobuf, a path placeholder in text. A property bag can't express that. That's what the per-format serializer files are for.

## The folder shape

Each type owns a `serializer/` subfolder. One file per format that renders differently; a `Default.cs` catch-all for the rest:

```
app/types/image/serializer/
    text.cs        (image, text)      → writer.String(path placeholder)
    protobuf.cs    (image, protobuf)  → writer.Bytes(raw)
    Default.cs     (image, *)         → writer.String(base64)
app/types/number/serializer/
    Default.cs     (number, *)        → writer.Int/Long/Decimal/Double(...)
app/types/code/serializer/
    Default.cs     (code, *)          → writer.String(source)
app/types/path/serializer/
    Default.cs     (path, *)          → writer.String(Relative)
```

Most types need only `Default.cs` — number, code, and path render the same regardless of format. Image is the one proving type that needs format-specific files (text → path, protobuf → bytes), and even it leans on `Default.cs` for json/plang (both base64). So the "~75 tiny files" worry from the first draft collapses: it's ~1 file for most types, ~3 for the asymmetric ones. The folder reads like a manifest of *what this type can do, per output medium*, and an empty-looking `serializer/` with one `Default.cs` says "renders uniformly" at a glance.

### A single file

```csharp
// app/types/image/serializer/protobuf.cs
namespace app.types.image.serializer;

public static class protobuf
{
    public static void Write(image.@this value, IWriter writer)
        => writer.Bytes(value.Bytes);
}
```

```csharp
// app/types/image/serializer/text.cs
namespace app.types.image.serializer;

public static class text
{
    public static void Write(image.@this value, IWriter writer)
        => writer.String(value.SourcePath ?? $"[image: {value.Mime} {value.Bytes.Length}B]");
}
```

```csharp
// app/types/image/serializer/Default.cs   (Default — `default` is a C# keyword, PascalCase per the filesystem/Default precedent)
namespace app.types.image.serializer;

public static class Default
{
    public static void Write(image.@this value, IWriter writer)
        => writer.String(System.Convert.ToBase64String(value.Bytes));
}
```

No switch. No mime string in the body. The file name **is** the format selector; the folder name **is** the type. Each file is one decision in one place. The `Write` method takes the concrete value type and the format-agnostic `IWriter` — a file uses only the writer primitives, never reaches for a concrete writer subclass. (If a format ever needs a richer primitive than `IWriter` exposes, that's a signal to add a method to `IWriter`, not to couple a type file to `json.Writer`.)

## The writer carries its format token

`IWriter` gains one read-only property:

```csharp
public interface IWriter
{
    /// <summary>Short format token — "json", "plang", "text", "protobuf". The
    /// (type, format) serializer lookup key. Maps to the serializer's mime,
    /// but type files match on this token, never the mime string.</summary>
    string Format { get; }
    // ... existing primitive methods ...
}
```

`json.Writer.Format => "json"`. The serializer registry (`app/channels/serializers/this.cs`) already maps mime → serializer; the writer just surfaces its own short token. Type-serializer files key off this token, so the smell of `case "application/json":` strings sprinkled through type code never appears.

## The dispatch table — generator-wired

The source generator scans `app/types/*/serializer/*.cs` (discovery-time, the registry decision from the spine). For each file it emits one registration into a static table:

```
(typeName, formatToken) → static void Write(object value, IWriter writer)
```

`Default.cs` registers under the wildcard token `"*"`. So the table for the proving types is:

```
(image,  "text")     → app.types.image.serializer.text.Write
(image,  "protobuf") → app.types.image.serializer.protobuf.Write
(image,  "*")        → app.types.image.serializer.Default.Write
(number, "*")        → app.types.number.serializer.Default.Write
(code,   "*")        → app.types.code.serializer.Default.Write
(path,   "*")        → app.types.path.serializer.Default.Write
```

**Build-time gate (PLNG).** Every `[PlangType]` must have either a `Default.cs` or a file for every registered format token. The generator emits a PLNG diagnostic at error severity otherwise. This is what guarantees a runtime lookup for a registered type can never miss — there is always a specific file or the wildcard.

## How it hooks into the existing pipeline

Two touch points, using a deferred marker:

**Normalize tags, doesn't render.** When the walk reaches a value whose CLR type resolves to a PLang name (`Registry.ResolveName(value.GetType())` is non-null) and that type has at least one registered serializer, Normalize wraps it as a marker instead of reflecting it:

```csharp
// in NormalizeValue, before the reflection branch
if (Registry.ResolveName(value.GetType()) is string typeName
    && TypeSerializers.Has(typeName))
{
    return new TypedValueNode(value, typeName);   // sealed record TypedValueNode(object Value, string TypeName)
}
// else: today's reflection walk into a property bag, unchanged
```

Normalize stays format-agnostic — it only tags. Unregistered domain objects (no `[PlangType]`) fall straight through to the existing reflection decomposition; nothing about them changes.

**The writer resolves the marker.** `json.Writer.Value(object?)` (`PLang/app/channels/serializers/json/writer.cs:113`) gains one case:

```csharp
case TypedValueNode tv:
    var write = TypeSerializers.Get(tv.TypeName, Format)      // specific (type, format)
             ?? TypeSerializers.Get(tv.TypeName, "*");        // Default.cs wildcard
    write(tv.Value, this);                                    // build-gate guarantees non-null
    return;
```

The writer knows its own `Format`; it does the (type, format) lookup and calls the generator-wired method. No other writer code changes — every future `IWriter` impl (protobuf, CBOR) gets the same one case and the whole type vocabulary works for it for free.

## The flow, concretely

`Wire.Write` is emitting `Data{Type="image", Value=<Image>}` through the json writer:

1. `Wire.Write` calls `data.Normalize(View)`. The walk reaches the `Image` value.
2. `Registry.ResolveName(typeof(image.@this))` → `"image"`; `TypeSerializers.Has("image")` → true. Normalize returns `TypedValueNode(<Image>, "image")`.
3. `Wire.Write` hands the normalized tree to `jsonWriter.Value(...)`. It reaches the `TypedValueNode`.
4. Writer's `Format` is `"json"`. Lookup `("image", "json")` → miss; lookup `("image", "*")` → `image.serializer.Default.Write`.
5. Call `Default.Write(<Image>, jsonWriter)` → `jsonWriter.String(base64)`.

Switch the run to protobuf: step 4 looks up `("image", "protobuf")` → hit → `protobuf.Write` → `writer.Bytes(raw)`. Same value, same step, different writer, different wire shape — and the channel never branched on type, the type never named a channel.

## Runtime-loaded and overwritten types

A type loaded from an external DLL at runtime (`- load mynumbers.dll`) can't be generator-wired — the generator already ran at PLang's build. So the dispatch table needs a **runtime-registration path** alongside the generated one, and the existing `code.load` machinery is the template.

`code.load` (`PLang/app/modules/code/load.cs`) today: loads a DLL, scans `GetExportedTypes()` for `ICode` implementations, registers each instance. The type-loading feature generalizes the same shape:

1. Scan the DLL for `[PlangType]`-bearing classes → `Registry.RegisterRuntime(name, clrType)` (the existing hook at `Registry.cs:103`).
2. Scan for the loaded type's renderers and register them as delegates into the same dispatch table the generator feeds: `TypeSerializers.RegisterRuntime(typeName, formatToken, writeDelegate)`. The DLL exposes them via a small interface the loader can reflect — the type-system analogue of `ICode` (call it `ITypeRenderer { string Format { get; } void Write(object value, IWriter writer); }`), so a loaded assembly ships one renderer instance per format it supports.

**Overwriting `int` works because resolution already favors runtime.** `Registry.ResolveType` checks `_runtimeNameToType` before the discovery-time table (`Registry.cs:85`), so `RegisterRuntime("int", customType)` shadows the built-in mapping for everything that resolves types by name — including the value slot's serializer dispatch. The runtime serializer table follows the same precedence (runtime registration wins over generated).

**The honest limit on overwriting built-ins.** Runtime registration changes *resolution and serialization* — what a name maps to, how a value renders. It cannot change what the **source generator already baked at build**: PLNG001 parameter-slot validation, the `Data<int>` slots emitted on existing action handlers, the compile-time type stamps in shipped `.pr` files. So `- load myint.dll` can make `int` resolve to a custom type and render differently, but a handler compiled against the built-in `int` still sees the built-in at its typed slot. Adding **new** types is unconstrained; overwriting built-ins is "new resolution + new rendering, same compiled slots." Worth stating loudly in the user-facing docs so nobody expects `- load myint.dll` to retroactively rewrite the arithmetic of already-built goals. This is captured as a cross-cutting note in [../plan.md](../plan.md) "Extending the type vocabulary at runtime".

## The parse-in side — `Resolve`, unchanged

Emit is 1-to-many (one type, a file per format). Parse is many-to-one (any format → one type). So parse-in does **not** get per-format files — it stays on the type's `static Resolve(string|byte[], context)` (the existing factory convention, on `this.cs` / `this.Parse.cs`):

- JSON / plang: the deserializer hits a `"value"` slot, gets a string. If the surrounding `Data.Type` is `"image"`, it calls `image.@this.Resolve(string, context)`, which sees base64 and `Convert.FromBase64String`s it back to bytes.
- protobuf: a bytes slot → `image.@this.Resolve(byte[], context)` overload, constructs directly.
- text: a string slot → `Resolve` interprets (path-shaped → image-backed-by-path; base64 → bytes; neither → typed parse error).

The type owns deserializing itself: the dispatch picks the type by `Data.Type`, and the type's `Resolve` overloads pick the construction path by incoming primitive shape. `Conversion.TryConvertTo` grows one branch — typed parse via `Resolve(byte[], context)` when the input is binary and `Data.Type` is set.

## What changes vs. what stays

**Stays:**
- `Data.Normalize`'s tree shape and the View-based filter discipline. The only addition is the tag-instead-of-reflect branch for registered types.
- `IWriter`'s primitive vocabulary (no new value methods — types route through existing slots). One new read-only `Format` property.
- The reflection walk for unregistered domain objects (Identity, Signature, user records) — untouched.
- The plang serializer's outer-shape contract (`{name, type, value, properties, signature}`). The serializer dispatch only governs the `value` slot.

**Changes / new:**
- New per-(type, format) files under `app/types/<name>/serializer/`. Static `Write(<Type>, IWriter)` each.
- New static dispatch table `TypeSerializers` (generator-emitted) with a runtime-registration path for loaded types.
- New `sealed record TypedValueNode(object Value, string TypeName)` and the tag branch in `NormalizeValue`.
- One `case TypedValueNode` in each `IWriter`'s value-dispatch.
- One `Format` property on `IWriter` + impls.
- Generator: scan `serializer/*.cs`, emit registrations; PLNG gate for missing-coverage.
- `code.load` sibling (or extension) that registers `[PlangType]` classes + their `ITypeRenderer`s from a loaded DLL.

`path`'s existing `this.JsonConverter.cs` (the legacy single-format converter) is absorbed: its logic moves into `app/types/path/serializer/Default.cs`, and `this.JsonConverter.cs` is deleted once STJ-pathway callers route through the new dispatch.

## Edge cases

**Cycles.** Registered-type values are leaves by definition — no cycle through them. Normalize's visited-set still guards the surrounding graph.

**Sensitive fields.** A registered type fully controls what its serializer files emit; the `[Sensitive]` reflection filter doesn't apply (the value isn't decomposed). Rule: a type carrying secret payload (private key) either doesn't register a serializer (falls to the reflection walk so `[Sensitive]` applies) or its serializer files explicitly mask. Documented on the type.

**Nested registered types.** A `Document` whose body is a `code` value: the document's serializer file calls `writer` brackets and, for the nested `code`, can re-enter the dispatch (`writer.Value(new TypedValueNode(body, "code"))` or a helper). The writer's `Value` dispatch handles nesting identically to top-level.

**Unknown format with no Default.** Can't happen for a registered type — the build gate requires `Default.cs` or full coverage. For a *runtime-loaded* type the loader must register at least a `"*"` renderer or the load fails with a typed error (mirrors `code.load`'s "no parameterless constructor" rejection).

**Round-trip.** Every registered type needs a `BuilderSanity`-style test: construct an instance, emit through each format's writer, `Resolve` back, assert equality (via the type's lenient equality — see [storage.md](storage.md)). At least `application/plang` must round-trip losslessly.
