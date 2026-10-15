# The goal and step subsystem — a C# developer's guide

A goal is one `.goal` file, built once into one `.pr`, and run as a list of
steps. This page covers the C# that holds those goals and steps: how a goal is
loaded and found, what a step is, and how a step's code reaches an action
handler. It is for the developer working *on* Runtime2 in C#.

> **Scope.** This page documents the C# shape. The `.goal` syntax, the builder
> that writes a step's code, and the prose the builder's LLM reads live
> elsewhere: the builder is in `os/system/builder/`, and the module prose is in
> `os/system/modules/`. Module dispatch (the `.pr` `code` row → handler) is
> documented in `../module/start.md` §7 and is not repeated here.

```
app.goal.@this       PLang/app/goal/this.cs       namespace app.goal
app.goal.step.@this  PLang/app/goal/step/this.cs  namespace app.goal.step
app.goal.list.@this  PLang/app/goal/list/this.cs  namespace app.goal.list
```

---

## 1. What a goal is

A goal is a `.goal` file's entry point, or a named sub-goal written after it in
the same file. The builder reads the `.goal` text, writes one `.pr` file with
every step in it, and the runtime reads that `.pr` back into a goal. Nothing
parses `.goal` text at run time.

```
Start.goal                         the source, written by the user
.build/start.pr                    the build, one file, all steps (Goal.Pr)
app.goal.@this                     what the .pr reads into
```

The `.pr` path is derived, not stored in the source. `Goal.Pr(source)` puts it
in a `.build/` folder beside the `.goal`, with the stem lowercased:

```csharp
public static path.@this Pr(path.@this source)
    => source.Parent.Combine(".build").Combine(source.Stem.ToString().ToLowerInvariant() + ".pr");
```

A goal **is an item**. `goal/this.Item.cs` gives the partial class its base,
`app.type.item.@this`, and the item interfaces that make it a plang value
(`ICreate`, `IMatch`, `ICurrent`, `ILoad`, `IList`, `IEncode`, `IDecode`). The
goal's own state is plain properties on the partial in `goal/this.cs`:

| Property | Meaning |
|---|---|
| `Name` | the goal's name, as written on its header line |
| `Comment` | the comment lines written above the name (the goal's description) |
| `Step` | the goal's steps, a `step.list.@this` |
| `Child` | the sub-goals written after it in the same file, a `child.@this` |
| `Path` | the `.goal` file, a `path.@this` |
| `PrPath` | the `.pr` it is built to (derived from `Path`) |
| `Parent` | the goal this one is a sub-goal of; null for a file's first goal |
| `Visibility` | `Public` for a file's first goal, `Private` for the rest |
| `Address` | the app-absolute name a call uses (see §3) |
| `Hash` | SHA-256 of the source as written; used by the build's cache |

> **Two goals, one file.** A `.goal` file holds one public goal (its first) and
> any number of private sub-goals. `goal.Parse` (in `goal/this.cs`) builds the
> first goal and puts the rest into its `Child` list. Every sub-goal's `Parent`
> is set by `child.@this` when it is added, so a sub-goal always knows its owner.

---

## 2. The goal collection: `app.goal`

`app.goal` is the goal type's node. It is a `type.current.@this<Goal, goal.list.@this>`,
so it has a `list` (the goals read so far) and a `current` (the goal in play):

```csharp
// PLang/app/this.cs
public global::app.type.current.@this<Goal, goal.list.@this> goal { get; }
```

| plang | C# | What it is |
|---|---|---|
| `%!app.goal%` | `app.goal` (the `type.current` node) | the goal type's node |
| `%!app.goal.list%` | `goal/list/this.cs` | every goal read so far, a `list<goal>` |
| `%!app.goal.current%` | `goal.@this.Current(context)` | the running goal (`context.call.Goal`) |
| `%!app.goal["/system/error/show"]%` | `goal.list.@this.Get(key)` | one goal by its address |

`goal.list.@this` (`goal/list/this.cs`) is the collection's own work. It is a
`type.item.list.@this<goal.@this>` with four jobs:

- **Load** a goal from its `.pr`. `goal.@this.Load(path, app)` reads the `.pr`
  (or returns the one already held), refuses a setup goal, and adds what it
  reads with `list.Add(goal)`. A held goal with the same `PrPath` is replaced.
- **Find** a goal a call names. `list.Find(name, caller)` answers in order: the
  caller's own chain (the caller, its children, then each ancestor and its
  children); then the goals already held; then a `.goal` that loads beside the
  caller, then from the app root.
- **Walk** every goal of the app and of `/system/`. `Walk(setting, context)`
  yields held goals first, then reads each remaining `.goal`'s `.pr` as the walk
  reaches it. `Walk()` is the no-asker form: the system actor asks.
- **Setup**, through `goal.list.setup.@this` (see §6).

