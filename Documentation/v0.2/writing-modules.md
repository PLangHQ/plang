# Writing PLang Modules — Field Notes

A living document. Each session that uncovers a pattern, a miss, or an "I should have known that" lands here. Append, don't rewrite. When a section grows past its weight, lift it out into its own doc and link from here.

## What a module is

A directory under `PLang/App/modules/<name>/`, lowercase. One C# file per action handler, file name matching the action token (`load.cs`, `read.cs`, `setDefault.cs`). The PLang verb the developer types (`- read file.txt`) maps to `<module>.<action>` (`file.read`).

## Handler skeleton

```csharp
using App.Variables;

namespace App.modules.<modulename>;

[ModuleDescription("Manage X — list, load, etc.")]   // first action only
[System.ComponentModel.Description("Read a file's content; ...")]
[Example("read file.txt, write to %content%",
    "file.read Path([path] file.txt) | variable.set Name([string] %content%), Value([object] %__data__%)")]
[Action("read")]
public partial class read : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Default(false)]
    public partial Data.@this<bool> ResolveVariables { get; init; }

    [Code]
    public partial IFile Files { get; }   // resolved from app.Code.Get<IFile>()

    public Task<Data.@this> Run()
    {
        // ...
    }
}
```

The canonical reference is `PLang/App/modules/file/read.cs`. Read it before writing a new handler.

## Property kinds (PLNG001)

The build-time gate is non-negotiable. Action handler properties must be one of:

- `Data.@this<T>` (or its nullable form) for parameters the developer supplies.
- `[Code] T` for pluggable behavior eagerly resolved from `app.Code.Get<T>()`.

Anything else fails the build with **PLNG001**. There is no `partial string Foo`, no `partial T Foo` without `Data.@this<>`. The generator does not emit a backing for raw types.

## File-path parameters: use `FileSystem.Path`, never `string`

Wrong:

```csharp
public partial Data.@this<string> Path { get; init; }
```

Right:

```csharp
public partial Data.@this<FileSystem.Path> Path { get; init; }
```

Why this matters:

- `Path` (`PLang/App/FileSystem/Path.cs`) carries `Absolute`, `Relative`, `Extension`, `FileName`, `Directory`, `MimeType`, `IsFile`, `IsDirectory`, `Exists`, `Size`, and `GoalCall`. A handler that takes `string` forfeits all of these and forces every consumer to re-parse.
- It validates against the app root (`Fs.ValidatePath`) and resolves relative paths off the current goal's runtime directory. Free security and correctness.

How the auto-wrap works: `Path` is decorated with `[PlangType("path")]` and exposes `public static Path Resolve(string rawPath, Context context)`. The source generator hooks both — the LLM sees the type as `"path"`, `Data.As<Path>` calls `Resolve`, and the wrapped value carries `Context` through `IContext` so subsequent properties (`Extension`, `Exists`, etc.) work without re-plumbing.

The same shape applies to any class decorated with `[PlangType(...)]`: prefer the domain class to the primitive.

## Variable-name parameters: `Data.@this<Variable>`

Parameters that *name* a variable rather than carry a value — write targets like `variable.set Name`, read-by-name lookups, foreach `ItemName`/`KeyName`, list operation targets — take `Data.@this<App.Variables.Variable>`.

`Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly. The result: both `Name="%x%"` and bare `Name="x"` collapse to `Variable { Name = "x" }`.

Consume via `.Value`. The implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally. A non-nullable `Data.@this<Variable>` slot gets a generator-emitted pre-`Run` guard that surfaces `MissingRequiredParameter`.

## Positional / variadic args: `List<Data>?`

When an action accepts a list of positional values whose count and types vary per call (the use case is rare — script-runners, generic formatters):

```csharp
public partial List<Data>? Args { get; init; }
```

Each element is a `Data` carrying its own type. Coerce per slot at the use site with `Data.As<T>(Context)`.

This is not a substitute for keyword parameters. Reach for it only when the action genuinely is variadic.

## Common attributes

- `[Action("name")]` — required. Maps the C# class to the action token. Add `Cacheable = false` for actions whose effects shouldn't be memoized at runtime (registry mutations, side-effecting calls, anything whose result depends on state outside its parameters).
- `[ModuleDescription("...")]` — once per module, on the first action file. The LLM reads it to know what the module is for.
- `[System.ComponentModel.Description("...")]` — every action. Single-line summary the LLM uses for action selection.
- `[Example(natural_language, action_chain)]` — show the LLM the canonical phrasing → handler mapping when the description alone won't reliably steer it. The chain uses `|` as the action separator and `[type] value` to hint typed arguments.
- `[Default(value)]` — declare a default for an `init` property. Pairs with `Data.@this<T>` — the wrapped value reads as `value` when the developer doesn't supply one.

## Result shape

Return `Task<Data.@this>`. Use `Data.@this.Ok(value)` for success, `Error(new ActionError(...))` for typed failures. Don't throw out of `Run()` — wrap exceptions into `ActionError` at the handler boundary so the runtime can route them through the error-handling pipeline.

## Live additions

Append below as we discover new patterns. Each addition should name the miss or the surprise that taught us, in one line.

- 2026-05-09 — `Data.@this<FileSystem.Path>` is the only correct shape for path parameters. `string` is wrong even for "just a filename". (Caught while drafting `code.run`.)
- 2026-05-09 — When a handler's owner needs cached/derived state (a compile cache, a loaded assembly, a reflection probe), do **not** introduce a record-shaped "Entry" type that another class indexes into. The OBP rule applies to internal services as much as to modules: the live object owns its state and exposes the behavior. Wrong shape — `ScriptEntry { hash, alc, type }` + `_scripts` table on `Code` + `RunScript()` orchestration method. Right shape — a `Compiler.@this` that owns the cache privately and returns a `Runtime.@this` which owns the ALC and exposes `Start` / `Invoke`. If the data has behavior attached, the data should be a class. (Caught while drafting `code.run`.)
