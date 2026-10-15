# Warnings — what the build notes without stopping

This is where you start when the builder reports something that did not stop
the build, and you need to know where it was raised and where it lands. It
covers the one warning type, the collection that holds warnings on a node, the
doors a warning is raised through, and where a warning is kept. It is for the
developer working *on* Runtime2 in C#. A warning is a build-time note. It is
never a runtime answer; a runtime failure is an `Error` (see `../error/start.md`).

```
app.warning         PLang/app/warning/         namespace app.warning
app.warning.@this   PLang/app/warning/this.cs  sealed class @this  (Key, Message)
app.warning.list    PLang/app/warning/list/this.cs   the warnings one node holds
```

`app.warning` is not a member of the app root. `PLang/app/this.cs` has no
`warning` property. A warning is reached through the node it hangs on:
`goal.Warning` and `step.Warning` (both `warning.list.@this`) and
`note.Warning` (`PLang/app/type/item/note/this.cs`).

---

## 1. What a warning is

A warning is a `{Key, Message}` pair. The builder attaches it to a goal or a
step, and it says that the program was accepted but something is not as
written. The type is a plain sealed class, not an `item` and not a `Data`:

```csharp
public sealed class @this
{
    [LlmBuilder, global::app.Debug] public string Key { get; init; } = "";
    [LlmBuilder, global::app.Debug] public string Message { get; init; } = "";
}
```

`Key` is the name of the condition (`"Unsure"`, `"Reopened"`,
`"CorruptPrFile"`). `Message` is the human line. Neither field is nullable.

> **A warning does not stop the build.** The type's own doc comment draws the
> line: a build error that aborts never produces a node to hang a warning on,
> and one that does not abort is a warning. The same comment says runtime
> failures are errors. They land on the run (`Call.Errors`, the `Data` result),
> never on the graph.

---

## 2. The collection: `warning.list`

`warning.list.@this` holds the warnings of one node. It is a C# domain
collection, not a plang type. It has its own backing list, so no caller
reaches into the list directly:

| Member | Does |
|---|---|
| `Add(warning)`, `AddRange(...)`, `Clear()` | the only writes |
| `Count`, `this[int]`, enumeration | the read surface (`IReadOnlyList<warning>`) |

There is no `Remove`. The one `Clear` in the code is a step taking another
step's warnings: `goal/step/this.cs` clears its own list, then `AddRange`s the
source's.

---

## 3. How a warning is raised

There are three doors. They differ in where the warning ends up.

| Door | Raised by | Where it lands |
|---|---|---|
| `action.Warn(problem, context)` | an action's own `Build()` | the `"builder"` channel only |
| `node.Warning.Add(new warning { ... })` | the builder and goal code | the node's `Warning` list |
| `Warning.AddRange(...)` | the step's pick (`Agree`) and notes | the step's or note's `Warning` list |

### 3a. `action.Warn` — from an action's `Build()`

`Warn(Error problem, context)` is the one door a handler's `Build()` warns
through (`PLang/app/goal/step/action/this.Build.cs`). It takes an `Error`, but
only its `Key` and `Message` are used. It writes the pair, naming the action,
to the build's `"builder"` channel:

```csharp
// PLang/app/module/file/read.cs
if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
```

The `file.read` action warns at build when a literal path is not there yet.
`path.Absence` returns the `Error` that says why, or `null`. See
`../module/file/start.md`. Other callers are `build/unreached.cs` and
`on/event.cs`.

> `Warn` does not add to any `Warning` list. Nothing is stored on the node.
> Only the channel sees it.

### 3b. `Warning.Add` — from the builder or goal code

Code that holds a node adds to its list. Examples:

- `module/build/code/Default.cs`: a `.pr` that will not read adds
  `CorruptPrFile` to the goal's `Warning`, and the goal rebuilds from source.
- `goal/this.cs` (`Reopen`): a cached step whose saved code no longer holds is
  reopened and gets a `Reopened` warning on the step.

### 3c. `AddRange` — the pick and the notes

- `goal/step/pick/list/this.cs` (`Agree`) builds `Unsure` and `Popular`
  warnings for an action the decider was not sure of. The step takes them in
  `goal/step/list/this.cs`.
- `type/item/note/this.cs` builds `NoteWith{Title}` and `{Title}WithoutNote`
  warnings when a note and its parts disagree.

---

## 4. Where warnings surface

| Surface | When | Source |
|---|---|---|
| the `"builder"` channel | while the build runs | `action.Warn`; rendered by `os/system/builder/EmitBuildEvent.goal` |
| the step's `"warning"` array in the `.pr` | when the `.pr` is written | `goal/step/this.Item.cs` writes `key` and `message` when `Count > 0` |
| read back from the `.pr` | when a `.pr` is loaded | `goal/step/serializer/Reader.cs` (`case "warning"`) |
| merged from a copied step | when one step is copied from another | `goal/step/this.cs` (`AddRange(from.Warning)`) |

> The `.pr` keeps the warnings of steps only. The goal and note warnings
> described in §3b and §3c are not written by any goal writer that was found
> in this pass. Treat that as unverified until checked.

---

## Find ... — cheat-sheet

| You have | You want | Look in |
|---|---|---|
| a warning line in the build output | who raised it | `builder` channel → `goal/step/action/this.Build.cs` → `Warn` |
| a `Key` such as `Unsure` | the code that adds it | `goal/step/pick/list/this.cs` → `Agree` |
| a `Key` such as `CorruptPrFile` | the code that adds it | `module/build/code/Default.cs` |
| a `Key` such as `Reopened` | the code that adds it | `goal/this.cs` → `Reopen` |
| a step's `warning` array in a `.pr` | how it was written and read | `goal/step/this.Item.cs`, `goal/step/serializer/Reader.cs` |
| the collection on a node | its shape | `warning/list/this.cs` |
| a failure that stops the build | an `Error`, not a warning | `../error/start.md` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, and the path = namespace = source rule |
| `../error/start.md` | the fatal side: an error answers, a warning only notes |
| `../module/start.md` | the action modules that raise warnings at build |
| `../module/file/start.md` | `file.read`, the action that warns on a missing literal path |