`Get(key)` on the type matches a goal by its address through `goal.@this.Match`.
A key that names a sub-goal (`/start#show`) is matched against each child's
`Address` too.

> **`current` has no list behind it.** `goal.@this.Current(context)` reads
> `context.call.Goal`. The running goal is whatever the call stack says is in
> play, not a field on the collection.

---

## 3. Addresses

A goal's address is how a call reaches it from anywhere in the app. It is
computed, not stored:

- A file's first goal: its `.goal` path without the extension.
  `/system/builder/EmitBuildEvent` is a goal in `system/builder/EmitBuildEvent.goal`.
- A sub-goal: its parent's address, a `#`, and its own name. `/start#show`.

```csharp
public string? Address
{
    get
    {
        if (Parent is { } parent) return parent.Address is { } above ? $"{above}#{Name}" : null;
        var path = Path?.ToString();
        ...
    }
}
```

`Goal.Reference(caller)` gives the name a call from another file writes for this
goal. A goal in the caller's own file stays bare, so a child in the same file
cannot be shadowed by another goal of the same name.

---

## 4. Steps: `goal.step.@this`

A step is one line of a goal: a `- ` line in the `.goal`, and its indented body
if it has one. `step/this.cs` and `step/this.Item.cs` define it. A step is also
an item (`PlangType("step")`).

| Property | Meaning |
|---|---|
| `Index` | the step's position in its goal, from 0 |
| `Text` | the step's text, as written; continuation lines are joined with `\n` |
| `Comment` | the `/ …` comment written above the step |
| `Line` | a `step.line.@this`: the `Number` in the `.goal` and the `Indent` |
| `Code` | the step's actions, a `step.action.list.@this` (the `.pr`'s `code`) |
| `Goal` | the goal the step belongs to (a birth fact, see below) |
| `Intent` | the builder's intent for the step, build-time |
| `Warning` | warnings the build attached to the step |

`Goal` is set with `init`. The step is born holding its goal; nothing stamps it
in afterwards:

```csharp
[JsonIgnore]
public global::app.goal.@this Goal { get; init; } = null!;
```

`Code` is the bridge from a step to the module system. A built step is a list of
actions, and each one names a catalog element (§5). A step with no `Code` is a
step the builder has not written yet.

### Running a step

`Step.Start(context)` pushes the step's own frame onto the call stack, runs its
`on.start` events before and after, and runs `Code.Start(context)`. That runs
each action in order and stops at the first one whose result says to exit:

```csharp
public async Task<data.@this> Start(actor.context.@this context)
{
    // push frame → on.start.Before → Code.Start(context) → on.start.After
}
```

The step catches exceptions. A program error that travelled as an
`AppException` becomes the step's answer, whole. Any other exception becomes a
result through `Error.FromException`.

### Build-time properties

Four properties on the step answer questions the build asks and nothing at run
time reads: `IsCached`, `IsFormal`, `IsAnswered`, and `Pick`. They are marked
`[JsonIgnore]`, and `Pick` is not in the `.pr`. The builder's own docs describe
what each one means for a step.

---

## 5. Actions: `step.action.@this`

A step's `Code` holds `step.action.@this` elements. Each one is a call: a
module, a name, and the parameters the step set.

| Property | Meaning |
|---|---|
| `Module` | the module element this action belongs to (`app.module.@this`), not its name |
| `Name` | the action's name, e.g. `read` |
| `Property` | the parameters the step set, a `type.property.list.@this` |
| `Default` | the frozen values for parameters the step did not set |
| `Child` | the steps of a control-flow body (`if`, `foreach`); empty on other actions |
| `Step` | the step this action belongs to (a birth fact for program actions) |

`Module` is an object, not a string. `Module[Name]` returns the **catalog
element** for this action: the one the registry made when the handler class was
discovered (see `../module/start.md` §3). A step's action is a *program* action,
and the catalog element is what gives it its handler class, its parameter list,
and its return type. The two are different objects.

### From a step's action to the handler

The path from an action to its handler class goes through the module registry
and is documented in `../module/start.md` §7. The short form, as the
action's own code reads it:

```csharp
// PLang/app/goal/step/action/this.cs
public (module.ICodeGenerated? Code, error.Error? Error) Instance(actor.context.@this context)
{
    if (Module[Name] is not { } element)
        return (null, ActionError.NotFound($"Action '{Module}.{Name}'"));
    ...
    var code = Module.Create(Name, context);
    ...
}
```

`Instance` checks that the catalog element exists and that every property the
step set is one the element declares. A `.pr` built before a parameter was
removed fails here with `UnknownProperty`, not silently. Then `Module.Create`
mints the handler and `Start()` runs it.

### One action, one frame

