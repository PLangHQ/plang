# Dispatch — `IWireWritable` and the Normalize hook

This file goes deep on the serialization-dispatch surface introduced in the spine. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the implementation contract.

## What exists today

The wire pipeline is already two layers, and the second layer is the right shape:

1. **`app.data.this.Normalize(View)`** walks the value-graph into a uniform tree whose runtime types are limited to: `null`, primitives (string, int, long, double, bool, DateTime, decimal), `byte[]`, `app.data.@this`, or `IList`. Domain objects get decomposed by reflection into a property bag, respecting `[Out]`, `[Sensitive]`, `[Masked]` filters per the `View`. Lives at `PLang/app/data/this.Normalize.cs:42`.

2. **`app.channels.serializers.IWriter`** is the format-encoder protocol. One impl per format (today: `app.channels.serializers.json.Writer`; later: protobuf, CBOR). Methods are primitive-typed: `Int`, `Long`, `Decimal`, `Double`, `String`, `Bytes`, `BeginArray`/`EndArray`, `BeginRecord`/`EndRecord`. The writer walks the normalized tree and emits format-specific bytes. Lives at `PLang/app/channels/serializers/IWriter.cs:19`.

3. **`app.channels.serializers.serializer.ISerializer`** identifies a format by mime (`Type = "application/plang"`, `"text/plain"`, …) and owns the read/write entry points. Wraps an `IWriter`. Lives at `PLang/app/channels/serializers/serializer/this.cs:12`.

The slot to intercept is **(1)**. Today Normalize decomposes every domain object the same way: walk public properties, build a `Data` property bag. For format-asymmetric types (image: bytes / base64 / `<img>` markup / path depending on format) that doesn't work — a property bag isn't enough; the type needs to choose what its content looks like *for the format it's about to be encoded in*.

## What we add

A single marker interface on the value:

```csharp
namespace app.data;

/// <summary>
/// A value that knows how to render itself onto the wire for a specific format.
///
/// <para><see cref="Normalize"/> dispatches to <see cref="WriteTo"/> when the
/// wrapped value implements this interface, instead of decomposing into a
/// property bag via reflection. The value receives the active
/// <see cref="ISerializer"/> (carrying format identity — mime string,
/// extension) and the <see cref="IWriter"/> (the format encoder), and emits
/// its content through the writer's primitive vocabulary. The choice of
/// primitive (bytes vs base64 string vs markup string) is the type's, keyed
/// off the format.</para>
///
/// Kept here next to <c>Data</c> (the dispatcher) so <c>Data</c> depends on
/// the marker, not on any concrete value type.
/// </summary>
public interface IWireWritable
{
    void WriteTo(IWriter writer, ISerializer serializer);
}
```

Sync. Reaching for I/O during wire emission is a bug — the value should already hold its bytes (or its lazy-resolved equivalent) by the time it reaches the writer. Async dispatch lives at action verbs, where `image.resize` may read source bytes from disk; the result of that action is a fully-materialized `Image` instance with `Bytes` populated.

## How the dispatch hooks in

One branch added at the top of `NormalizeValue` (`PLang/app/data/this.Normalize.cs:42`), before today's tree-native and reflection branches:

```csharp
internal static object? NormalizeValue(object? value, View mode, HashSet<object> visited, int depth)
{
    if (depth > MaxNormalizeDepth) throw new NormalizeException(...);

    if (value is null) return null;

    // NEW: value owns its serialization for the active format.
    if (value is IWireWritable selfWriting)
        return new WireWritableNode(selfWriting);   // placeholder marker — see below

    // ... existing tree-native, nested-Data, dict, list, reflection branches ...
}
```

The complication: `Normalize` produces a *tree of primitives* that `Wire.Write` then walks. `IWireWritable.WriteTo` doesn't return primitives — it writes directly into the `IWriter`. So Normalize can't return the rendered content; it has to return a *deferred marker* that `json.Writer` (and future writers) recognize.

Two ways to handle that:

- **Option A — Deferred marker.** Normalize returns a `WireWritableNode { Value }` placeholder. The writer's value-dispatch method recognizes it and calls `Value.WriteTo(this, serializer)`. Clean separation; needs the writer to learn one new node type.
- **Option B — Eager render at Normalize-time.** Normalize gets the active `ISerializer`/`IWriter` passed in, and `IWireWritable.WriteTo` is invoked during the Normalize walk, writing into a buffer that's then spliced into the output. Avoids the placeholder, but couples Normalize to the writer (today Normalize is format-agnostic).

