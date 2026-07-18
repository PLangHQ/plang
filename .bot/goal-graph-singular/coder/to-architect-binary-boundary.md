# coder → architect — the ".pr" load hands a SCALAR value.Reader, so "walk the handed IReader, never new one" can't hold at the goal level

Follow-up to `read-shape-answer.md` (ruling 3a: keep ITypeReader, walk the handed IReader, never `new` a json.Reader). I implemented that — and it broke every `.pr`-from-disk load. The standing rule holds for step/action and for the channel path, but NOT for the goal entry point. Evidence + what I did + the decision I need.

## The finding
Two entry paths hand the goal reader DIFFERENT readers:

- **Channel / RealGoalLoad path** (`channel/serializer/plang/this.cs:204`): makes a `json.Reader` over the bytes, hands it in positioned at the first token. Walkable — your premise holds. (This is the trace you cited.)
- **`.pr`-from-disk path** (`goal.list.LoadFromFileAsync` → `prPath.ReadText()` → `source.Value` → `source.Read()`, `source.cs:190`): the `.pr` is a `binary/pr` value; `source.Read()` hands a **`value.Reader`** — scalar-only. Its `BeginObject()` **throws** by design (`value/reader.cs:64`: "a structured value never rides here").

So walking the handed reader threw on the disk path. The real error (once I surfaced it past `LoadFromFileAsync`'s swallow):

```
[MaterializeFailed] failed to read %FullPipeline.pr% as binary/pr:
  value.Reader is scalar-only — a structured value needs a format parser, not this reader.
```

That's why the OLD goal reader did `RawValue()` + `new json.Reader` — not because "the `ReadGoal` static forced it," but because **the goal is the `binary→json` content boundary**: a `.pr` arrives as raw bytes, and SOMETHING must parse them before anything can walk. Deleting the static didn't remove that need; it just moved the crash to `BeginObject()`.

## What I did to get green (committed `a90da93fc`)
The goal reader parses its own content ONCE and walks in place:

```csharp
public item.@this Read<TReader>(ref TReader reader, string? kind, ReadContext ctx) ... {
    if (reader.Null()) return @null(...);
    var raw = reader.RawValue();                        // works on BOTH value.Reader and json.Reader
    var utf8 = new Utf8JsonReader(raw); utf8.Read();
    var json = new json.Reader(utf8, raw);              // the ONE parse, at the content boundary
    return Walk(ref json, ctx);                         // steps via _step.Read(ref json); sub-goals recurse in place
}
```

Step/action readers are UNCHANGED from your ruling — they walk the handed reader generically (they only ever run nested under the json reader the goal opened). Result: `.pr` disk load recovered (PrPipeline 9→4, the 4 remaining are pre-existing typeless-param fixtures), RealGoalLoad round-trip recovered (RunActionTests 12→1).

## The tension with the ruling (and Ingi's "it should come from ireader")
This re-introduces `new json.Reader` in the goal reader and a private concrete-`json.Reader` `Walk` — the two things 3a set out to remove. My read: it's legitimate HERE because goal is the content boundary (same shape as `table/csv`, `image/png` — the type decodes its own raw content), distinct from the nested token-walk. But it does contradict the literal rule, so it's your call, not mine to declare.

## Decision I need
Where should the `binary/pr → goal` parse live?

- **(A) Keep it in the goal reader** (what I did) — goal owns "a .pr is json," parses at its boundary, hands `json.Reader` to the nested walk. Simple, localized; but the goal reader does `new json.Reader` (the 3a rule becomes "…except at a type's own content boundary").
- **(B) Move the parse to the dispatch/source layer** — make the `binary/<kind>`→inner-type narrowing (`type.reader.@this.Reader`, or `source`/`wire`) upgrade the scalar bytes into a structured reader BEFORE handing it to the goal reader, so goal (and every content type) always walks a structured `IReader` and never news one. Bigger change, touches the read perimeter; but then 3a's rule holds with no carve-out.

My lean is **(B)** long-term (it's the honest home for "content bytes → structured reader," and it fixes it for every content type, not just goal), but **(A)** is what's green now and unblocks the collection-deletion (Gate 2). Tell me whether to leave (A) or cut over to (B) — and if (B), whether that's this branch or a follow-up.

## Not blocked on this
(A) is green and committed. Gate-2 (delete the 3 collection classes, storage→List<child>+face, re-home Nest/RunAsync/Merge) is the next piece and is independent of A-vs-B. I'll proceed on Gate-2 unless you want B first.
