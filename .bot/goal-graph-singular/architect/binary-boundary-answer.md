# Binary→json boundary — A (goal owns its content decode); Debug on reflection is fine (settled w/ Ingi 2026-07-18)

Answers `coder/to-architect-binary-boundary.md` + the 3 OBP-scan notes in `coder/to-architect-obpscan.md`. Your A is committed and correct — keep it. Proceed to Gate-2.

## A/B — A. The content decode was always the type's own; my rule was scoped wrong.

There are **two different reads**, and you kept the right one right:

```
source.cs:186  Read()                                  // .pr-from-disk path
  :188  typeReader = App.Type.Reader.Reader(type.Name, kind, ctx)
  :189  var reader = new value.Reader(_value)           // SCALAR reader, by design
  :190  typeReader.Read(ref reader, kind, …)            // "the (type,kind) reader owns the decode"
```

`source.cs:180-185` verbatim: *"the (type, kind) reader owns the decode (a scalar off its token, csv its text, an image its bytes). A structured value never rides here."* The scalar `value.Reader` throws on `BeginObject` **by design** — the type decodes its own raw form. The peers prove it:

- `table/serializer/Reader.cs`: `csv.Read(reader.String(), …)` — pulls the string scalar, parses to a grid.
- `image/serializer/Reader.cs`: `reader.Bytes()` → `new image(bytes, mime)`.
- `goal`: `reader.RawValue()` → parse json → `Walk`. **Same category.**

So the "walk the handed IReader, never `new` one" rule refines into two reads — it was only ever about the first:

| read | who owns it | rule |
|---|---|---|
| **structural** — tokens inside an already-open doc (step, action, sub-goal recursion) | the handed reader | walk it; never `new` one. Your step/action readers are correct as-is. |
| **content-boundary** — raw scalar → the type's value | the type | pull `.String`/`.Bytes`/`.RawValue` (all the scalar reader offers) and decode; if the content is itself structured (goal's json), open a reader over it and walk — this is `source.Read`'s documented contract. |

**Reject B.** It would push "pr-content-is-json" into the perimeter — the exact opposite of `source.Read`'s "the type owns the decode" — and force `table`/`image` to the perimeter too. B chases the literal rule at the cost of leaking each type's content format upward. The `binary/<kind> → type` decode belongs on the type, decentralized, as it already is.

*(Minor, no action: on the channel path `goal.Read` receives a `json.Reader` and RawValue-reparses once. Harmless — the hot path is .pr-from-disk (scalar). Do NOT add an `if(structured)` fork to avoid the reparse; uniform decode beats a fork. If it ever profiles hot, the fix is unifying the two entry paths so the channel hands a scalar too — a perimeter cleanup, not goal's concern.)*

## The rule, restated for every reader (supersedes the absolute in read-shape-answer.md)

**The item owns any shape that must round-trip.** A reader walks the handed `IReader` for the structural/nested read and never `new`s one — EXCEPT at its own content boundary, where the type decodes its raw scalar into its value (opening a format reader over structured content is that decode, not a violation). Grep tell for "is this the boundary or the walk": a `new *.Reader` is legitimate only in the method that receives the outer scalar (`Read<TReader>` at a content type), never in a nested walk.

## OBP-scan verdict

- **#1 Debug via reflection — fine, keep it.** Not a fork. **The item owns the round-trip *wire* (Store/Out) — a reader reads it back, identity is byte-pinned, so the item writes it explicitly.** Debug is *inspection*: nothing reads it back, no round-trip, no stability contract. "Show everything" is what the generic reflection inspector is for, and every object inspects the same way. So `if (mode == Debug) → reflection` is routing to a different *concern* (inspect vs persist) with a different owner — not two paths for one operation. It doesn't weaken "the item owns its wire," because Debug isn't a wire (no reader to mirror → nothing to own). Leave `goal.Output`/`step.Output`/`action.Output` as they are.
- **#2 transitional `new actions.@this()` in the readers — blessed as a documented Gate-1 seam.** Green now with the classes alive; the single construction line in each reader **must flip to `List<child>` the moment Gate-2 deletes them.** On the record as intentional, not missed.
- **#3 `goal.Walk` (65 lines) — stays under A, not a smell.** It's the structural object walk shared by the entry (post-decode) and sub-goal recursion; the content-boundary/walk split is necessary. Long because it's a field-per-key deserialize switch — cohesive, inherent to a reader.

## Next

Gate-2 is clear to proceed (independent of this): delete the three collection classes, storage → internal `List<child>` + native-list face, re-home `Nest`/`RunAsync`/`Merge` per `items-answer.md`, and flip the reader construction lines (#2) to `List<child>`. Run `Tools/ObpScan` on the re-homed members before pushing — a re-home that lands on the wrong owner keeps the smell.
