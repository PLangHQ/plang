# IReader read path — summary

**Version:** v1 (increment B1 landed; B2+ pending)

## What this is
Deserialization made symmetric with serialization. Serialization already has
`IWriter` — a format-agnostic surface a type *pushes* its value into. This builds
the mirror, **`IReader`** — a format-agnostic surface a type *pulls* its value from
— so the type reads its own value directly off the single decode pass: no
intermediate `JsonElement` DOM, no `GetRawText()` re-stringify.

Settled with Ingi: **one** `IReader` abstraction with N format impls
(`json.Reader`, future `csv`/`binary`/protobuf), **one** `ITypeReader` registry
keyed by type; the front-end impl is chosen by format/kind at the read site, the
type stays format-agnostic. The old `object raw` reader registry is being replaced
(not yet deleted — coexists during the migration).

Chosen approach **B** (incremental): `IReader` over the `ref Utf8JsonReader`
inside the existing STJ converter, rewiring the eager value read. Approach **A**
(own the input buffer for true zero-copy `RawValue`) is a deliberate follow-up —
see plan.md.

## What was done (B1 — the mechanism, proven on three scalars)
The hard part is the ref-struct dispatch; B1 proves it end-to-end on
semantically-trivial scalars (bool/guid/duration — no kind ambiguity, no `%ref%`),
leaving the rich types (number kind-switch, text `%ref%` carve-out, containers,
polymorphic) for B2.

- **`PLang/app/channel/serializer/IReader.cs`** — the pull surface + `TokenKind`.
  Leaf pulls (`Long`/`String`/`Bytes`/…), structure (`BeginArray`/`NextElement`,
  `BeginObject`/`NextName`), `Peek`/`RawValue`/`Skip`. Cursor contract: every read
  leaves the cursor on the value's *last* token; drivers advance one to the next.
- **`PLang/app/channel/serializer/json/reader.cs`** — `ref struct Reader : IReader`
  holding `Utf8JsonReader` **by value** (a *ref field* to a ref struct is illegal —
  CS9050). Threaded by ref so the single embedded cursor advances in place; the STJ
  bridge copies in (ctor) and copies the advanced state back out (`Inner`).
  `RawValue` re-encodes for now (B; A makes it a buffer slice).
- **`PLang/app/type/reader/ITypeReader.cs`** — `Read<TReader>(ref TReader, kind, ctx)
  where TReader : IReader, allows ref struct`. The anti-constraint lets the
  stack-only ref struct cross unboxed, monomorphized per format at the call site.
- **`PLang/app/type/reader/this.cs`** — added the `_*Typed` dicts, `Typed(type,kind)`
  lookup (same precedence as `Of`), and ITypeReader discovery (instance classes,
  alongside the existing static `Read` scan). Both registries coexist for now.
- **`bool|guid|duration/serializer/Reader.cs`** — the three typed readers (mirror
  their `Default.Read` semantics: bool direct, guid/duration via their `Convert`).
- **`PLang/app/data/Wire.cs`** — `ReadBody` value case: when the declared type is
  context-bearing, non-polymorphic, non-deferrable AND has a typed reader, the
  type reads its own value off a `json.Reader` (no DOM, no `Build`); else the
  existing eager path is untouched. New `born` local short-circuits at `EndObject`.

## Verified
- `./dev.sh full` — all 6 C# projects green, counts identical to baseline except
  Wire 495→**498** (3 new round-trip tests). Zero regressions.
- **Mutation-proven live:** with bool's `Read` throwing, the bool wire round-trip
  fails with the probe (path is reached); reverted → green. guid/duration round-trip
  correctly through their typed readers.
- Pre-existing `plang --test` abort (born-typed `variable.set`) is unrelated and
  unchanged.

## Code example (the pattern every typed reader follows)
```csharp
public sealed class Reader : ITypeReader
{
    public string Kind => reader.@this.AnyKind;     // "*"
    public item.@this Read<TReader>(ref TReader reader, string? kind, ReadContext ctx)
        where TReader : IReader, allows ref struct
        => reader.Null() ? new @null.@this("bool", kind)
                         : new @bool.@this(reader.Bool());   // pulls one token off the pass
}
```

## What's next (B2+)
1. **`number`** — typed reader keyed off `kind` (mirror `Default.Write`'s NumberKind
   switch in reverse: Int/Long→`Long()`, Float→`Float()`, Decimal→`Decimal()`,
   Int128/UInt128/BigInteger→`String()`), then `number.Convert(raw, kind, ctx)`.
2. **`text`** — preserve the `%ref%` raw-string carve-out (an unresolved reference
   must NOT become a templated `text` instance — see `item.serializer.json.TextLeaf`).
3. **Containers** — `list`/`dict` stream elements off the reader (Part B raw-slot
   backing already landed); element parse moves off Wire onto the containers.
4. **Polymorphic/no-type** — `object`/`item` reader branches on `Peek()`.
5. **`readers.For(format/kind)` selector** + rewire `source.Value` to construct the
   kind-matched `IReader` and call the same `ITypeReader`; then **delete** the
   `object raw` registry and its 4 consumers (type.Deserialize, source.Value,
   json/converter.cs path field, kind.TypeOf), plus the dead Wire machinery
   (`deferredRaw`/`GetRawText`, `IsDeferrableShape`, `_readDepth`). Keep `Judge`,
   `@schema:data` nested Data, the signature-layer read.
6. **Approach A** (own the buffer → true `RawValue` slice) once the plang suite is
   back up as a net.
```
