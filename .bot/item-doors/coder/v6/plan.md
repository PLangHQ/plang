# coder — item-doors — v6 — (c2) writing is a use (158): the trace

Nothing built. Rows 158/159 read.

## The paste today: three places a value asks who writes it

```
wire.Output          if (_reader.Owns(writer)) writer.Raw(slice)        wire/this.cs:64
IWriter.Content      kind.Owns(this) ? Raw(chars) : String(chars)       format/IWriter.cs:65
json.Writer.Bytes    kind.Owns(this) ? WriteRawValue(bytes) : …         kind/json/writer.cs:50
```
`Owns` has two overrides. json's is true for a json writer or a plang writer (kind/json/this.cs:24). plang's is true for
a plang writer or a json writer (wire/kind/plang/this.cs:126). So "owns" just means "the writer is json underneath".

Who calls Content: text.Output (text/this.cs:237, 246), source.Output (source.cs:376) and size.Output
(size/this.cs:57). Size's kinds have no Owns, so size always writes a String.
Who calls Bytes: source (source.cs:368), content (content/this.cs:146), image and binary. Only json content reaches the
own-kind arm.
Who calls Raw: wire:64, and formal's own `Variable` (formal/writer.cs:93).

## Each path: who reaches it, today, after, its test

| who reaches it | today | once the paste is gone | test (red first) |
|---|---|---|---|
| a .pr written back (goal Encode, Store, the plang writer, indented) | each lazy slot's slice is pasted as captured | the slot decodes with the Data's context and the value writes itself, indented like the rest | `ActionPropertyPrTests.WrittenPr_ReadsBackAndWritesAgain_ByteIdentical` stays green. New: a .pr holding a nested dict / list / `1.50` slot reads back and writes again byte-identical |
| Data.Digest (sign and verify; the plang writer, layer) | verify re-writes a received Data, and its lazy slots paste the sender's bytes | verify decodes and re-writes, so the digest matches only if read-then-write is lossless | New: sign → encode → decode → verify succeeds for a Data holding a nested dict with `1.50`, `"æ<&"`, `{"a":1,"A":2}` |
| an http json response / an http body (json kind Encode, the json writer) | a source or content of kind json pastes its raw: whitespace kept, never parsed | parsed (a use) and written as the json value: compact, the json kind's escaping | `PostItemOfKindJsonVerbatimTests` goes red (spaces): restate it as "sent as the json it is" |
| resting json saved to a file (file channel, the json writer) | `SavingAReadJsonFile_IsByteIdentical`: a copy, whitespace and all | written compact | that test goes red: restate it as "equal json" (OPEN 1) |
| resting json on the plang wire | `Cut1_*`: pasted, MaterializeCount 0 | parsed and written as a dict / list under `{item, json}`; reads back the same | Cut1 restated: "relays as equal json, reads back equal". Marker test stays (a dict's `@schema` key is a key) |
| a debug dump (item.cs:822, Debug view, the json writer) | lazy slots paste | they decode and write | covered by the .pr and Digest tests |
| a json text written to a terminal (text writer) | top level: its characters as-is. Nested in a dict: a quoted string of its characters | top level and nested: the json value (compact) | New: a json text inside a dict writes as an object, not a string (red today) |

## What replaces Content(kind): the open doors each value already has (nothing new)

```csharp
// text: its own Opened(context), the kind.Open door, opened once and kept
if (Opened(context) is { } opened)
{
    if (!opened.Success) throw new AppException(opened.Error!);   // "{x" declared json: the write's answer
    await writer.Kind.pair.Output(opened.Peek(), writer, mode, context); return;
}
writer.String(_value);                                            // md, plain text: characters are themselves

// source (chars or bytes): its own Parse(ctx) door, the "a use opens it" one (source.cs:279)
if (Parse(context) is { } value) { await writer.Kind.pair.Output(value, writer, mode, context); return; }
if (_value is byte[] b) writer.Bytes(b, Type.kind); else writer.String(chars);

// content: (_format ?? Type.kind).Parse(Bytes, context), then the same; else Bytes
// size: w.String(Text)
// json.Writer.Bytes: the own-kind arm goes. A text kind's bytes are written as their text, anything else as base64
```
What goes: wire:64, `IWriter.Raw` (formal keeps a private one for `Variable`), `IWriter.Content`, `kind.Owns` and both
overrides. These are removals from the locked surface, approved by 158; the re-pin cites 158.

## wire.template, once line 64 goes

Its Store branch calls `base.Output`, which is now `pair.Output(Decoded(context))`, the same as its other branch.
template's only override was Output, and nothing asks `is template`, so the class has nothing of its own left:
`type.Make` (type/this.cs:430) makes one wire holding the row's variables. Template-ness rides on `Type.Template` in
`Read`'s ReadContext either way. `WireTemplateOutputTests` (both tests) prove it.

## The round trip signatures rely on

- **Strings:** sign and verify use the same plang writer and the same escaping. `"æ"` reads back as æ and is
  written as `æ` again. Stable.
- **Numbers:**
  - A slot whose type carries its kind reads through that kind (decimal's `r.Decimal()`), so `1.50` survives.
  - An untyped number, json content or a dict element goes through `number.Parse`, which turns `1.50` into the
    double 1.5, written back as `1.5`. That's a digest mismatch, so SignatureInvalid. The red test above names which
    slots hit it.
  - Fix, if red: number.Parse keeps a decimal literal exact when it fits (a behaviour fix in number). It changes the
    kind a json `1.50` reads as, double → decimal (OPEN 3).
- **Nested dicts:**
  - Key order is kept.
  - The dict is case-insensitive, so `{"a":1,"A":2}` reads back as one key. That loses data and breaks the digest,
    where today the slice is pasted. Red test above.
- **json content that isn't one value:**
  - json Parse reads the first token and ignores the rest: `1, "evil": 2` reads as 1.
  - `Cut1_RawThatIsNotOneJsonValue_FailsTheEncode` goes red, because the encode now succeeds and writes `1`.
  - Fix: json Parse refuses trailing content. This is a latent bug for `%x.a%` today too.
- **Context:** a wire now needs its context even in Store. Today the Store paste worked without one. Every writer
  traced passes one (goal Encode, snapshot, sqlite, Data.Output's `??= _context`).

## OPEN (what a user sees)

1. Unread json written out loses its formatting, whether saved to a file, sent as an http body or written to a
   terminal:
   - it is written compact;
   - `< > & ' +` are escaped in json output;
   - a copy of a json file is no longer byte-identical.
2. .pr files change on their next rebuild: nested slots come out re-indented and canonical, which shows as a one-time
   diff across test/**/.build.
3. Only if the red proves it: a json `1.50` reads as a decimal.
4. Cost: an untouched json and every lazy .pr slot are parsed when written (the builder's save, a relay, a dump). This
   is what 158 means by "writing is a use".