I lean **Option A**. Keeps Normalize format-agnostic; the writer adds one branch; future writers handle the same marker uniformly. The placeholder is one tiny record (`sealed record WireWritableNode(IWireWritable Value)`).

The writer-side branch lives in `app.channels.serializers.json.Writer.Value(object?)`:

```csharp
public void Value(object? normalized)
{
    switch (normalized)
    {
        case null:                  Null(); break;
        case WireWritableNode w:    w.Value.WriteTo(this, _serializer); break;  // NEW
        case @this data:            BeginRecord(data); /* ... */ EndRecord(); break;
        case bool b:                Bool(b); break;
        // ... existing primitive cases ...
    }
}
```

The writer needs to know which serializer it's a part of (for the `serializer` arg in `WriteTo`). Today `json.Writer` is constructed from a `Utf8JsonWriter` + options + `View`; we add the `ISerializer` to its constructor (the plang serializer holds the json writer; passing itself through is one extra ref). Trivial.

## What `WriteTo` looks like per type

### `number`

Format-uniform. Every wire format knows how to write a number primitive. The type's job is to dispatch on its `Kind` to pick which writer primitive carries it:

```csharp
public void WriteTo(IWriter writer, ISerializer serializer)
{
    switch (Kind)
    {
        case NumberKind.Int:     writer.Int((int)_i); break;
        case NumberKind.Long:    writer.Long(_i); break;
        case NumberKind.Decimal: writer.Decimal(_d); break;
        case NumberKind.Float:   writer.Float((float)_f); break;
        case NumberKind.Double:  writer.Double(_f); break;
    }
}
```

The `serializer` argument is unused — number doesn't render differently for JSON vs plang vs protobuf. (Text serializer's `String` falls back to `ToString()` for non-string writes, or text-writer has its own value-dispatch that handles numbers; either works.)

### `image`

Format-asymmetric — the whole point of the interface:

```csharp
public void WriteTo(IWriter writer, ISerializer serializer)
{
    switch (serializer.Type)
    {
        case "text/plain":
            // Path placeholder if we have one; size summary otherwise.
            writer.String(_sourcePath ?? $"[image: {_mime} {_bytes.Length}B]");
            break;

        case "text/html":
            // Inline data URL. When HTML grows its own writer (no JSON
            // escaping), this slot emits raw markup.
            writer.String($"<img src=\"data:{_mime};base64,{Convert.ToBase64String(_bytes)}\">");
            break;

        case "application/json":
        case "application/plang":
        case "application/plang+json":
            // Base64 string — the lossless JSON representation of bytes.
            writer.String(Convert.ToBase64String(_bytes));
            break;

        case "application/protobuf":
        case "application/octet-stream":
            // Native bytes — the lossless binary representation.
            writer.Bytes(_bytes);
            break;

        default:
            // Unknown format — default to base64 string, the broadest fallback.
            writer.String(Convert.ToBase64String(_bytes));
            break;
    }
}
```

The mime mapping lives in one place: on the type. Adding a new channel that wants HTML doesn't touch image; adding a new image variant (animated PNG, AVIF) might add internal state but doesn't touch any channel.

### `code`

Text-shaped with a language tag. Most formats render as a string; HTML wraps for display:

```csharp
public void WriteTo(IWriter writer, ISerializer serializer)
{
    switch (serializer.Type)
    {
        case "text/html":
            writer.String($"<pre><code class=\"language-{_language}\">{HtmlEscape(_source)}</code></pre>");
            break;

        case "text/plain":
        case "application/json":
        case "application/plang":
        case "application/plang+json":
        default:
            writer.String(_source);
            break;
    }
}
```

