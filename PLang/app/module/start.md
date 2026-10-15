# The module system

A **module** is one vocabulary of actions — `file`, `variable`, `list`, `llm`.
When you write a line of plang, the builder picks a module and an action, and at
run time that action's C# handler executes. This page is how that works on the
C# side: where modules come from, what a handler looks like, and how a built step
reaches it.

```
app.module.@this     PLang/app/module/this.cs     namespace app.module
```

> **Scope.** This page documents the C# — the registry, the handler shape, and
> dispatch. The user-facing *prose* for each module (what it's for, notes,
> examples, the text the builder's LLM reads) is **not** here: it is generated
> from the markdown under `os/system/modules/<module>/` — see §6. This page does
> not repeat it.

---

## 1. What a module is

`module.@this` is itself an `item` (`: app.type.item.@this`) tagged
`[PlangType("module")]`. One instance is one module — it carries a `Name`
(`"file"`, `"variable"`) and owns that module's actions.

There is a special one: **the app's module**, reached at `app.module`
(`%!app.module%`). It has an empty name and holds no actions of its own — it is
the *registry*: it carries `list`, every loaded module, and the lock that
registration takes. Every individual module reaches that same `list` as its own
`.list`.

```csharp
internal @this(global::app.@this app)      // the app's module (the registry)
{ Name = ""; _app = app; _modules = new(); _registering = new object();
  Discover(typeof(@this).Assembly); }

internal @this(string name, @this root)    // one real module, e.g. "file"
{ Name = name; _root = root; }
```

So `app.module` is the collection node (§4 of `../start.md`): `app.module.list`
is the `list<module>`, `app.module.file` selects the `file` module, and
`app.module.file.read` is the catalog element for the `read` action.

---

## 2. The registry: selection + lifecycle only

`module.@this` follows the rule from `../start.md` §4 — **registry = selection +
lifecycle; behaviour lives on the element.** The registry's whole job is to find
modules and actions and own their lifetime:

```csharp
public async ValueTask<data.@this<@this>> Get(string name)   // one module by name
public global::app.type.item.list.@this<@this> list          // all modules (native list<module>)
public action.@this? this[string actionName]                 // one action element in this module
```

It never branches on *what kind* of action it holds. The action's role (plain
step, clause, loop, keep) is decided once, at registration, by minting the right
subtype — see §5. There is no flag and no type-switch at use time.

---

## 3. Discovery: how a class becomes an action

At birth the app's module scans the assembly for action classes:

```csharp
public int Discover(Assembly assembly, string? baseNamespace = null)
{
    baseNamespace ??= "app.module";
    foreach (var type in assembly.GetTypes())
    {
        if (type.IsAbstract || !typeof(ICodeGenerated).IsAssignableFrom(type)) continue;
        if (type.GetCustomAttribute<ActionAttribute>() is not { } attr) continue;
        if (type.Namespace is null || !type.Namespace.StartsWith(baseNamespace + ".")) continue;
        Register(type.Namespace[(baseNamespace.Length + 1)..],          // module name = namespace after app.module
                 attr.Name ?? type.Name.ToLowerInvariant(),            // action name
                 type);
    }
}
```

Read that middle line carefully — it is the §2-of-`../start.md` rule in code:

```
PLang/app/module/file/read.cs
  namespace app.module.file        -> strip "app.module." -> module name = "file"
  [Action("read")]                 -> action name = "read"
                                   => file.read
```

The **module name is the namespace segment after `app.module`**, and the action
name is the `[Action("...")]` argument (or the lowercased class name). The module
is created the first time an action names it (`Of(name)` under the registry's
lock), so you never register a module explicitly — you just put an `[Action]`
class in the right namespace.

Built-in modules are discovered from the runtime assembly at startup. An external
DLL is brought in the same way at run time through the `module.add` action.

---

## 4. The action handler pattern

A handler is a `partial class` carrying `[Action]`. The whole of `file.read`
(`PLang/app/module/file/read.cs`):

```csharp
namespace app.module.file;

[Action("read")]
public partial class Read : IContext
{
    [IsNotNull]
    public partial data.@this<path> Path { get; init; }

    public partial data.@this<choice.@this<template.kind.@this>>? Template { get; init; }

    public Task<data.@this> Start() => Path.Use(path => Template is not { IsInitialized: true }
        ? path.Read(Context)
        : Template.Use(kind => path.Read(Context, kind.Value)));

    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(async path =>
    {
        if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
        return await path.Expect(Context);
    });
}
```

Point by point:

- **`[Action("read")]`** — the plang action name. With the namespace, this *is*
  `file.read`.
- **`partial class`** — the source generator (`PLang.Generators`) emits the other
  half: the `ICodeGenerated` dispatcher, the parameter-resolution for each
  `partial` property, and the not-null guards. You never implement
  `ICodeGenerated` yourself.
- **`partial data.@this<T>` properties are the parameters.** They are *lazy*: the
  generator fills each from the built step and resolves any `%var%` on first
  access. `data.@this<path> Path` is the carrier for the path argument;
  `data.@this<...>? Template` (nullable) is an optional one.
- **`Start()` is the entry** — what runs when the step executes. Its return type
  *is* the action's return contract (`Task<data.@this>` here; `Task<data.@this<T>>`
  for a concrete return type).
