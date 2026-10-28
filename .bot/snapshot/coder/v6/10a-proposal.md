# 10a — the actor's hold, saved as its structure (proposal v2, nothing built)

Pattern: one door: the app writes itself, and each object reads itself back.
The trace and the finding (save the `on.event` action, not its held call) are accepted (203).

## Where the walk stops (6)

- The hold stays a C# class (190), written by the host walk over its tagged members, as variable memory's `row` is.
- The walk reads **public** properties only (`type/item/kind/reflection/Tagged.cs:30`, `GetProperties(Public |
  Instance)`). It stops at `actor.on`, which is `internal`, and the hold's class `event.on.list.@this` is
  `internal sealed`.
- So the actor's `on`, the hold's class and its `list` go public.
- The pre-pass reaches what the bindings name:
  - A list writes each row as a self-describing Data record (`type/item/list/this.cs:321-327`).
  - A binding row is typed `binding.action`, which chooses through its `[Snapshot] action`. That leads to action,
    then step, then goal path, so the goal is loaded.
  - The item row is typed (a goal's goes through goal's path).

## `on` writes and reads itself (3 against 4)

- A type with any `[Snapshot]` member is written by its members (`View.snapshot.Whole`), so `on` carries **no**
  `[Snapshot]` member.
- Its `item` is untagged.
- Its own `Write` runs. It writes `item` as a typed record, then each event that holds a saved binding, by name. That
  includes declared ones, which have no property.
- Its `Create` reads it back.

## Members (signature, and what it replaces)

1. `own` folds into `public class event.on.@this`, which holds the open lists.
   - `public static readonly event.on.@this empty` replaces `Empty`, `None` and the `No*` statics.
   - `empty.Of(type)` keeps the declared empties.
   - Removed: `own.Bind<T>`, `list.Bind(…handler…)` and `list.Bind<T>(…)`. Callers write
     `actor.on[item][name]![when].Add(side => …)`.
2. `internal binding.list.@this this[When when]` on `event.@this`. It replaces the `when == before ? …` inside
   `own.Bind`. Its two callers with a run-time When are `module/on/event.cs` and `module/debug/this.cs:146`. Callers
   with a fixed side (cache, timeout, error, test) use `.before` or `.after`.
3. `internal type.item.@this item { get; }` on `event.on.@this`, born with it (`new on(item, declared)`).
   Untagged. New.
4. `protected internal override ValueTask Write(IWriter)` on `event.on.@this` replaces the default walk.
   `public static event.on.@this? Create(object? raw, type.@this? type, data.@this data)` (ICreate) is new: it reads
   the item, then each other key as an event, each row added through its side.
5. `[Snapshot]` on the existing `event.@this.before` and `after`, and `binding.@this.Actor` and `Scope`. Tags only.
6. `[Snapshot] public goal.step.action.@this action { get; }` on `binding.action.@this` replaces
   `private readonly … _call`. It holds the `on.event` action that bound. `Handle` reads its `Action` when the
   binding fires.
7. `internal virtual bool IsSaved => false` on `binding.@this`, `true` on `binding.action.@this`: the side writes only
   those. This is (ii), to Ingi.
8. The hold:
   - `public sealed class event.on.list.@this`, which was internal.
   - `[Snapshot] public type.item.list.@this list { get; private set; }`: its `on` of each item with a saved binding.
     The private setter adds each, read back, under its item. New.
   - `[Snapshot] public event.on.list.@this on` on `actor.@this`, which was internal and untagged. It stays declared
     above Context, pinned.
9. Snapshot readers, each the pair of a written key:
   - `PLang/app/actor/serializer/Snapshot.cs`: name → the actor in the app it goes on in.
   - `PLang/app/serializer/Snapshot.cs`: name → that app. Another name is refused, `SnapshotAppMismatch`.
   - `PLang/app/channel/serializer/Snapshot.cs`: key → the actor's channel by name.
10. `internal override ValueTask<item?> Key(context)` on `channel.@this`: `{actor, name}`. New. A channel writes
    nothing in the snapshot view today (only [Out] and [Debug] members, no Key).
11. The action snapshot reader (`goal/step/action/serializer/Snapshot.cs`): a key with no step and no index reads
    as `module[name]`, the catalog action. Today it is refused (ActionKeyIncomplete).

## Items a binding can be on

| item | written as | reads back today |
|---|---|---|
| goal | `{name, path, hash}` | yes (goal reader) |
| step | `{index, goal}` | yes (step reader) |
| action in a step | `{module, name, index, step}` | yes (action reader) |
| catalog action (`%!app.module.file.read%`) | `{module, name}` | **no**, ActionKeyIncomplete. Fixed by 11 |
| module | `{name}` | yes (module reader) |
| type entity (`%!app.type.goal%`) | its identity (`type.Write` → `Identify`) | its type reader reads a name, as a .pr's type slot does; pinned at build |
| channel | nothing (no snapshot face, no Key) | **no**. Fixed by 10 and 9 |
| app | its name (Key) | **no**. Fixed by 9 |
| actor | its name (Key) | **no**. Fixed by 9 |

## The bound action is the action that fires

- The builder writes `on.event`'s `Action` as a literal nested action (e.g. `bindingstaysinitstest.test.pr`:
  `"Action": {"type": "action", "value": {"module": "goal", "name": "call", …}}`).
- Read when the binding fires, it is the same action.
- Its own parameters (`Name`, a `%var%` in them) resolve when it starts, in the firing context. They do that today
  too: `_call.Start(context)`.
- A step that wrote `Action=%x%` would resolve `%x%` again when it fires. The save refuses such a binding
  (`SnapshotBindingVariable`): an error key, not a member.

## Not saved

- C# closures (debug's, the test run's): bound again by their maker at start.
- Clauses on an action's own events: made again from the step.
- An `on.event` outside a step: no key, refused (`SnapshotBindingNoStep`).
