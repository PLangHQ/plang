# v1 — `IReader`: the type reads its own value off the wire

Branch `ireader-read-path`. Implements the read-path half of stage-11 / stage-11A,
with the **unified-registry** correction settled with Ingi (see below).

## Source docs
- `.bot/compare-redesign/coder/stage-11a-read-path-cleanup.md` — the pickup doc.
- `.bot/compare-redesign/architect/stage-11-lazy-read-and-containers.md` — Part A/B
  (Part B container raw-slot backing already landed; this is Part A + element parse on containers).
- This supersedes the architect's "do not build IReader" — Ingi confirmed (the
  `allows ref struct` mechanism the original note missed).

## The correction (Ingi, this session)
There are **not** two read axes. There is **one `IReader` abstraction** with N
format impls (`JsonReader`, `CsvReader`, `BinaryReader`, protobuf later) and
**one `ITypeReader` registry keyed by type**. The impl is chosen by **format/kind
at the read site**, the type is format-agnostic.

- `.pr` structural read → `JsonReader` over the `.pr` bytes.
- content-on-touch (`source.Value`) → construct the **kind-matched** `IReader`
  over the raw payload (csv→`CsvReader`, json→`JsonReader`, bytes→`BinaryReader`)
  and call the **same** `ITypeReader.Read`.
- `object raw` registry is **deleted** — replaced by `readers.For(format/kind)`
  (the read-side mirror of the channel writer-by-mime lookup).

## The mechanism
- `IReader` is a `ref struct`-compatible interface (leaf pulls + structure +
  `Peek`/`RawValue`/`Skip`).
- `ref struct JsonReader : IReader` wraps `Utf8JsonReader` (stack-only, by-ref,
  never stored/boxed). `RawValue()` captures a whole subtree verbatim by
  tracking `TokenStartIndex`→end and slicing the source span the reader holds.
- `ITypeReader.Read<TReader>(ref TReader reader, string? kind, ReadContext ctx)
  where TReader : IReader, allows ref struct` — monomorphized per format, zero box.

## Flow (the picture, confirmed with Ingi)
```
bytes ─► format reader (IReader impl, tokenized once) ─► ITypeReader.Read(ref reader, kind, ctx) ─► item
            JsonReader / CsvReader / BinaryReader            (type pulls tokens; never knows the impl)
```
Wire reads `{@schema?,name,type}`, then `registry[typeRef].Read(ref jsonReader,…)`.
Deferrable kinds capture `reader.RawValue()` → `item.source`, decoded later through
the kind-matched IReader.

## Migration order (revised — selector first)
1. `IReader` interface + reuse `ReadContext`.
2. `ref struct JsonReader : IReader` (incl. `RawValue`/`Skip`/`Peek`).
3. `readers.For(format/kind)` selector — `IReader` impl by kind (csv/json/bytes).
   The read-side mirror of writer-by-mime. (NEW step, ahead of per-type.)
4. `ITypeReader` registry keyed by type; convert the **structural** readers
   (number/text/bool/date-family/guid/duration; list/dict streamers; object/item
   polymorphic) to `Read<TReader>(ref…)`. Content readers (csv→table, image,
   json-string→object, code, goal) also become `ITypeReader` but are reached via
   their kind-matched IReader from `source.Value`, not off the JsonReader.
5. Rewire `Wire.ReadBody`: read envelope, dispatch `registry[typeRef].Read`;
   deferrable → `reader.RawValue()` → `source`.
6. Move container element-parse onto `list`/`dict` (raw-slot backing already there).
7. Rewire `source.Value` to `readers.For(kind)` + `ITypeReader.Read`; delete the
   `Read(object raw)` registry (`app.type.reader.@this`) once unreferenced.
8. Delete dead Wire machinery (deferredRaw/GetRawText re-stringify, IsDeferrableShape,
   _readDepth, the three-branch value switch). Keep Judge, keep @schema:data nested
   Data, keep signature-layer read.

## Invariants (must survive)
- Verbatim passthrough (`RawValue` → source, no re-stringify).
- `@schema:data` nested-Data in a value slot (signatures, snapshot section lists).
- Signature-layer auto-verify-on-read.
- `type` precedes `value` on the wire — reject value-first loud.
- Sync-only read pass (no await between Begin/End — ref struct can't cross await).
- Judge stays (Ingi's call).

## Verify
- `./dev.sh full` clean; per-suite sweep; **zero new failures** vs baseline.
- Watch: `Wire/**`, `Data/App/LazyDeserialize/**`, Stage3 verbatim/never-narrowed,
  binary/kind decode (json→dict, csv→table, image, md→text, .pr→goal).

## Open risks I'll surface if hit
- `RawValue()` subtree capture across a multi-segment `ReadOnlySequence<byte>`
  buffer (Utf8JsonReader can be built over a sequence) — single-span is easy,
  sequence needs care.
- Whether every existing content reader's decode maps cleanly onto an IReader
  front-end (CSV→array/object surface; image bytes→`Bytes()`). If one doesn't, stop and ask.
