# Stage 11A — `IReader`: the type reads its own value off the wire (mirror of `IWriter`)

**Status:** ready to implement. Self-contained pickup doc for a fresh context.
**Branch:** `compare-redesign`. **Owner:** coder. **Target framework:** .NET 10 / C# 13 (this matters — see "the mechanism").
**Read first, in order:** this doc → `deserialize-flow-design.md` (the original flow design; **its "IReader can't be built" note is now SUPERSEDED — see below**) → `architect/stage-11-lazy-read-and-containers.md`. Skim `list-dict-raw-slot-model.md` (container raw-slot model already landed) and `handoff-value-model-2026-06-17.md` (the binary/kind work that landed after the design doc — re-ground against current code, Step 0).

---

## The goal

Deserialization is symmetric with serialization. We have **`IWriter`** — a format-agnostic surface the type *pushes* its value into (`writer.Long(42)`), with a JSON impl (`json.Writer`) and protobuf/CBOR as future siblings. Build the mirror: **`IReader`** — a format-agnostic surface the type *pulls* its value from (`reader.Long()`), so **the type reads its own value directly off the single decode pass**. No intermediate `JsonElement` DOM, no `GetRawText()` re-stringify, no "decode to a plain object then re-read it." Data reads `{@schema?, name, type}`, then hands the reader (positioned at `value`) to the type, and the type constructs itself.

```
bytes → format reader (tokenized ONCE) → IReader → type.Read(ref reader, kind, ctx) → item
                                                     (the type pulls its tokens; no DOM, no re-parse)
```

## The mechanism — why `IReader` IS buildable now (supersedes the old "impossible" note)

The earlier doc said a format-agnostic `IReader` is impossible because `Utf8JsonReader` is a `ref struct` and "can't cross an interface." That objection was about **storing** the reader in a class field (genuinely illegal) and **boxing** it to an interface (illegal). **We never store or box it.** Two facts dissolve the objection:

1. **The read is one *synchronous* pass.** The reader is a stack local for the duration of the parse and is threaded **by `ref`** into each type's read; nothing keeps it alive across calls. (A `ref struct` only can't cross an `await` — and parsing is sync; I/O is the separate lazy `Value()` door.)
2. **C# 13 `allows ref struct`** (we're on .NET 10). A `ref struct` may implement an interface, and a generic method may accept it without boxing via the anti-constraint `where TReader : IReader, allows ref struct`. So a type's read is generic over the reader, monomorphized per format at the call site — zero boxing, zero storage.

So the asymmetry with `IWriter` is only in *plumbing*, invisible to the type: the **writer is a normal class** the type holds a reference to and pushes into; the **reader is a stack-only ref struct** threaded by `ref` and pulled from. Both are `reader.Long()` / `writer.Long(42)` to the type.

## The shape

```csharp
// 1. The format-agnostic pull surface (mirror of IWriter). A ref struct so a
//    format impl can hold a Utf8JsonReader.
public interface IReader
{
    string Format { get; }                 // "json", "protobuf", … (mirror of IWriter.Format)

    // leaf pulls (mirror IWriter's leaf pushes)
    bool   Null();                          // true if the current token is null (consumes it)
    bool   Bool();   int Int();   long Long();   float Float();   double Double();
    string String(); decimal Decimal();  byte[] Bytes();
    System.DateTime DateTime(); System.DateTimeOffset DateTimeOffset();
    System.TimeSpan TimeSpan(); System.Guid Guid();

    // structure
    void BeginArray();  bool NextElement();  void EndArray();      // while(NextElement()) { recurse }
    void BeginObject(); bool NextName(out string name); void EndObject();

    // read-only extras IWriter doesn't need (writes are told the shape; reads must look):
    TokenKind Peek();                       // number | string | bool | null | array | object — branch on it
    byte[] RawValue();                      // capture the current value's encoded bytes WITHOUT decoding
                                            //   (the lazy/verbatim path — see below; NOT GetRawText)
    void Skip();                            // skip the current value entirely
}

// 2. The JSON impl — a ref struct holding the (ref struct) Utf8JsonReader. Legal:
//    a ref struct MAY contain a ref struct field (C# 11+). Never stored in a class.
public ref struct JsonReader : IReader
{
    private Utf8JsonReader _r;              // stack-only field of a stack-only struct — fine
    public string Format => "json";
    public long Long() => _r.GetInt64();    // pulls one token off the single pass
    // … etc, plus RawValue() via _r's byte span / a bounded skip-and-capture
}

// 3. Dispatch on the TYPE, generic over the reader. The registry stores NON-generic
//    reader objects (storable); the generic method is monomorphized per TReader.
public interface ITypeReader
{
    item.@this Read<TReader>(ref TReader reader, string? kind, ReadContext ctx)
        where TReader : IReader, allows ref struct;
}
// number.serializer.Reader : ITypeReader { Read<TReader>(...) => new number(reader.Long()); }
// registry: (typeName, kind) -> ITypeReader   (replaces today's Readers.Of(name,kind))
```

