# Snapshots — a C# developer's guide

A snapshot is a captured copy of runtime state, written as a plain tree of plang
values. The runtime takes one when a run fails, so the failure can be replayed
from the captured position without a live model call. The same tree is what
`resume` reads back. This page covers the snapshot class, the `ISnapshot`
contract that each owner of state implements, which owners take part, the wire
form, and the resume path. It is written for the developer working on Runtime2
in C#.

```
app.snapshot.@this      PLang/app/snapshot/this.cs          namespace app.snapshot    the tree
app.snapshot.ISnapshot  PLang/app/snapshot/ISnapshot.cs     namespace app.snapshot    the owner contract
App.Snapshot(...)       PLang/app/this.Snapshot.cs          namespace app             capture and restore
App.SnapshotToWire(...) PLang/app/this.SnapshotWire.cs      namespace app             disk form
```

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> The root of the runtime is described in `../start.md`. The call stack that a
> snapshot records is `../call/start.md`.

> **Alias.** `Snapshot` is a global alias for `app.snapshot.@this`
> (`PLang/app/GlobalUsings.cs`). `ISnapshot` is a global alias for the interface.
> The name `Snapshot` also appears as a member on `app`, on `Data`, and on
> `Error`. Those are properties and methods, not the class.

> **Built on locked core.** Snapshots are not themselves on the locked-core
> list, but they ride surfaces that are — the serializer and `app`'s members. A
> change to what a snapshot captures touches those, so treat the capture shape as
> load-bearing and take surface changes through the architect.

---

## 1. A snapshot is a dict of Data

`app.snapshot.@this` (`PLang/app/snapshot/this.cs`) is a plang value: it
extends `app.type.item.@this` and implements `ICreate<@this>`. Its type name is
`snapshot`, set by `[PlangType("snapshot")]`.

It holds one thing: `Entries`, a `dict` of Data. There is no second structure.

```csharp
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public global::app.actor.context.@this Context { get; internal set; }
    public global::app.type.item.dict.@this Entries { get; }

    public @this(global::app.actor.context.@this context) { ... }
}
```

- **Born with a context.** The constructor takes the actor context. Entries are
  born as plang values in that context. A restored snapshot is born in the
  context that restores it, not the one that captured it (`serializer/Reader.cs`).
- **A section is an entry.** `Section(name)` returns the sub-snapshot stored under
  `name`, creating it if missing. It is an entry whose value is itself a
  snapshot. `HasSection`, `SectionNames` and `Sections` read them back.
- **`Write<T>(key, value)`** stores one entry. The value goes through
  `item.Create(value, Context)`, so a scalar becomes a plang value at the write.
  A value the door cannot make fails at this call, not later in a serializer.
- **Navigation is the dict's.** Plang reads `%snap.variables.x%` as the section
  `variables`, then the entry `x`. `Get` and `Set` on the snapshot delegate to
  `Entries`.
- **It writes itself.** `Output` opens an object, writes each entry's name and
  value, and closes the object. Nothing in the snapshot checks what kind of
  member an entry is.

---

## 2. The owner contract: `ISnapshot`

`ISnapshot` (`PLang/app/snapshot/ISnapshot.cs`) is the contract an owner of state
implements to take part in capture and restore.

```csharp
public interface ISnapshot
{
    string Section { get; }
    void Capture(@this s);
    System.Threading.Tasks.Task Restore(@this s, actor.context.@this context);
}
```

| Member | Contract |
|---|---|
| `Section` | The name of the owner's section in the tree. Each owner has one. |
| `Capture(s)` | Writes the owner's state into `s`. The owner writes its own section. |
| `Restore(s, context)` | Reads `s` back into this instance, the live owner. It hard-errors on referent-integrity failure. There is no silent fallback. |

Three rules from the interface's doc comment:

- **The owner owns both halves.** `Capture` and `Restore` live on the same type,
  so the shape of a section is written and read in one place.
- **References are by name.** An owner refers to another owner's data by name,
  the way plang already resolves names at runtime. `Restore` does not depend on
  pointer fixup.
- **The type is the classifier.** An owner that implements `ISnapshot` is in the
  snapshot-and-restore group. An owner that does not is rebuilt on build. The
  comment in `PLang/app/this.Snapshot.cs` lists the rebuilt group: Modules,
  Goals, Channels, Cache, Events, Settings, Types, Config and FileSystem.

---

## 3. Which owners take part

`App` decides the set and the order. In `PLang/app/this.Snapshot.cs`:

