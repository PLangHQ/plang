# Data Structural Normalization

## What this is

PLang's Data carries any `object?` as its Value today. JSON (via STJ reflection) can encode anything, which makes the current setup "free" for JSON channels. But the moment a non-reflection format enters the picture (protobuf, MsgPack, CBOR, Avro), arbitrary `object?` becomes the wall — those formats can't introspect random C# types.

This branch lands the **structural normalization** that makes Data format-agnostic at the transport layer. The contract on `Data.Value` tightens, and a `Normalize()` step walks any C# object into a uniform tree once, at the serialization boundary. After that, every format encoder is a trivial walker — no reflection, no per-format converters for arbitrary content. PLang becomes truly transport-format-independent.

This branch builds on `data-serialize-cleanup` (assumes ISerializer takes Data, plang serializers are merged, signing is at the channel, Compress is flat).

## Why now — and what the trigger was

The follow-up conversation that led here started with: "how would the serializer know how to serialize a complex object — JSON is super flexible because of reflection, but what about protobuf?" The honest answer was "JSON's reflection covers us today, protobuf would force a decision later." The cleaner answer is to make Data carry its own representation so the question never binds on the format library.

## The Value contract

Today: `object?` — anything.

After this branch: one of
- **primitive** — `string`, `int`, `long`, `double`, `bool`, `DateTime`, `decimal`, `null`
- **`byte[]`** — opaque binary leaf
- **`Data`** — nested record (used when a name or signature needs a home)
- **`List<>`** — elements are any of the above, mixed

Anything else — User objects, Dictionaries, custom records — gets normalized into one of these. Normalization is reflection in PLang's hands (one-time, at the boundary) rather than in every format library's hands (every time).

## Normalize — the operation

`Data.Normalize()` walks Value:

```csharp
private static object? NormalizeValue(object? v) {
    if (v is null or string or bool or DateTime or decimal or byte[]) return v;
    if (v is int or long or double or float) return v;
    if (v is @this data) { data.Normalize(); return data; }

    // Homogeneous primitive list — stays as List<primitive>; type becomes "list<elementtype>"
    if (v is IEnumerable enumerable && IsHomogeneousPrimitive(enumerable, out var elemType)) {
        return ToList(enumerable);                // bare primitives, no Data wrapping
    }

    // Heterogeneous or Data-containing list — each element normalized, wrapped if needed
    if (v is IEnumerable mixed) {
        var list = new List<@this>();
        foreach (var item in mixed)
            list.Add(item is @this d ? d : new @this("", NormalizeValue(item)));
        return list;
    }

    // Named-member object — reflect once, decompose into List<Data> with names
    if (IsNamedStructure(v)) {
        var members = new List<@this>();
        foreach (var prop in v.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (prop.IsDefined(typeof(WireIgnoreAttribute), false)) continue;
            members.Add(new @this(prop.Name.ToLowerInvariant(), NormalizeValue(prop.GetValue(v))));
        }
        return members;
    }

    // Dict — same shape as named-member object, keys become names
    if (v is IDictionary dict) {
        var members = new List<@this>();
        foreach (DictionaryEntry e in dict)
            members.Add(new @this(e.Key?.ToString() ?? "", NormalizeValue(e.Value)));
        return members;
    }

    throw new InvalidOperationException($"Cannot normalize type {v.GetType()}");
}
```

Reflection fires here, once. Format encoders never reflect.

## Wire shape — bare-when-possible

A Data record is `{name, type, value, signature}`. The rule for wrapping:

- **Bare value** — when the parent's `type` covers what the value is. Primitives inside a list whose parent type is `"list<int>"` ride bare.
- **Data wrapper** — when a name or signature needs a home. Named members of an object; signed sub-records; elements of heterogeneous lists where per-element type matters.

Examples:

```json
// Primitive
{ "name": "answer", "type": "int", "value": 42, "signature": {...} }

// List of primitives — bare elements
{ "name": "counts", "type": "list<int>", "value": [1,2,3], "signature": {...} }

// Object — named members wrapped
{
  "name": "user", "type": "user",
  "value": [
    { "name": "firstName", "type": "string", "value": "Ingi" },
    { "name": "lastName",  "type": "string", "value": "Gauti" }
  ],
  "signature": {...}
}

// Dict — same structural shape as object, just a different type label
{
  "name": "headers", "type": "dict",
  "value": [
    { "name": "content-type", "type": "string", "value": "application/json" },
    { "name": "accept-language", "type": "string", "value": "en-US" }
  ]
}
```