`Wire` then:

```
read @schema (optional, ignored)
read name
read type     → typeRef                      // type precedes value ⇒ typeRef known at `value`
value         → registry[typeRef].Read(ref jsonReader, typeRef.Kind, ctx)   // type pulls its own value
construct     → new Data(name, item)
```

- `number.Read` → `reader.Long()`. `text.Read` → `reader.String()`.
- `list.Read` → `reader.BeginArray(); while (reader.NextElement()) { /* recurse a Data envelope read on the SAME ref reader */ } reader.EndArray();` — **element parsing lives on `list`/`dict`, not in `Wire`** (interlocks with the raw-slot container model already landed).
- Polymorphic / no declared type: the `object`/`item` reader reads the natural value from `reader.Peek()` (number→number, string→text, array→list, object→dict).

## Lazy / verbatim passthrough — the one subtlety (keep it)

Verbatim passthrough (read→write-out of an *untouched* value = byte-identical) is a **requirement** (Stage 3 "the never-narrowed path"), not waste. Today it's done with `deferredRaw` + `GetRawText()` (a re-stringify). With `IReader` it becomes `reader.RawValue()` — capture the encoded bytes for the value slot **without** decoding into a value, born straight into an `item.source` (raw form + `{type,kind}` stamp), materialized lazily later via the type's content decode. The win: no `JsonDocument` DOM, no `GetRawText` round-trip — the reader hands the raw span. **Do not delete laziness; delete the re-stringify.**

