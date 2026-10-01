# Learnings — OBP formal doc example fixes (v1)

Source: educator's verification of `Documentation/v0.2/object_pattern_formal.md`
against app-systems code. Six examples flagged; five were stale, one was branch
drift. Concrete, reusable insights:

1. **Channels belong to the actor, not the app root.** There is no `app.Channel`.
   Channels register on an actor (`actor.Channel.Register(...)`); action handlers
   write through their own `Channel` (the `IChannel` surface) via
   `await Channel.WriteAsync(...)` (`module/output/write.cs`). When writing a
   root-navigation example, use something actually on `app` (`app.Cache`,
   `app.goal`, `app.module`, `app.type`, `app.actor`).

2. **The callstack's error collection is `Audit`.** `callstack/this.cs` exposes
   `public error.list Audit { get; }` — not `Error`. A *frame* has its own
   `Errors` (`callstack/call/this.cs`), and appending to a frame also appends to
   the stack's `Audit` (`Errors.Add(error); _stack.Audit.Add(error);`).
   `app.error.list.@this` exposes `Newest` + `IReadOnlyList` enumeration — there is
   no `.List` member to "enumerate".

3. **A goal runs its steps via `on.start`.** The shape is
   `on.start.Before(this, context)` → `Step.Start(context)` → `on.start.After(this,
   result, context)` (`goal/this.cs`). There is no `Lifecycle` type and no
   `Step.Load`. The goal delegates to the `Step` collection; it never loops steps.

4. **Type-system paths.** There is no `app/type/type/` folder and no
   `app.type.type` namespace. The type collection is `app.type` (a **lowercase**
   property; the access path is `App.type`, not `App.Type`), class `app.type.@this`
   at `app/type/this.cs`. Concrete scalar types live at
   `app/type/item/<name>/this.cs` = `app.type.item.<name>.@this` (e.g.
   `app.type.item.text.@this`).

5. **Leaf write pattern (Data rides sealed).** `file.save` is the canonical shape:
   `await Path.Use(async path => (data.@this) await path.Save(Value, Context))`
   (`module/file/save.cs`). The value (`Value`) rides in whole; the leaf never
   cracks `(...Value()).Bytes` open for a static helper.

6. **Verify examples against the TARGET branch, not the reporter's commit.**
   `path.Size` was flagged as an async method taking context on the educator's
   commit, but on current app-systems it is a lazy property that does the work on
   access (`type/item/path/file/this.cs`) — exactly what the doc illustrates. Left
   unchanged. Branch drift cuts both ways; `grep`/read the branch you will merge
   into before "fixing".