`action.Start(context)` pushes one call frame for the action's whole run,
including its `on.start`, `on.error`, and dispatch. A retry is another attempt
in the same frame. `Attempt` runs the before-bindings, `DispatchAsync`, and the
after-bindings. `DispatchAsync` calls `Instance`, then `Resolve` (the generated
parameter binding), then the handler's own `Start()` through
`ICodeGenerated`. The action's `on.start` and `on.error` bindings are read at four
levels (`Level`): the action type's, its module's, its catalog element's, and its
own, the general wrapping the specific.

### Action roles

The action class carries the role. A clause (`clause.@this`), a loop, or a keep
is a subtype of `action.@this`, and it overrides one part of the step's chain:
`Attach`, `Anchor`, `Follow`, `Refuse`, `Place`. `Program(step)` mints a
program action of the same kind as its catalog element. Nothing branches on a
flag at run time.

---

## 6. Child goals and setup

**Child goals.** `goal.child.@this` (`goal/child/this.cs`) is the sub-goal list.
Adding a goal to it sets that goal's `Parent`. Sub-goals inherit the owner's
`Path`, so a relative path in a sub-goal resolves against the same folder.

**Setup.** `goal.list.setup.@this` (`goal/setup/this.cs`) runs setup goals once
per step, at app startup. A setup goal is one named `Setup`, or one in the
app's `/setup/` folder. `DiscoverAsync` reads only two fixed locations:
`/Setup.goal` and `/Setup/Setup.goal`, each through its `.pr`. A step's hash is
recorded in the `setup` table of the app's store. A step whose hash is already
there does not run again. A changed step has a new hash and runs again.

> **Setup is not a walk.** `goal.list.Walk()` skips setup goals (`IsSetup`).
> Setup is found only by the two fixed locations above, so a large app with
> many `.pr` files is not scanned at startup.

---

## 7. From a `.pr` to these classes

The `.pr` is a JSON file, one per goal. The goal's own reader and the step
reader turn it into the objects above:

| `.pr` shape | C# |
|---|---|
| the whole file | `goal.@this` (`goal/serializer/Reader.cs`) |
| each entry of the step array | `step.@this` (`goal/step/serializer/Reader.cs`) |
| each `code` row | `step.action.@this` (`goal/step/action/serializer/Reader.cs`) |
| a `code` row's `module` and `name` | `action.Module` (the catalog element) and `action.Name` |
| a `code` row's `property` | `action.Property` |
| a sub-goal entry | `goal.@this` via `child.@this` |

`goal.@this.Decode` reads the `.pr` bytes, and `goal.@this.Encode` writes the
goal back out through the item's own writer (`goal/this.Item.cs`).

> **Shell first.** The reader builds the goal shell before its steps and
> sub-goals, so each step is born holding its goal and each sub-goal holds its
> parent. Nothing repairs `Goal` or `Parent` after the read.

---

## 8. How to find the source for a goal or step

| You have | You want | How |
|---|---|---|
| a `.goal` file | its goal object | `goal.@this.Load(path, app)`, or `Find` from a caller |
| a `.goal` file | its `.pr` | `goal.@this.Pr(path)`: `.build/<stem lowercased>.pr` beside it |
| a goal's address (`/start#show`) | the goal | `app.goal["/start#show"]` or `goal.list.Get` |
| a step's text | the step | `step.@this.Text`; its `Index` is its place in `Goal.Step` |
| a step's `code` row | the handler | `module` and `name` on the row → `../module/start.md` §7 |
| a step's action | its catalog element | `action.@this.Module[Name]` |
| the running goal | the goal in play | `app.goal.current`, which reads `context.call.Goal` |

---

## Find ...

| Find | plang | C# | Source |
|---|---|---|---|
| the goal collection | `%!app.goal.list%` | `goal.list.@this` | `goal/list/this.cs` |
| a goal by address | `%!app.goal["/start"]%` | `goal.@this.Match` | `goal/this.Item.cs` |
| the running goal | `%!app.goal.current%` | `goal.@this.Current` | `goal/this.Item.cs` |
| a goal's steps | `%goal.Step%` | `step.list.@this` | `goal/step/list/this.cs` |
| a step's actions | `%step.Code%` | `step.action.list.@this` | `goal/step/action/list/this.cs` |
| an action's catalog element | `%action.Module%` | `module.@this` | `goal/step/action/this.cs` |
| a goal's sub-goals | `%goal.Child%` | `child.@this` | `goal/child/this.cs` |
| setup goals | (none; system only) | `setup.@this` | `goal/setup/this.cs` |
| the `.pr` a goal is built to | `%goal.PrPath%` | `goal.@this.Pr` | `goal/this.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and reading the codebase |
| `../module/start.md` | the module registry, the action handler pattern, and dispatch |
| `../module/file/start.md` | the `file` module, an action end to end |
| `../type/start.md` | the type system: `type.current`, `item`, and the value lattice |
