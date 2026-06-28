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