```csharp
private IEnumerable<global::app.snapshot.ISnapshot> Snapshotted(
    global::app.snapshot.ISnapshot variables, global::app.snapshot.ISnapshot callStack)
    => [Code, variables, _kept, this, callStack];
```

Providers come first, because a later owner may consume them. Restore walks the
same list in the same order. A section is restored only if the snapshot holds it.

| Owner | Section | Source | What it writes |
|---|---|---|---|
| `Code` (the provider registry) | `Providers` | `PLang/app/module/code/this.Snapshot.cs` | the registry layer: selections and registrations, not the provider instances. `Restore` throws `ProviderRestoreException` when it cannot resolve one. |
| `context.Variable` (variable memory) | `Variables` | `PLang/app/type/item/variable/list/this.Snapshot.cs` | each user variable as one entry, cloned. Names starting with `!` are skipped, and so are always-fresh values (`DynamicData`, computed items). |
| `_kept` (the app's kept members) | `Kept` | `PLang/app/type/item/kept/list/this.cs` | the members a program keeps on the app (`set %!app.home% = …`), by name, each as its Data. |
| `App` itself | `App` | `PLang/app/this.Snapshot.cs` | its `Mode`. `Restore` rebuilds the build controller and the test session from it. |
| the call stack | `CallStack` | `PLang/app/call/list/this.Snapshot.cs` | the active chain of frames (see §4). |

The call stack has two shapes. The list itself is an owner. `At(chain)` returns
a second owner, `Thrown`, which captures a chain that was already unwound. That
is how an error callback snapshots the frames as they stood at the throw.

---

## 4. Frames inside the call stack

A frame is not an owner. `PLang/app/call/this.Snapshot.cs` gives `call` one
`internal bool Capture(snapshot.@this s)`. The call stack calls it for each frame
and collects the results in a `frames` list.

A frame writes its position, not its state:

| Entry | What it is |
|---|---|
| `goalName`, `goalPrPath`, `goalHash` | the goal's identity. `goalHash` is a SHA-256 of the goal's name and step text. A mismatch on restore is a hard error. |
| `stepIndex`, `actionIndex` | where the frame sits in its goal. |
| `actionModule`, `actionName` | the action at that position. Restore checks the live action against these. |
| `id` | the frame's short id, kept for log correlation. |

`Capture` returns `false` for a frame with no action position, such as a goal or
a step frame. That frame writes nothing. Timing, diffs, tags and the items bag
are not captured.

---

## 5. Capture: two entry points

`App` has two capture methods. Both build the tree with the same private
`Capture`, which runs each owner's `Capture` into its own section.

```csharp
public snapshot.@this Snapshot(actor.context.@this context)
    => Capture(context, Snapshotted(context.Variable, context.call));

public snapshot.@this Snapshot(global::app.error.Error error, actor.context.@this context)
    => Capture(context, Snapshotted(context.Variable.SnapshotAt(error), context.call.At(error.CallFrames)));
```

- **Live.** `Snapshot(context)` captures the current variables and the live call
  stack. `IContext` gives action handlers a shortcut, `this.Snapshot()`
  (`PLang/app/module/IContext.Snapshot.cs`). The `pending` message in
  `PLang/app/channel/type/message/this.cs` holds one.
- **At the throw.** `Snapshot(error, context)` is the error path. By the time an
  error reaches its handler, the live stack has unwound past the failing action.
  So the variables are taken as they stood at the throw (`SnapshotAt`), and the
  frames come from the chain the error carried (`error.CallFrames`). The error
  itself calls this method (`PLang/app/error/Error.cs`).

---

## 6. Restore

```csharp
public async System.Threading.Tasks.Task Restore(snapshot.@this s, actor.context.@this context)
{
    foreach (var owner in Snapshotted(context.Variable, context.call))
        if (s.HasSection(owner.Section))
            await owner.Restore(s.Section(owner.Section), context);
}
```

A hard error stops the walk. The App is then left partly restored, and the
caller treats the failure as a referent-integrity violation. The `Providers`
section is the one that throws by design (`ProviderRestoreException`, an
`AppException` with status 500).

The call stack's `Restore` rebuilds the captured chain into `RestoredChain`. It
looks up each goal by name, then by `PrPath`, checks the hash, and resolves the
step and action by index. It does not change the live `Current`.

---

## 7. The wire form

The snapshot writes its own disk form. The files are in two places:

- `PLang/app/snapshot/this.Wire.cs` holds `Serialize(context)`. It encodes the
  snapshot with the `goal` format's kind, under `View.Store`, and returns a UTF-8
  string. The root is written bare, with no `Data` envelope, and it is not
  signed. A snapshot is in-process state, not an actor-boundary crossing.
- `PLang/app/this.SnapshotWire.cs` holds the App's pair. `SnapshotToWire(s)` calls
  `Serialize`. `SnapshotFromWire(json, context)` reads the string through the
  `snapshot` type, so the registered reader rebuilds it.

The reader is `PLang/app/snapshot/serializer/Reader.cs`. It is registered for the
any-kind reader. It reads a JSON object as a dict of entries, and each entry is
read through the parser. A nested object is read the same way, so a section
needs no special case.

---

## 8. Resume

`Resume(context)` in `PLang/app/snapshot/this.Resume.cs` is the path that turns
a captured snapshot back into a running goal:

1. `App.Restore(this, context)` puts the sections back.
2. The restored chain is read from `context.call.RestoredChain`.
3. The bottom frame re-enters its goal at the captured step and action
   (`goal.Resume`).
4. Each parent frame pushes itself, resumes its child, and then continues at
   the action after the call.

Two verbs reach it. The `resume` action (`PLang/app/module/snapshot/resume.cs`,
`[Action("resume", Cacheable = false)]`) takes a `Data<snapshot.@this>`. The
type declaration is what makes the runtime turn the bytes it was given into a
snapshot. The callback module (`PLang/app/module/callback/start.cs`) delegates
to `Callback.Snapshot.Resume` for a callback that carries one.

`ResumeFromWire(json, context)` in `PLang/app/this.SnapshotWire.cs` joins the
two: parse, then resume. The caller reads the file through the path verbs and
passes the string in.

---

## 9. What is not claimed here

- The snapshot is what the tests in `PLang.Tests/Wire/App/SnapshotTests/` cover.
  `SnapshotWireTests.cs` marks one case as waiting on a planned redesign. This
  page does not say which cases pass.
- `Data` has a `Snapshot` property (`PLang/app/data/this.Snapshot.cs`), marked
  `[JsonIgnore]`. It is in-process state and is not part of the wire form. The
  comment there describes when an action should attach one. This page does not
  verify that rule against the code.
- Not every owner that holds state is in the tree. A provider instance, a module
  and a goal are rebuilt on build, not restored from the snapshot.

---

## Find ...

| You want | Plang | C# | Source |
|---|---|---|---|
| the snapshot class | — | `app.snapshot.@this` (`Snapshot`) | `PLang/app/snapshot/this.cs` |
| the owner contract | — | `ISnapshot` | `PLang/app/snapshot/ISnapshot.cs` |
| capture the app | — | `App.Snapshot(context)` | `PLang/app/this.Snapshot.cs` |
| capture at a throw | — | `App.Snapshot(error, context)` | `PLang/app/this.Snapshot.cs` |
| restore the app | — | `App.Restore(s, context)` | `PLang/app/this.Snapshot.cs` |
| the section list and order | — | `Snapshotted(...)` | `PLang/app/this.Snapshot.cs` |
| the variables section | — | `variable.list.Capture`, `Restore` | `PLang/app/type/item/variable/list/this.Snapshot.cs` |
| the kept section | — | `kept.list.Capture`, `Restore` | `PLang/app/type/item/kept/list/this.cs` |
| the providers section | — | `Code.Capture`, `Restore` | `PLang/app/module/code/this.Snapshot.cs` |
| the call stack section | — | `list.Capture`, `At(chain)`, `Restore` | `PLang/app/call/list/this.Snapshot.cs` |
| one frame's entries | — | `call.Capture(s)` | `PLang/app/call/this.Snapshot.cs` |
| the disk form | — | `Serialize`, `SnapshotToWire`, `SnapshotFromWire` | `PLang/app/snapshot/this.Wire.cs`, `PLang/app/this.SnapshotWire.cs` |
| the reader | — | `serializer.Reader` | `PLang/app/snapshot/serializer/Reader.cs` |
| resume a snapshot | `- resume %snap%` | `snapshot.@this.Resume(context)` | `PLang/app/module/snapshot/resume.cs`, `PLang/app/snapshot/this.Resume.cs` |

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and the reading fundamentals |
| `../call/start.md` | the call stack and variable memory, whose frames a snapshot captures |
| `../type/start.md` | the type system, which the snapshot's `snapshot` type sits in |
| `../module/start.md` | the module system, which dispatches the `resume` action |
| `../actor/start.md` | the context a snapshot is born in |
