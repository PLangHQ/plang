# The `file` module

A worked example of a module, end to end. If you have read `../start.md` (the
module system), this is the same ideas on one concrete, small module — seven
actions, each a short handler. It is the best place to start reading real action
code.

```
app.module.file     PLang/app/module/file/     namespace app.module.file
```

> The user-facing prose for `file` (what each action is for, examples the builder
> reads) lives in `os/system/modules/file/`, not here. This page is the C#.

---

## The seven actions

Each action is one file in this folder. The plang you write, the file that runs
it, and what it returns:

| plang (what you write) | file | `[Action]` | returns | cacheable |
|---|---|---|---|---|
| `read X` | `read.cs` | `read` | a reference (`Data`) | yes |
| `write %v% to X` / `save X` | `save.cs` | `save` | `Data<path>` | no |
| `list files in X` | `list.cs` | `list` | `Data<list<path>>` | yes |
| `if X exists` | `exists.cs` | `exists` | `Data<path>` | — |
| `copy A to B` | `copy.cs` | `copy` | `Data<path>` | no |
| `move A to B` | `move.cs` | `move` | `Data<path>` | no |
| `delete X` | `delete.cs` | `delete` | `Data<path>` | no |

Two things to read off that table:

- **The mutating actions are `Cacheable = false`** (`save`, `copy`, `move`,
  `delete`) — set on the `[Action]` attribute. Reads (`read`, `list`) cache.
- **The return type is the contract.** `Start()`'s signature is what the catalog
  reports as `→ returns ...`, so a trailing `write to %x%` knows the type it
  captures. `list` returns `Data<list<path>>`; `exists` returns `Data<path>`.

---

## `read` — the canonical handler

The whole of `read.cs`, the one to internalize:

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

- `read X` **lands a reference** — a `file`, a `url`, or a `directory` — with
  nothing read yet. The content is that reference's own value, read at first
  touch. The path itself knows what reading it lands.
- The handler **does no I/O directly.** `Path.Use(path => path.Read(Context))`
  opens the carrier and asks the `path` value to read itself. All filesystem
  access goes through the `path` verbs, never `System.IO.*`.
- `Template` is an optional parameter (nullable carrier). Given, it is opened with
  its own `.Use` and its kind handed to `Read` (e.g. read a `.plang` template and
  fill `%variables%` from memory).
- `Build()` runs at build time: for a literal path, warn if it isn't there now
  and record the expected reference; a path holding a `%var%` is only known at run,
  so build does nothing.

Because `read` returns across schemes, the `file` module is not "local files" —
it is every path scheme (`file`, `http`/`https`, and any registered scheme). The
scheme is resolved from the raw path; an unregistered one (`s3://…`) is a typed
`SchemeNotRegistered` failure, not a crash.

---

## `exists` — existence is the path's truthiness, not an action

`exists.cs` is the most instructive of the small ones — it does **no** I/O:

```csharp
[Action("exists"), global::app.Attributes.Question]
public partial class Exists : IContext
{
    public partial data.@this<path> Path { get; init; }
    public Task<data.@this<path>> Start() => Task.FromResult(Path);   // just returns the path
}
```

It returns the path unchanged. Existence is answered *later*, by the path's own
truthiness: a comparison like `if %result% exists` routes `== true` through
`Data.ToBooleanAsync()` → `path.AsBooleanAsync()`, which **stats** the filesystem
for a file scheme or issues an **HTTP HEAD** for an http scheme. Keeping the path
live means re-testing it reflects the current state. This is the
`IBooleanResolvable` rule — truthiness belongs to the value, so `exists` has
nothing to compute. The `[Question]` attribute marks it as a yes/no action.

---

## The mutating actions — same shape, `From` + `Use`

`save`, `copy`, `move`, `delete` all have the same two-method shape. `save.cs`:

```csharp
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<path>> Start() => data.@this<path>.From(
        await Path.Use(async path => (data.@this)await path.Save(Value, Context)));

    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(path =>
    { path.Add(Context); return Task.FromResult(Context.Ok()); });   // the saved file joins the build's files
}
```

- `Value` is a bare `data.@this` — `save` takes whatever value it is handed and
  lets the `path` write it.
- `data.@this<path>.From(...)` re-types the `Data` the owner produced back to the
  action's `Data<path>` return contract.
- `copy` and `move` add `Source`/`Destination` and an `Overwrite` flag
  (`[Default(false)]` — a flag is false by default); `copy` also has a `Subfolder`
  choice. `delete` adds a `Recursive` flag. Their `Build()` is a no-op when either
  path holds a variable.

---

## Map it back to the fundamentals

- **Find the source for `move A to B`:** build it → `.pr` `code` row
  `{ "module": "file", "name": "move" }` → `PLang/app/module/file/move.cs`.
- **Owners take values, handlers open carriers:** every action here is
  `X.Use(x => x.Verb(...))` — the handler never reaches inside a carrier; the
  `path` value owns the I/O.
- **No `System.IO.*`:** nothing in this folder touches the filesystem directly;
  it all goes through `path` verbs, which pass the auth gate.

See `../start.md` (the module system) for discovery, dispatch, and the attribute
reference, and `../../start.md` for the path = namespace = source rule.
