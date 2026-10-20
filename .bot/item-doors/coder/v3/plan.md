# item-doors — stage 2, one door for writing (coder plan, v3)

Shape agreed with the architect (stage-2 trace, ruled in 107). `Output` is the only door: each type's own `Output`
writes what it is. Five commits, in this order, each green on its own.

## (a) every `Write` becomes `Output`
- The 49 item `Write(IWriter)` overrides become `Output(IWriter, View, context?)`: a leaf writes its token and returns
  `default`; a structure awaits its members' `Output`.
- `item.Write` goes; base `Output` is the reflection kind's (no `IsLeaf` branch; `IsLeaf` itself stays, read elsewhere).
- The 56 sync `.Write(w)` callers await `Output` (including `Error.Write` in `step/this.Item.cs`).
- Kind-level `Write(value, w)` (number's kinds, duration's) stays sync.
- Pinned by: every view's existing writes (Wire suite: serializer, round-trip, snapshot; the surface golden).

## (b) the writer carries its output kind
- `IWriter.Format` (string) → `IWriter.Kind` (`kind.@this`). json: item's json kind, or wire's plang kind while
  `_emitsSchema` (interim, marked for stage 3); text: text's own format; formal: `[Format("formal")]` on
  `action.list.@this` → `{action.list, formal}`. If `[Format]` can't take a format with no MIME and no extension, stop.
- Re-pin the type page / decider pins if they move (read the diff).

## (c) the pairs, in data.Output
- `writer.Kind.pair[input kind]`, held by the output kind; a miss falls back through the input kind's parent.
- A kind meeting itself is the identity pair: it relays a value's written form; a value with none writes itself.
- `Owns` and its 5 checks go; list's `_formats` becomes formal's pair (a list in formal is a list of arguments).
- If the identity pair can only reach the written slice through a new virtual on item, stop (item's virtuals are locked).
- Surface re-pin citing 846 and 107.

## (d) the byte door
- `Bytes(byte[] value, kind kind)` replaces `Bytes(byte[])` and `Raw(byte[])`; the writer decides per kind.

## (e) json's `Value(object)` keeps C# primitives only
- Its data/dict/list arms and the text writer's leaf arm go; those values arrive through `Output`.

## Limits
- Nothing of stage 3 (the record door, EmitsSchema) early.
- Production edits by Edit, file by file; affected suites before each push, the full sweep for (a).
