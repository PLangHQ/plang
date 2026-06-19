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

## B2 progress — scalar leaves done (number/text added to bool/guid/duration)
- **number** — typed reader is the inverse of `Default.Write`'s `NumberKind` switch:
  pull the matching token, `From(...)` at that exact precision (Int128/UInt128/
  BigInteger and overflowing ULong ride as a string). No `Convert`, no `ChangeType`.
- **guid/duration** — born directly from `reader.Guid()`/`reader.TimeSpan()` (Ingi's
  point: the reader hands the typed token; `Convert` was a needless re-parse). Drops
  the context dependency too.
- **text** — borns `text{canTemplate, Kind}` from the string token, same shape as
  `Default.Read`. The authored-vs-literal `Template` stamp is NOT set here — that
  rides the reader's mode under the separate **template-stamping-at-read** epic
  (`.bot/compare-redesign/coder/template-stamping-at-read.md`); until it lands the
  post-parse seam stamps it, exactly as for the eager path. Mutation-proven live
  (Cut1 "hello" round-trips through it); full suite green.
- Tests are isolated reader units (`ReadScalar` drives a reader over a bare token,
  no Wire/signing) + a bool e2e through Wire. The full-pipeline round-trip with a
  raw-`int` + signing + `Clr<T>` was a flaky harness (fails on the old path too) —
  the isolated form is the correct unit shape.

## B2 containers — list/dict stream off the pass
- **list/dict** typed readers stream each slot via `item.serializer.json.ReadSlot`
  (a generic streaming sibling of `RawSlot`): a scalar streams with no DOM; a
  nested container / `@schema:data` element narrows through the proven parser via
  `reader.RawValue()` (kept generic — no `Inner` reach). The element/entry walk
  lives on the container, not Wire (stage-11 Part A intent). Raw-slot backing
  (Part B) already there, so this just fills it off the reader.
- Added `IReader.Number()` — natural long-or-double for a raw slot / polymorphic
  read where no declared kind picks the precision.
- Mutation-proven live in the Wire path (list-bearing tests fail when the reader
  throws); full suite green, counts identical to baseline.

## Done so far (READ off the wire is fully on the IReader path)
Scalars (bool/guid/duration/number/text) + containers (list/dict) all read their
own value off the single decode pass. The JsonElement-DOM double-read is gone for
every common .pr value. Eight typed readers, all mutation-proven live where
reachable, full suite green at every commit.

## Polymorphic reader — built, proven NOT exercised, reverted
Built `object`/`item` polymorphic readers + a `ReadNatural` streaming helper and
routed Wire's no-type/`Polymorphic` case through them. Full suite stayed green, but
a **mutation test proved the path is never exercised** (Data's 937 tests all pass
with `ReadNatural` throwing) — typed values dominate the wire; a bare polymorphic
value rarely/never rides it. Worse, the streaming path *diverges* from the proven
eager `Parse` path on two points: a `%ref%` string borns `text` instead of a raw
string (belongs to template-stamping-at-read), and the Data's declared type derives
to the natural type instead of preserving `{object}`. An unexercised path that also
diverges is a latent bug, not a feature — **reverted**. The eager `Parse` path keeps
polymorphic values correct. Revisit only alongside template-stamping-at-read.

## FINAL DECISION (with Ingi) — two read modes, the IReader work is complete
We will **not** delete the `object raw` registry. Implementing the content path
revealed it is the *wrong* thing to unify: content decode is a **whole-payload**
operation (csv→table parses the whole string, image→base64 wraps the whole bytes,
json-string→dict parses the whole payload) — the raw is already materialised, there
is no token stream to pull. Forcing it through `IReader` means wrapping the raw in
a degenerate reader whose `String()`/`Bytes()` hand the same raw straight back —
indirection that returns its own input, with no DOM avoided and no streaming.

So the honest end state is **two read modes, both load-bearing**:
- **token-stream pull** (`IReader` / `ITypeReader`, `Readers.Typed`) — the `.pr`
  wire structural read, where streaming off bytes kills the JsonElement DOM. DONE
  for every common value (scalars + containers).
- **whole-payload content decode** (`Read(object raw)`, `Readers.Of`) — `file.read`'s
  path: the channel stamps `{type,kind}` from mime → `item.source` → `source.Value`
  → the type's `Read(object raw)` (csv→table, json→dict, bytes→image). The raw is
  in hand; the type decodes it whole. This is the *right* shape, not transitional debt.

`file.read` materialises through `source.Value` (mode 2) and already works — it never
needed to be an `IReader`. **The IReader read-path project is complete.** The
polymorphic reader was reverted (nothing feeds it). Nothing further to delete:
the deferred-capture machinery in Wire feeds mode 2 (content), so it stays.

## (obsolete — superseded by the FINAL DECISION above) the deletion phase
Deleting the `object raw` registry (`Readers.Of`) requires, in order:
1. **Two new `IReader` impls** the content path needs:
   - a **content reader** over a held `string`/`byte[]` (degenerate — `String()`/
     `Bytes()`/`RawValue()` hand the raw back) for `source.Value`'s content decode;
   - a **CLR-value reader** over an already-decoded `object` (dict/list/scalar) for
     `type.Deserialize` (its `raw` is post-decode, not bytes) — iterates a held
     dict/list and parses held scalars through the pull surface.
2. **Convert the content readers to `ITypeReader`** — `object/json`, `table/csv`,
   `image`, `code` (their existing `Read(object raw)` decode, now pulling raw via
   `reader.String()`/`Bytes()`).
3. **Rewire the 4 consumers** off `Readers.Of`/`TypeOf`: `source.Value` (73/79),
   `type.Deserialize` (410), `json/converter.cs` path field (77), `kind.TypeOf` (34).
4. **Delete** `Readers.Of`, the object-raw delegate + its discovery, and the dead
   Wire machinery (`deferredRaw`/`GetRawText`, `IsDeferrableShape`, `_readDepth`) —
   the deferred capture moves to `reader.RawValue()` → `source`.

Why not now: it builds two new reader kinds, converts 4+ content readers, rewires 4
call sites, then does the **irreversible** registry removal — larger than the entire
read-path work above, and the read path is the load-bearing deserialize spine. It
deserves its own session with the plang suite ideally back up as a net. Everything
above this line is proven green and the old paths stand untouched behind the new
ones, so this is a safe stopping point.

## (superseded) earlier next-steps list
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
