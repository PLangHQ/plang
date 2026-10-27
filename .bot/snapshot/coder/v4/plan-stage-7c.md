# Stage 7c: a goal, a step and an action read back as themselves in the snapshot view (snapshot 93), read only

Patterns: read by the view, as the write is (1); a reader answers its own type (9); one door, asked at one place (7);
whoever numbers it finds it (a step by its goal, an action by its step).

## The trace (where a read reaches a type's reader)

A value slot reaches its type's reader in three places, all through `type.list.Reader`:

1. `type.Read(ref json, ctx)` (`type/this.cs:447`), the one door for a row's value: an eager type reads now
   (`:452`), any other slot is born a lazy `wire` (`:475`).
2. The lazy wire's first touch: `source.Value` (`type/item/source.cs:158`, async) → `wire.Read(context)`
   (`wire/this.cs:61`) → `plang.Read(source, ctx)` (`wire/kind/plang/this.cs:123`) → `Reader.Reader(type, kind)`.
   Then `await item.Value(data)` (`source.cs:243`).
3. A plain source's first touch: `source.Read` (`source.cs:301`) → `Reader.Reader(type, kind)`.

**The view is lost at (2).** `wire.Read` makes a new `ReadContext` with no view (`wire/this.cs:62`), so it reads under
Out whatever the bytes were written for. A wire is born from `ctx` at (1) but doesn't keep `ctx.View`.

**Who sets the snapshot view.** The snapshot's reader knows it is reading a snapshot
(`snapshot/serializer/Reader.cs:29–49`). Today it reads its entries under the asker's view. It reads them
`ctx with { View = View.Snapshot }` instead: the deserializer knows it is a snapshot restore.

## The mechanism

**(a) Where the view reaches the dispatch.** One internal member on the view, the mirror of `Output`:

```csharp
// View.cs: the reader a value of `type` is read by in this view (default: the type list's for its type and kind)
internal virtual reader.ITypeReader Reader(type.@this type, actor.context.@this context)
    => context.App.type.list.Reader.Reader(type.Name, type.kind.IsEmpty ? null : type.kind.Name, context);
// snapshot: a found type (one that tags [Snapshot]) is read by its key reader; any other as the default
```

(1), (2) and (3) ask `ctx.View.Reader(type, ctx.Context)` instead of `Reader.Reader(...)`. The wire is born holding
`ctx.View` (a birth fact, through the one `type.Make(slice, …)` mint, `type/this.cs:436`) and reads under it at (2).
Store, Out and Debug answer the default, so their reads don't change, and goal's `.pr` reader is untouched.

**Where a found type's key reader is found: one cost to name.** The registry is keyed by (type, kind), and a view is
not a kind. Two roads:

- **(A, recommended)** `ITypeReader` gains a default member `View? View => null` (the view a reader reads for; null for
  every view). The registry indexes a token reader by (type, kind, view), and the snapshot view asks for (type, *,
  Snapshot). The goal's key reader is `goal/serializer/Snapshot.cs`, `View => View.Snapshot`. **An addition to the
  locked `ITypeReader`: Ingi's.**
- **(B)** No locked addition: the key reader registers under a kind token named for the view (`"snapshot"`). The
  snapshot view asks (type, "snapshot"). This overloads one key with two meanings (a kind, a view), as 73 found
  before, so I don't recommend it.

**(b) How the load is deferred.** A type reader is sync; loading a goal may read its `.pr`. The precedent
(`DeferVerify`, `data/schema/signature.cs:122`, `wire/kind/plang/this.cs:84–97`) is the sync read keeping what it needs
and the async caller finishing. Here the order is reversed: the async half runs **before** the sync read, so the reader
always answers a goal.

- Every read under the snapshot view has an async caller just before it: (2) `source.Value`, and the stack's `Start`
  for a frame's record.
- That caller asks the view first: `await ctx.View.Load(source, context)`, an internal virtual. The default does
  nothing. The snapshot view reads the found key's location (the general read of the key, `pr` a file made unread from
  `{path}`, 80) and asks the goal list to hold it: `goal.list.Read(pr.Path, context)` (`goal/list/this.cs:83`, async,
  one read in flight per `.pr`; a fresh held goal answers without a read).
- Then the sync key reader finds it with no I/O: `goal.list[pr.Path]` (the list's one-look index, `:345`), the goal of
  that name in its chain (the file's goal or one of its children), and the hash compared exactly (73). A changed one is
  `GoalHashMismatch` 409, thrown to the read boundary as a `DeclinedException`, so `source.Value` answers it on the
  binding (`source.cs:246`). A key with no `pr`, or a `.pr` the list doesn't hold, is refused with the key it lacks.
- The `Load` member asks the found type to load what its key names. For goal that's the existing `ILoad<goal>` door,
  with goal's `Load` taking the `.pr` its key names (a behaviour change inside goal, no new surface). Another found type
  that never loads (a file) has nothing to load.

**(c) How a step and an action reach their owner.** The frame's place is three members in declared order: `Goal`,
`Step`, `Action`. Their keys are `{name, pr, hash}`, `{index}` and `{index, module, name}`.

- The stack's `Start` pushes the frame's kind (as a live push, under the frame current now), then maps the record onto
  it by the general read (`reflection/this.cs:176–198`), member by member in declared order. The place members get
  internal setters on call (now get-only, set by the constructor); that's internal, not surface.
- The general read hands each member's read the host it is mapping, as an internal `ReadContext.Host` (init only).
  - **Step's key reader:** `((call)ctx.Host).Goal[index]`, the goal's own goal-wide numbering (`goal[int]`), set just
    before by the Goal member.
  - **Action's key reader:** `((call)ctx.Host).Step.Code[index]`, checked by module and name
    (`CallbackActionMismatch`, as today).
  - Each answers its own type, a step or an action. A host with no goal or no step refuses the key.
- One check for a changed goal, the goal's key. The frame's own hash check (`call/this.Snapshot.cs:93–105`) goes with
  `Find`.

## What goes

View.Type and the writer asking it, `goal.kind.key`, `goal/serializer/Key.cs`, goal.Select's dict arm (back to text
only), and FoundElsewhereTests' file-record assertion, which was about the kind. The 92 findings 2–5 fall away.

## Locked-surface costs

- (A) `ITypeReader.View`, a default interface member: Ingi's. (B) avoids it at the cost of a key with two meanings.
- Everything else is internal: `View.Reader`, `View.Load`, `ReadContext.Host`, the wire holding its view, and call's
  place setters.

## After the go: the uncontroversial part of 7c

The stack's Store as its frames from the root (a list, no `children`, `current` or ids; the last is where the run
stopped), the task refusal kept, the frame's `Write` override and `call/this.Snapshot.cs` gone. Tests: FreshAppGoesOn
green; a binding's `%item%` and a handler's `%!event%` come back; `%!call.scope.x%` comes back; a save in a task still
refused; no `children`, `current` or ids in the text; a changed goal refused once, by its key.
