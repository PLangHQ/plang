# Data Structural Normalization

> **Note for downstream bots (coder, test-designer):** Every code snippet, type signature, method name, file path, and test case shown in this plan and its stage / topic files is a **suggestion** that captures architect intent — not a contract. You own your code and tests. Reshape, rename, restructure, or replace anything below as the real constraints of implementation demand. Push back if the design itself looks wrong; the architect would rather hear it than have you contort the code.

## Why

The trigger from the design conversation: "if Data is opaque, how does a non-reflection format like protobuf encode it?" JSON gets a free pass because STJ has reflection — hand it any C# object, it walks the type metadata. Protobuf doesn't. MsgPack doesn't. CBOR doesn't. Any format that needs schema or registered types breaks the current model.

The deeper reason to make this change: PLang is a language for building things, not a JSON-encoding ceremony. As long as `Data.Value` is `object?`, PLang's transport story is tied to whatever format can reflect — which is JSON. Users who want efficiency (protobuf), schema validation (Avro), small wire size (MsgPack/CBOR), or strict typing don't have a path. Structural normalization decouples PLang's data model from any single format's encoding rules. Once the tree is uniform, every format is a trivial walker, and PLang stops dictating wire format at the language level.

This is also the cleaner OBP shape. Today Data's representation is "discovered" via reflection at every format library's discretion. After this change, Data owns its representation; format libraries are encoders, not introspectors. Reflection — the OBP-violation of "knowing without asking" — fires exactly once in Data.Normalize, never in format code.

*Ingi's deeper motivation — whether the trigger is a specific deployment scenario (protobuf for performance, MsgPack for IoT, etc.), a language-design aspiration, or something else — would tighten this further. The above is what surfaced in the design conversation; if there's a concrete user story or constraint behind it, it goes here.*

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

## Cross-cutting decisions (settled)

- **Normalize is always lazy.** Runs at serialize-time only. `data.Value` keeps `object?` in memory; the narrowed contract applies to the *wire* tree, not the in-memory tree. Less invasive (existing `data.Value as SomeType` call sites keep working), and we don't pay normalization cost for values that are never serialized.
- **Cycle detection — bounded.** Normalize tracks visited objects (visited-set) and enforces a max-depth cap. Hitting either is a hard error at serialize-time, not silent truncation. STJ has the equivalent; ours needs to match.
- **`[Out]` is the wire whitelist.** Only properties tagged `[Out]` go on the wire. Properties without `[Out]` are excluded. This is a redefinition of today's `[Out]`, which only forces a `[JsonIgnore]`'d property back in — Stage 2 introduces a new wire-view filter that makes `[Out]` the positive gate. `[Sensitive]` still excludes (even when `[Out]` is present). `[JsonIgnore]` becomes irrelevant for the wire path.
- **`[Masked]` — new attribute for observable-but-redacted properties.** Tag a property `[Out, Masked]` and its name travels on the wire but its value is replaced with `"****"`. Canonical use: `setting.value` — receivers see that `DATABASE_URL` is configured (useful for diagnostics) but never see the secret. Distinct from `[Sensitive]` (which excludes entirely). Honored in both Out and Debug views — debug never unmasks. Joins the `View.cs` attribute cluster in Stage 1.
- **Debug mode bypasses the `[Out]` filter.** When the serializer runs in debug mode, every property goes on the wire — no per-property `[Debug]` tag needed. The only filters that persist in debug: `[Sensitive]` (always excluded) and `[Masked]` (value still `"****"`). The existing `View.Debug` enum + `[Debug]` attribute in `PLang/app/View.cs` stay for the LLM-builder and other views; the wire serializer just doesn't consult them.
- **No custom-serialization escape hatch.** Every type serializes as its reflected (`[Out]`-filtered) property bag — no `IDataNormalizable` interface, no opt-out of reflection. Types like `path` whose properties are derived from a canonical form still ride on the wire as the full filtered bag (`{ Scheme, Relative }` for path). The receiver re-derives everything via per-type `As<T>` (e.g. `path.Resolve(Relative, context)`). No type in the inventory genuinely needs to collapse to a non-object wire form.
- **`RawSignature` is deleted.** Legacy from when `Signature.get` had a lazy-populate side effect (stage 2a.7 removed that). The four callers (signing.verify Ed25519, actor/permission, plang serializer × 2) migrate to `Signature` directly. Folded into Stage 1 since we're touching Data anyway.

The full per-type `[Out]` proposal is in [`plan/wire-out-attributes.md`](plan/wire-out-attributes.md) — 13 in-scope domain types with property-level decisions and rationale.

## Stages

Each stage carved as `stage-N-<slug>.md` at the architect root. Linear dependency chain — each builds on the previous.

| Stage | File | Status | Goal |
|-------|------|--------|------|
| 1 | [stage-1-out-discipline.md](stage-1-out-discipline.md) | pending | Apply `[Out]` per the wire-out-attributes inventory. Add `[Masked]`. Delete `RawSignature`. Surface-only prep — no Normalize yet. |
| 2 | [stage-2-normalize-jsonwriter.md](stage-2-normalize-jsonwriter.md) | pending | Add the wire-view filter ([Out] as whitelist). Add `Data.Normalize()` (lazy, bounded). Introduce `IWriter` protocol. Ship `JsonWriter` as the first concrete adapter. Replace path's existing JsonConverter. Wire the debug-mode bypass. |
| 3 | [stage-3-as-tree-walker.md](stage-3-as-tree-walker.md) | pending | Rewrite `As<T>` to walk the normalized tree instead of delegating to STJ reflection. Property-lookup cache. Per-type round-trip via `path.Resolve`-style hooks. |

**Second-format proof (protobuf / MsgPack) is deferred.** The `IWriter` abstraction is designed so a non-reflection format slots in without touching Normalize or any domain type — but actually shipping a second format isn't part of this branch. The proof comes when there's a concrete demand; for now the architecture is shaped to accept it.

## Test handoff

Test material for test-designer lives in `plan/`:

- [`plan/test-strategy.md`](plan/test-strategy.md) — narrative: scope, layer mapping (C# / goal / integration), integration cuts.
- [`plan/test-coverage.md`](plan/test-coverage.md) — heavy reference: coverage matrix, failure matrix, new surfaces inventory.

## Out of scope

- Choosing a specific protobuf library or MsgPack library.
- Stream-mode encoding (chunked writes for huge payloads). The current `byte[]`-then-write pattern stays.
- Cross-language schema sharing (PLang doesn't generate .proto files for other consumers — that's a future "schema export" concern, separate from how PLang itself encodes).

## Dependencies

`data-serialize-cleanup` must be merged first. This branch assumes ISerializer takes Data, plang serializers are merged into one, signing lives at the channel, Compress is flat.