- **`Build()` is the build-time check** — run once when the step is built, to
  catch what can be caught early (here: warn if a literal path isn't there).
- **`.Use(...)` opens a carrier.** An owner takes plang values, not `Data<T>`
  carriers. `Path.Use(path => ...)` opens the `Path` carrier and hands the inner
  `path` value in; the carrier's own failure becomes the action's answer. This is
  the "owners take values; handlers open carriers" rule — you don't reach inside
  a carrier yourself, you `Use` it.

---

## 5. The attributes — the C# shape (not the prose)

A handler's *shape* is declared with attributes (`PLang/app/module/Attributes.cs`):

| Attribute | On | Meaning |
|---|---|---|
| `[Action("name")]` | class | marks the action; sets its plang name; `Cacheable` (default true) |
| `[IsNotNull]` | property | the parameter must be non-null before `Start()` (generator-validated) |
| `[IsInitiated]` | property | a `Data?` that must be initialized before `Start()` |
| `[Default(value)]` | property | value used when the builder omits the parameter |
| `[Code]` | property | injected from the `app.Code` provider registry |
| `[Whole]` | property | take the `Data` whole, including a failed one (don't short-circuit on failure) |

The **prose** — Description, Notes, Examples — is deliberately *not* in C#
attributes. It lives in markdown under `os/system/modules/<module>/` and is read
at run time by the builder (the LLM that decides and writes steps). See §6.

---

## 6. Where the module's user-facing prose lives

The registry knows each module's docs folder:

```csharp
internal path Folder          // os/system/modules/<Name>
public file.@this Description  // os/system/modules/<Name>/module.description.md
public file.@this Notes        // os/system/modules/<Name>/module.notes.md
public file.@this Guide        // os/system/modules/<Name>/module.guide.md
```

These are lazy file handles — an absent file is falsy, so a template can guard on
presence (`{% if module.Description %}`) without reading. Per-action prose
(`<action>.description.md`, `.notes.md`, `.examples.md`) lives in the same folder.

That markdown is the *runtime teaching layer* for the builder's LLM — a different
audience from this page. When you add or change what a module is *for*, edit the
markdown under `os/system/modules/<module>/`; when you change how it *works in
C#*, edit the `start.md` here. This page does not restate the teaching prose.

---

## 7. Dispatch: from a built step to `Start()`

A built goal is a `.pr` (one file per goal, all its steps). Each step's `code`
block names the module and action. Here is a real step — `set %modules% =
%!app.module.list%` from `os/system/builder/BuildGoal/.build/decide.pr`:

```json
{
  "index": 0,
  "text": "set %modules% = %!app.module.list%",
  "code": [
    { "module": "variable", "name": "set", "property": [ ... ] }
  ]
}
```

`module: "variable"` + `name: "set"` → the registry's `app.module.variable["set"]`
→ the catalog element holding `app.module.variable.Set`. At run time the registry
mints the handler with the run's context and starts it:

```csharp
internal ICodeGenerated? Create(string actionName, actor.context.@this context)
    => Handler(actionName) is { } clr && typeof(ICodeGenerated).IsAssignableFrom(clr)
        ? (ICodeGenerated)Activator.CreateInstance(clr, context)!
        : null;
```

You start an action through `action.Start(context)` — the generated
`ICodeGenerated.Start()` is an explicit interface member, so the handler's own
`Start()` and the generated dispatcher share the name in one partial class. You
never call the handler's `Start()` directly from outside.

Notice too the `property` block inside the step carries the parameters, and a
`%var%` parameter carries its decomposition as `code` hops (`!app` → `module` →
`list`). That is the same path-navigation story from `../start.md` §6: the
variable path is parsed once into hops, never re-scanned with a regex.

---

## 8. One action, several roles — the type *is* the role

Some actions are not plain steps. A clause (`on error`, `on cache`, `on
timeout`), a loop (`foreach`), a keep — each is a different element *subtype*,
decided once at registration by what interface the handler implements:

```csharp
_action[actionName] =
    typeof(IClause).IsAssignableFrom(clr) ? new action.clause.@this { ... }
  : typeof(ILoop).IsAssignableFrom(clr)   ? new action.loop.@this   { ... }
  : typeof(IKeep).IsAssignableFrom(clr)   ? new action.keep.@this   { ... }
  : new action.@this { ... };
```

So "the type is the role" needs no flag and no second home. A handler becomes a
clause by implementing `IClause` — the registry mints it as a `clause` element,
and everything downstream (building, dispatch, events) sees the role in the type.

---

## 9. Find an action's source

| You have | You want | How |
|---|---|---|
| a plang step | the handler | build it, read the `.pr` `code` row: `module`+`name` → `PLang/app/module/<module>/<name>.cs` |
| a handler class | its plang name | namespace after `app.module` = module; `[Action(...)]` = action |
| a module | its C# | `PLang/app/module/<name>/` — the folder is the module |
| a module | its user-facing prose | `os/system/modules/<name>/` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `PLang/app/module/file/start.md` | the `file` module, every action mapped to its source |
| `../start.md` | the app root and the reading-the-codebase fundamentals |
