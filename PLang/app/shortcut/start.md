# The shortcut subsystem: a C# developer's guide

A shortcut is a name that reads as a goal's answer. `%!goal%` runs the shortcut
named `goal` and gives its return value, and it does so before the app's member
of that name is looked at. This page covers the C# that holds shortcuts: how
they are read at startup, how a name is resolved to one, and where the
resolution sits. It is for the developer working *on* Runtime2 in C#.

> **Scope.** This page documents the C# shape. The teaching prose for shortcuts
> and the goal syntax that uses them live elsewhere. Variable resolution is
> documented in `../type/start.md`, and the goal itself in `../goal/start.md`.

```
app.shortcut.@this       PLang/app/shortcut/this.cs       namespace app.shortcut
app.shortcut.list.@this  PLang/app/shortcut/list/this.cs  namespace app.shortcut.list
```

---

## 1. What a shortcut is

A shortcut is a goal under a `shortcut/` folder, named by its file. A goal at
`/shortcut/goal.goal` is the shortcut `goal`. The name is the file stem, not the
goal's header line:

```csharp
/// <summary>Its name — its goal's file (<c>goal.goal</c> → <c>goal</c>), read as <c>%!goal%</c>.</summary>
[global::app.Out]
public string Name => Goal.Path?.Stem.ToString() ?? Goal.Name;
```

`shortcut.@this` is a plang item (`[PlangType("shortcut")]`) that holds one
`goal.@this`. It is built with an internal constructor, so only the list makes
one. `Goal` is the goal a read starts. `IsSystem` is true when that goal sits
under `system/`, and a system shortcut's name is sealed: an app goal cannot take
it.

The shortcut is a structure, not a leaf (`IsLeaf => false`). It is matched by
its name, compared the way a variable's name is compared:

```csharp
public System.Threading.Tasks.ValueTask<@this?> Match(string key)
    => new(global::app.type.item.variable.list.@this.Comparer.Equals(key, Name) ? this : null);
```

### Reading one

`Read(asker)` is what `%!goal%` returns for the asker. It starts the shortcut's
goal in the asker's context, under a call of its own:

```csharp
public async System.Threading.Tasks.Task<global::app.data.@this> Read(global::app.actor.context.@this asker)
{
    await using (asker.call.Isolate(null))
        return await Goal.Start(asker);
}
```

Because the call is isolated, every write the goal makes ends with the read. The
asker's `%!data%` is the same after the read as before it. A shortcut goal reads
who asked through the call stack, `%!app.callstack.scope.caller.goal%`.

## 2. The app's shortcuts: `shortcut.list.@this`

`app.shortcut` is a `type.@this<shortcut.@this, shortcut.list.@this>`, declared
on the app root in `PLang/app/this.cs`. Its `list` is the app's shortcuts. The
list is read once, lazily, at the first ask:

```csharp
private readonly System.Lazy<System.Threading.Tasks.Task<global::app.data.@this>> _read;

public System.Threading.Tasks.Task<global::app.data.@this> Read() => _read.Value;
```

The read is done in `Fill()`, in two passes:

| Pass | Folder | Result |
|---|---|---|
| 1 | `/system/shortcut/` and the os's `system/shortcut/` | each system shortcut, the app's own copy first; names are sealed |
| 2 | `/shortcut/` | each app shortcut; a name a system shortcut holds is refused |

A goal is found with `app.goal.list.Find(address)`, the same lookup a call
uses. A goal that does not load is left out and said on the debug channel. A
clash in pass 2 is not left out. `Fill()` returns a `ShortcutCollision` error,
naming both files, and every later ask answers it too.

> **Read once, at startup.** `app/this.cs` `Launch()` calls
> `shortcut.list.Read()` before any goal runs. A clash therefore stops the app
> before its first goal, not on the first `%!name%` that uses it. A shortcut
> goal built after the read is not in the list until the app starts again.

`Walk()` reads the list first if it has not been read, and throws the read's
error if the read failed.

## 3. How a name resolves

A `%!name%` read reaches `Variable.Start` in
`PLang/app/type/item/variable/code/Variable.cs`. The order is fixed:

```
%!name%
├─ own bindings (context.Variable.Get)        !app, !data, !event, … answer first
├─ app.shortcut.Get(name) → Read(context)     a shortcut, when the name is one
├─ the app's member (!app → name)             %!build% while building
└─ the module named name                      %!llm%
```

The shortcut comes before the app member. So `%!goal%` is the shortcut when
`/shortcut/goal.goal` or `/system/shortcut/goal.goal` exists, even though the
app has a `goal` member. The member is still reachable by its full path:
`%!app.goal%` always reaches `app.goal`, the collection node (see
`../goal/start.md` §2).

> **Bindings still answer first.** A name the run has bound (`!data`, `!event`)
> is never a shortcut. Only a name the memory does not bind reaches the
> shortcut list.

## 4. Where the code sits

| Piece | File | Role |
|---|---|---|
| the type node | `PLang/app/this.cs` | `app.shortcut`; `Launch()` calls `shortcut.list.Read()` |
| the item | `PLang/app/shortcut/this.cs` | `Name`, `Goal`, `IsSystem`, `Match`, `Read` |
| the list | `PLang/app/shortcut/list/this.cs` | the one-time read, the two passes, the collision refusal |
| the resolver | `PLang/app/type/item/variable/code/Variable.cs` | the `Start` order in §3 |

## Find ...

| Find | plang | C# | Source |
|---|---|---|---|
| the shortcut type node | `%!app.shortcut%` | `shortcut` on the app | `PLang/app/this.cs` |
| the app's shortcuts | `%!app.shortcut.list%` | `shortcut.list.@this` | `PLang/app/shortcut/list/this.cs` |
| one shortcut's answer | `%!goal%` | `shortcut.@this.Read` | `PLang/app/shortcut/this.cs` |
| a shortcut's name | (the file stem) | `shortcut.@this.Name` | `PLang/app/shortcut/this.cs` |
| the startup read | (none; runs in `Launch`) | `shortcut.list.@this.Read` | `PLang/app/shortcut/list/this.cs` |
| the resolution order | `%!name%` | `Variable.Start` | `PLang/app/type/item/variable/code/Variable.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and reading the codebase |
| `../goal/start.md` | goals, steps, and the `goal.list` lookup a shortcut is read through |
