# Final-stage cleanup — read-path-unification (Ingi decides each)

Items surfaced while building the read path. Held for the LAST stage so Ingi makes the
call rather than them landing silently mid-flight.

## 1. `goal` should be `clr`, not a plang type  *(Ingi's call)*
`goal` (`app.goal.@this`) is a host CLR object, not a plang VALUE — it shouldn't be a
"type" with a `serializer/Reader.cs`. A thin `goal/serializer/Reader.cs` was added as a
**bridge** to unblock the read (wraps `Deserialize<Goal>(GoalReadOptions)`). End-state
option: stop treating goal as `item`/a type — it rides as `clr`; remove the goal reader +
goal-as-type machinery (`TypeFromMime` plang-goal narrowing, the goal type entry).
**Decision deferred to Ingi.**

## 2. Verb+Noun method names (flashing-sign smells, pre-existing)
- `channel.@this.StampType(context)` (`PLang/app/channel/this.cs`) — decides the
  `{type, kind}` for content bytes from the channel's `Mime`, already delegating to
  `Format.TypeFromMime`. Inline it / move onto the owner.
- `format.list.@this.TypeFromMime(mime)` (`PLang/app/format/list/this.cs:444`) — "what type
  does this mime name" should be a noun-navigation on the mime/format, not a verb method.

## 3. Context non-null (continue context-never-null)
- `source.Context` is non-null in shape but the births still allow null until `Judge` /
  `WireLocal` (the context-less paths) are removed. Then: `source` born-with-context in the
  ctor (throw on null, like `type.Create`), `Data.FromRaw` context param non-null, and the
  boundary `!`s at `FromRaw` / `Judge` deleted. This is the WireLocal/Judge phase.

## 4. Rename `asking` → `data` across the `item.Value` / `Create` family
`asking` is a made-up role name (not OBP — name the variable after its type, a `data.@this`).
Renamed in `source` only. Finish across every override + factory: `item/this.cs:47`,
`path`, `text`, `url`, `file`, `computed`, `variable`, `ICreate.cs`, `list/this.Generic.cs`,
`permission`, `snapshot/this.Wire.cs` (params + `<paramref>` + comments).

## 5. Delete `Readers.Of` + the delegate registry (the Stage-1 goal proper)
With every value type owning a `serializer/Reader.cs` and `source.Value` dispatching through
the serializer + `App.Type.Readers.Reader(...)` (narrow + throw), the old whole-payload `Of`
path is dead: delete `Readers.Of`, the `Read` delegate type, the `_generated`/`_runtime`
tables, the static-`Read` discovery branch, the per-type `serializer/Default.cs` `static Read`
methods, and `type.Deserialize`'s `Of` use. Confirm no caller remains first.

## 6. TEMP context-less `Convert` fallback in `source.Value`
`source.Value` keeps a `Context == null → Create(...).Convert(s)` branch so the context-less
`Judge` sources still read. It dies with the context-less births (#3, WireLocal/Judge phase) —
delete it then so `source.Value` is the single serializer dispatch.

## 7. OBP debt from the data.reader extraction (logged 2026-06-28)
From the OBP review after extracting `app.data.reader.@this` and the no-DOM work:

- **`ReadPropertiesObject` / `ReadPropertyPrimitive` are Verb+Noun** (now in `app/data/reader/this.cs`).
  Linked to the next item — fixing properties-as-source dissolves both.
- **Properties read into raw CLR** — `ReadPropertyPrimitive` builds `List<object?>` /
  `Dictionary<string,object?>`. A property value should ride as a lazy `source` like a value
  slot does (`never_lower_to_clr`), not a CLR bag. This is the "data reader pulls properties
  via RawValue" item from the plan — the real OBP target. Doing it removes both Verb+Noun
  helpers above.
- **Duplicated wire field-name literals** (`"name"/"type"/"value"/"properties"` in the reader
  switch + `Normalize`/`Wire.Write`). Tried single-sourcing as `Wire*` consts → reverted:
  "Name"/"Type"/"Value" are verb-ambiguous so `WireName` etc. read as verb phrases (broke the
  Verb+Noun rule). The literals are the wire contract; if single-sourced later it needs a
  non-verb-phrase shape, not flat `Wire*` constants.
- **`json.Reader.RawValue` buffer-vs-DOM branch** — a fast-path/fallback (identical output,
  gated on owning the buffer). Transitional; vanishes when STJ is removed for nested Data.

## 8. Inspect `app/data/this.cs` (+ partials) for removable code (logged 2026-06-28, Ingi)
Data has accreted code that the read-path-unification work may have made dead or redundant.
**`this.Normalize.cs` is a prime suspect** (the write path now flows through `source.Write` /
`json.Writer` — Normalize may be partly or wholly superseded), and "more" beyond it. NOT a
quick delete — needs inspection: trace each method's live callers before removing, confirm no
behavior is lost (Data/Wire green + a write-path probe), watch for the wire-shape knowledge
that legitimately lives there. Candidates to inspect: `this.Normalize.cs`, and any `this.*.cs`
partial whose role overlaps the new reader/serializer split.

## 9. Properties read — route each value through the value reader (don't hand-roll) (2026-06-28)
Loose-end-1 of stage 2. `ReadPropertiesObject`/`ReadPropertyPrimitive` are an OBP violation:
they REINVENT value-reading (a bespoke string/number/object/array → CLR switch on the envelope
reader) instead of using the path the value slot already uses (serializer → the type's reader).
A property value IS a value — read it the SAME way; `Properties` owns the collection iteration.
That dissolves both methods.

Blocker (why it's deferred, not done): the **sync `Properties[key]` getter**
(`Properties : IDictionary<string, object?>`) — routing through the value reader yields plang
items, not raw CLR, and ~10–15 consumers read CLR directly (`Create(kvp.Value).Clr(...)`,
`%x!key%`, Normalize/Wire.Write enumeration). So this is coupled to making property access
async (or eagerly materializing). The fix is the PATTERN (don't invent a property reader),
NOT an IReader rewrite of the hand-rolled switch — that attempt mis-advanced the
signature-wrapped path (`42`→double) and was reverted. Kept on `Utf8JsonReader` until done right.
