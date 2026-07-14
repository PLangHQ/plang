# Cluster 3 — the producer isn't `context.Ok(goal)`; it's a `[Out]` host field. One fork for you.

Implementing ruling (a). Traced the real producer (frame above `writer.Value`) — it differs from the
sketch's shape, and the difference decides WHERE the lift goes, so flagging before I touch the
serialization core you fenced.

## The actual producer

```
test.report.Run → test.report.Wire(results) → list.Output → data.Output
  → app.test.@this.Output → OutputTagged → WriteReflected(value) → writer.Value(goal) → THROW
```

- The item is `app.test.@this` (`app/test/this.cs:16`), serialized as a tagged item.
- The offending slot is a **field, not a Data**: `[Out] public required global::app.goal.@this Goal { get; init; }`
  (`:30`). `goal` is confirmed a **host** — `app/goal/this.cs:22 sealed partial class @this` with no
  `item.@this` base — so `WriteReflected`'s `case @this item` misses and it falls to
  `default: writer.Value(value)` (`type/item/this.cs:540`), which throws.

So there is no `context.Ok(goal)` / `data.Set(goal)` site to fix — the raw host rides an **`[Out]`
property of an item**, written by the generic tagged-reflection path.

## Why I stopped instead of applying the sketch

The lift has three candidate homes, and two of them are the exact boundaries your ruling fenced:

1. **`WriteReflected`'s `default`** → lift a host via `item.@this.Create(value, context)` before
   writing. This IS `item.@this`'s own output machinery (arguably "the producer"), and it routes
   through `clr(goal).Output` which **respects `[Out]`** (not the raw-reflection option-b you
   rejected). But it's one line from "lift at serialization," touches EVERY tagged item, and changes
   raw-scalar handling unless I gate it to non-BCL objects.
2. **`app.test.@this`** stops exposing a raw goal on the wire — either the field isn't `[Out]` and a
   lifted/`clr(goal)` projection is, or it stores the goal lifted. Keeps the change local to the
   producer, but it's a `required [Out]` field with a wire-name/shape contract I don't want to change
   blind (does the report consume the whole goal, or just its name/path?).
3. The field's **domain type** stays `goal` (used for `HasSkipTag`, `Complete`) — so it can't simply
   become `item.@this` without breaking domain callers.

## The ruling I need

Which home: (1) `WriteReflected.default` lifts non-BCL hosts to `clr` (general, respects `[Out]`, but
"lift at serialization"); or (2) `app.test.@this` lifts its `Goal` at the `[Out]` boundary (local, but
needs the wire contract — whole goal as `clr(goal)`, or just identity)? If (2), confirm the wire
should carry `clr(goal)`'s reflected `[Out]` fields (goal.Name/Path/…), which is what the report reads.

Proceeding with **Cluster 2 (http getter throw + fixture attach)** meanwhile — that ruling is
unambiguous.