Note the two distinct "reads" — don't conflate:
- **Wire structural read (`IReader`'s job):** pull the `.pr` value tokens — number/string/array/object — into the value (or its raw form).
- **Content-kind decode (stays on the type / `item.source`):** turn a *content* payload (a CSV string, a JSON string that is a file's content) into rows/a dict, lazily, on `Value()`. `IReader` gets the structural raw form; the kind-reader decodes the content. They compose (a `{table,csv}` value: `IReader` yields the CSV string → `source` → table content-decode on touch).

## Judge STAYS (Ingi's call)

Out of scope. Leave `type.Judge` and callers. Judge's kind/strict reconciliation runs on the single read value just as before — it's separable from this read-path rewrite, and removing it is a later, smaller step once the read is clean.

## Step 0 — RE-GROUND before editing (mandatory)

`deserialize-flow-design.md` predates the binary/kind + raw-slot-container landing (`handoff-value-model-2026-06-17.md`). Confirm against **current** code, in `PLang/app/data/Wire.cs`:
- the eager value path (`JsonDocument.ParseValue` → `item.serializer.json.Parse`) — this is the decode-then-read this design replaces with off-stream reads.
- which of `{deferredRaw, IsDeferrableShape, _readDepth, LiftDataIfShaped, LiftArrayElements}` still exist (some may be gone).
- the `@schema:data` nested-Data-in-value-slot path (signatures, snapshot section lists) — **real, keep it**; the design doc's "no nested Data" is false now.
Write a 5-line findings note (what's already off-stream, what's left) before changing anything.

## What this removes (after Step 0 confirms)

`Wire.ReadBody`'s value eager-decode (`JsonDocument` DOM + `json.Parse` re-read), the `deferredRaw`/`GetRawText` re-stringify, `IsDeferrableShape`, `LiftDataIfShaped`, `LiftArrayElements`, the `_readDepth` counter; `type.Deserialize`'s JsonElement-unwrap + try/catch + reader-registry `Shared` fallback. Element-parsing moves onto `list`/`dict`.

## Migration order

1. **`IReader` + `ReadContext`** (the surface above; `ReadContext` already exists — reuse).
2. **`ref struct JsonReader : IReader`** over `Utf8JsonReader`, including `RawValue()`/`Skip()`/`Peek()`.
3. **`ITypeReader` registry** — convert each existing reader (json/csv/text/goal/code, the scalar types) from `Read(object raw, kind, ctx)` to `Read<TReader>(ref TReader, kind, ctx)`. Scalars pull one token; containers stream.
4. **Rewire `Wire.ReadBody`** to read `{@schema?,name,type}` then dispatch `registry[typeRef].Read(ref reader, kind, ctx)`; lazy/verbatim → `reader.RawValue()` → `item.source`.
5. **Move container element-parsing** onto `list`/`dict` (raw-slot model already there).
6. **Delete** the dead machinery once unreferenced. Leave `Judge`.

## Caveats / invariants

- **Sync-only read pass** — no `await` between `BeginObject` and `EndObject` (ref struct can't cross await). Already true. Content-kind decode that needs I/O stays behind the lazy `Value()` door, post-read.
- **`type` precedes `value`** on the wire (writer invariant) — so `typeRef` is known at the value token. Keep enforcing on the writer; a hand-authored value-first `.pr` is the only thing that'd need buffering (reject or tolerate — decide, document).
- **`allows ref struct`** requires C# 13 / .NET 9+. Confirm `<LangVersion>`; if not defaulted, set it.
- **Verbatim passthrough** must survive (`reader.RawValue()` → `source`, no re-stringify).
- **`@schema:data` nested Data** in a value slot is real — keep that read path.

## Files

- NEW: `PLang/app/channel/serializer/IReader.cs` (mirror `IWriter.cs`), `…/json/Reader.cs` (the `ref struct JsonReader`).
- `PLang/app/data/Wire.cs` — `ReadBody` (the value loop), delete the DOM/deferredRaw/depth machinery; keep the writer side (passthrough invariant).
- `PLang/app/type/item/source.cs` — lazy form fed by `reader.RawValue()`.
- `PLang/app/type/list/this.cs`, `…/dict/this.cs` — element parsing via `IReader`.
- `PLang/app/type/this.cs` — `Build`/`Deserialize` simplify; `Judge` stays.
- The reader registry: `Context.App.Type.Readers` → returns `ITypeReader` now.
- Mirror reference: `PLang/app/channel/serializer/IWriter.cs` + `…/json/Writer.cs` (read the write side first; build the symmetric read).

## Verify

- `./dev.sh build` clean → per-suite sweep (see `CLAUDE.md` "Running plang Tests").
- Watch: `PLang.Tests/Wire/**`, `PLang.Tests/Data/App/LazyDeserialize/**`, `Stage3_*` verbatim-passthrough / never-narrowed, and the binary/kind decode tests (json→dict, csv→table, image, md→text, `.pr`→goal — listed in `handoff-value-model-2026-06-17.md`).
- Every change here is deep-shared → full sweep, diff against baseline, **zero new failures** is the bar.

## Baseline at handoff (HEAD on `compare-redesign`)

C# suite: **0 real failures.** Two HTTP tests (`Redirect_ToUnauthorizedHost`, `Post_405`) are **flaky under full-suite server contention** — pass isolated; ignore unless they fail isolated.
Skipped (do NOT expect to clear with this work): snapshot-redesign (3: `Snapshot_CapturesByReference`, `Snapshot_NullValuedVariable`, `ErrorsTrail`), archive-as-layer (8), pure-lazy source-gen (8). `ErrorsTrail` is a neighbor (snapshot serializes `IError` via Data-normalization as an empty `[Out]` bag); if the read rework happens to fix it, bonus, but it's filed under the snapshot redesign.

## Why this is right (the signal)

It's the exact symmetric counterpart of `IWriter` — the same minimal leaf+structure surface, the same per-format impl, the same type-owned dispatch. The write side already proved the shape; the read side just needed the ref-struct mechanics (sync pass, by-ref, `allows ref struct`) that the earlier note missed. Types stay format-agnostic; JSON is one impl; protobuf/CBOR ship later as siblings without touching a single type.

## Related model context

- `clr-plang-boundary.md` — CLR⇄plang boundary (Lift inbound, `.Clr` outbound) + the everything-plang-internally endpoint (Stage 10 — the architect is mapping that separately; the `a4dfcb602` "ask the value, don't reach past it" worklist is its start).
- `null_model_no_absent` (memory) — `absent` collapsed into the null citizen; value-less = `IsNull` (a failed/unmaterialized read surfaces as the null citizen).
- `data_value_model` (memory) + `data-value-model.md` — the type-instance-IS-the-value contract this serves.