Every wrapped Data carries all four fields (when populated). Bare primitives ride in the parent's value slot when the parent's type describes them.

## Format encoders — trivial walkers

After normalization, the encoder is a five-case dispatch:

```csharp
void EncodeValue(object? v, IWriter w) => v switch {
    null            => w.Null(),
    string s        => w.String(s),
    int i           => w.Int(i),
    long l          => w.Long(l),
    double d        => w.Double(d),
    bool b          => w.Bool(b),
    DateTime dt     => w.DateTime(dt),
    byte[] bytes    => w.Bytes(bytes),
    Data nested     => EncodeRecord(nested, w),
    IList list      => w.Array(list, item => EncodeValue(item, w)),
    _ => throw // unreachable after Normalize
};
```

`IWriter` is the format protocol. Implementations: `JsonWriter` (over `Utf8JsonWriter`), `ProtobufWriter` (over `IBufferWriter<byte>` with field-number machinery), `MsgPackWriter`, etc. Each implements the same primitive emit methods plus structural primitives.

JSON: emit native primitives, arrays, objects (or our Data-record shape). Protobuf: typed `oneof` over primitives + `DataList` for nested. Same logical shape.

## As&lt;T&gt; — the reverse direction

`data.As<T>()` becomes a tree-walker. Today it delegates to STJ reflection. After this branch, it walks the normalized tree and builds T by matching member names to T's properties.

For `As<Dictionary<string,X>>()`: walk `List<Data>` value, map name → key, value → As&lt;X&gt;.

For `As<User>()`: walk `List<Data>` value, for each member find the matching User property and assign.

For `As<List<int>>()`: walk `List<int>` value (already primitives), return directly.

The reflection happens once per type in As&lt;T&gt;'s property-lookup cache, mirroring how Normalize works on the way out.

## Cross-cutting decisions still to settle

- **Eager vs lazy normalize.** Eager: normalize at construction-time, so `data.Value` always has the narrowed contract. Lazy: normalize at serialize-time only, `data.Value` keeps `object?` in memory. Lazy is less invasive but means in-memory inspection of `data.Value` has two possible shapes. Recommendation: **lazy first** — keeps the migration tractable.
- **Cycle detection.** The Normalize walker needs to track visited objects to avoid infinite loops on circular references. STJ has this already; ours needs to. Bounded depth check + visited-set.
- **Custom serialization.** Types that want to control their wire shape implement `IDataNormalizable` (or similar): `object? ToWireValue()`. DateTime → ISO string; decimal → string; user-defined oddities → user's choice. Without the interface, default reflection.
- **`[WireIgnore]` / `[WireInclude]` attributes.** Mirror today's `[JsonIgnore]` / `[Out]`. Probably reuse `[Out]` discipline if the semantics align.

## Stages

To be carved when this branch starts in earnest. Rough outline:

1. **Value contract narrowed + Normalize implemented.** Add the method on Data; wire it into the serializer entry point. Migration plan for `data.Value as SomeType` call sites.
2. **`IWriter` protocol + JsonWriter implementation.** First format adapter; should produce byte-identical output to the current STJ path so all existing tests pass.
3. **As&lt;T&gt; rewritten as tree-walker.** Replaces the STJ-deserialize path. Property-lookup cache.
4. **Cycle detection, custom serialization extension, `[WireIgnore]` discipline.** The polish.
5. **Second format proof — protobuf adapter (or MsgPack).** Demonstrates the architecture works for non-reflection encoding. Probably stays behind a feature flag until tested.

## Out of scope

- Choosing a specific protobuf library or MsgPack library.
- Stream-mode encoding (chunked writes for huge payloads). The current `byte[]`-then-write pattern stays.
- Cross-language schema sharing (PLang doesn't generate .proto files for other consumers — that's a future "schema export" concern, separate from how PLang itself encodes).

## Dependencies

`data-serialize-cleanup` must be merged first. This branch assumes ISerializer takes Data, plang serializers are merged into one, signing lives at the channel, Compress is flat.
