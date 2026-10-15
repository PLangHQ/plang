# Settings — the values an owner reads for a run

A setting is a class of options that an owner lets a program or a run set.
The app has one of its own. A module has one. An action may have one, or a
bare node that stands for its options. This page is for the C# developer who
needs to read a setting, add an option, or follow where a value comes from.
It covers the app's own class, how code reads a setting, and the layers a
read builds from. The call frame and the module have their own pages; this one
links to them rather than repeating them.

```
app.setting.@this     PLang/app/setting/this.cs     namespace app.setting
app.setting.@this     base: app.type.item.setting.@this  (PLang/app/type/item/setting/this.cs)
app.actor.setting     PLang/app/actor/setting/this.cs    the layers a read builds from
```

> **Scope.** The base class (`type/item/setting`) and its kind are the type
> system's; read them in `../type/item/start.md`. The layering rule here is the
> actor's (`PLang/app/actor/setting/this.cs`). The read is reached through
> `context.Setting`, which is one layer of that chain.

---

## 1. What a setting is

A setting is a class that derives from `type.item.setting.@this`. Its public
settable properties are the options. Each property's initializer is its
default. The class names its own path by its namespace:

| Class | Path | Read as |
|---|---|---|
| `app.setting.@this` | `app.setting` | `%!app.setting%` |
| `app.module.llm.setting.@this` | `llm.setting` | `%!llm.setting%` |
| `app.call.setting.@this` | `app.call.setting` | `%!app.call.setting%` |

The path is the namespace, with the `app.module.` prefix removed for a module.
The module case is in the base constructor
(`PLang/app/type/item/setting/this.cs`). An owner reads its settings by that
path. A class does not need to store the path itself.

The app's own class holds these options
(`PLang/app/setting/this.cs`):

| Option | Type | Default | Meaning |
|---|---|---|---|
| `Create` | `bool` | `False` | allow creating a new app where none exists (`--app={"create":true}`) |
| `Name` | `text?` | unset | the app's name; unset, the app goes by its folder's name |
| `Environment` | `text` | `"production"` | the environment the app runs in |
| `Culture` | `culture` | `Machine` | how numbers read as text; JSON and the wire never use it |

Every setting node also answers `Enabled` and `Disabled`, from the base class.

`Id` is not an option. The class overrides `Next` to answer `id` from
`parent.Context.App.Id`. It can be read, and it is never set.

> **Options carry `[Out, Store]`.** The app's options travel on the wire and
> persist to the store. A new option that should not persist takes no tag.

---

## 2. How code reads a setting

The read is `context.Setting.Of<T>()`. It returns the class built for this
context, with its defaults, the saved row, and this run's values applied:

```csharp
// PLang/app/this.cs
=> context.Setting.Of<global::app.setting.@this>().Environment;
```

Other callers do the same with their own class, for example
`context.Setting.Of<global::app.module.llm.setting.@this>()` in
`PLang/app/module/llm/code/OpenAi.cs`. The result is read-only by contract.
A caller that changes an option changes a copy, not the built class.

| You have | You want | Where |
|---|---|---|
| a `context` | the built class | `context.Setting.Of<T>()` |
| a `context` | a setting by path | `context.Setting[path]` |
| a `context` | this run's value for a path | `context.Setting.Set(path, value)` |
| a `context` | a saved row, written back | `context.Setting.Save(setting)` |

Reads are cached per version. A write anywhere in the chain moves the version,
so a built class is rebuilt on the next read.

---

## 3. The layers a read builds from

`context.Setting` is one layer in a chain. Each layer chains up to its parent,
and the top layer chains to the actor's (`PLang/app/actor/context/this.cs`,
`Setting` property):

```csharp
public global::app.actor.setting.@this Setting => _setting ??= new(this, Parent?.Setting ?? Actor.Setting);
```

`Actor.Setting` falls back to the system's layer
(`PLang/app/actor/this.cs`). A build applies, in order:

1. the class's **defaults**, from its initializers;
2. the **saved row** for this actor (the user's, else the system's);
3. **this run's values** for the class's own options, the closest scope first
   (`set %!x%`, `--app={…}`, the CLI flags).

The saved rows are read once, when the app starts. A value written in a
closer scope shadows a value in an outer one. The settings' own store is
`/.data/setting/data.sqlite` (`PLang/app/actor/setting/this.cs`). It is not
the app's data store.

> **Scope of a read.** The chain above is the settings' layering. What a
> call frame does with settings (its `context` per push, and what
> `%!app.call.setting%` captures) is in `../call/start.md` §3. The call
> frame reads its own class through the same `context.Setting`.

---

## 4. Module settings

A module's settings are read by the module, not the app. `%!llm.setting%` is
`context.Setting.Of(module)`: the module's own class when it has one, else a
node. An action's settings work the same way through `Of(action)`. The
module's own options are a class like the app's
(`PLang/app/module/llm/setting/this.cs`), with `Cache`, `Key`, and an endpoint.
The module model and its action handlers are in `../module/start.md`, §1 and
§2.

---

## Find ...

| You want | Where |
|---|---|
| the app's own options | `PLang/app/setting/this.cs` |
| a module's options | `PLang/app/module/<name>/setting/this.cs` (e.g. `llm`) |
| the base class and its kind | `PLang/app/type/item/setting/this.cs` |
| the chain a read builds from | `PLang/app/actor/setting/this.cs` |
| the context's layer | `PLang/app/actor/context/this.cs` (`Setting`) |
| a call's settings | `PLang/app/call/setting/this.cs` |
| a read of one class | `context.Setting.Of<T>()` in `PLang/app/actor/setting/this.cs` |

---

## The tree from here

| Page | Covers |
|---|---|
| `../start.md` | the app root, the collection-node graph, and what `app` holds |
| `../call/start.md` | the call stack, and frame-scoped settings (§3) |
| `../module/start.md` | the module system, and a module's own settings |