The language tag rides as a `Property` on the surrounding `Data`, not as part of the value rendering — the property surfaces uniformly across formats and the LLM can read it back via `%snippet!language%`. No subtype precision in the LLM scope (per the spine's cross-cutting decision); `code` is the type, `language` is metadata on the wrapper.

### `path` (retroactive)

Add the interface to `app.types.path.@this`. The existing `JsonConverter` (`PLang/app/types/path/this.JsonConverter.cs`) emits the portable `Relative` string for JSON. Under `IWireWritable` this becomes:

```csharp
public void WriteTo(IWriter writer, ISerializer serializer)
{
    // Portable form for all formats — path's wire shape is its Relative
    // string by construction (file vs http variants both serialize to a
    // single string the parse side resolves through scheme registry).
    var wire = Context != null ? TryGetRelative() : (Raw ?? Absolute);
    writer.String(wire);
}
```

The `JsonConverter` stays for now (STJ-pathway callers route through it directly), but Normalize-dispatched paths flow through `WriteTo`. When the dust settles the `JsonConverter` can shrink to "call WriteTo into a buffer."

## The parse-in side — `Resolve(string, context)`

The interface is **out-only**. Parsing back from the wire still goes through `static Resolve(string, context)` — the existing factory convention — for string-shaped representations. For format-specific binary representations (protobuf bytes for image, raw bytes for path), the format's reader handles the round-trip:

- JSON / plang: deserializer hits a `"value"` slot, gets a string. If the surrounding `Data.Type` is `"image"`, looks up the image type's `Resolve(string, context)`, which sees base64 and calls `Convert.FromBase64String` to reconstruct bytes.
- Protobuf: deserializer hits a bytes slot. Same `Type=image` lookup; `Resolve(byte[], context)` (a separate overload, by-bytes) constructs directly.
- Text: a string slot. `Resolve` tries to interpret — path-shaped string → image-backed-by-path; base64 → bytes; neither → fail.

The parse asymmetry is fine: emit is always 1-to-many (one type, four formats), parse is always many-to-one (any format, one type) with the type knowing which incoming shapes are valid. The existing `Conversion.TryConvertTo` dispatch grows a new branch (typed parse via `Resolve(byte[], context)` when input is binary and Type is set).

## What changes vs. what stays

**Stays:**
- `Data.Normalize`'s tree shape and the View-based filter discipline.
- `IWriter`'s primitive vocabulary (no new methods — every type routes through the existing slots).
- `ISerializer`'s mime identity and the registry at `app/channels/serializers/this.cs`.
- The plang serializer's outer-shape contract (`{name, type, value, properties, signature}`) — `IWireWritable` only affects how `value` is emitted.
- All existing `[JsonConverter]`-based custom emission (path's `JsonConverter`, signature's converter, etc.).

**Changes:**
- One new file: `PLang/app/data/IWireWritable.cs` (the marker interface).
- One new internal node type: `WireWritableNode` (in `PLang/app/data/this.Normalize.cs`, the placeholder for the writer to recognize).
- One added branch in `NormalizeValue` (`PLang/app/data/this.Normalize.cs:42`).
- One added branch in `app.channels.serializers.json.Writer.Value` (and any future writer's value-dispatch).
- One ctor arg added to `json.Writer` (the `ISerializer` ref, threaded through).

That's the entire dispatch surface. Three proving types adopt the interface (number, image, code), `path` adopts it retroactively, and the door is open for every future type to do the same without touching any channel.

## Edge cases

**Cycles.** `IWireWritable` values can't be part of a cycle the way property-bag domain objects can — they're leaf values by definition. The visited-set in Normalize still guards against the surrounding graph cycling; the leaf write itself is acyclic.

**Sensitive fields.** A type that holds secret content (private key, password) and implements `IWireWritable` has full control over what reaches the wire. The `Sensitive` filter doesn't apply (the value isn't decomposed). Documentation rule: don't implement `IWireWritable` for types that carry secret payloads — let them go through the reflection walk so `[Sensitive]` discipline applies.

**Nested `IWireWritable`.** A value might want to delegate sub-fragments back through the writer (e.g. a `Document` type whose body is a `code` snippet). `WriteTo` can recursively call into other `WriteTo`s as long as it owns the wrapping (`writer.BeginArray` / `EndArray`, etc.). The writer's `Value` dispatch handles the recursive case identically to top-level.

**Unknown mime.** If a type's `WriteTo` doesn't recognize `serializer.Type`, it should fall back to its most general representation (base64 for image, raw string for code). Never throw — an unknown format channel should always get *something*; format introduction shouldn't require touching every type.

**Round-trip.** A value emitted through `IWireWritable` must be reconstructible via `Resolve` from at least one format's emission (typically `application/plang`). The branch should add a `BuilderSanity`-style test that emit-then-resolve round-trips a constructed instance of every registered type.
