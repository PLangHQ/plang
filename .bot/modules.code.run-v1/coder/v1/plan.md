# modules.code.run/v1 — coder v1 (minimal, designed live with Ingi)

## What this is

A new PLang action: compile a C# file at runtime, load it into a collectible
ALC, and invoke a method on it (default `Start`). Designed in conversation —
the architect's plan was deliberately set aside in favor of a much smaller
design Ingi sketched in pseudocode:

```
- run mycode.cs                         // calls Start()
- run SumList %x%, %y% in mycode.cs, write to %sum%
```

## Shape (three files, no registry, no caching)

```
PLang/App/Code/PluginLoadContext.cs     NEW   collectible ALC, default-fallback
PLang/App/Code/Runtime/this.cs          NEW   wraps loaded asm + entry type
PLang/App/Code/this.Load.cs             NEW   partial — Load(path) → Data<Runtime>
PLang/App/modules/code/run.cs           NEW   action handler, four real lines
```

`App.Code.@this` (the existing provider registry) is **not modified** beyond
the partial-class addition of `Load`. The provider registry's data structures
(`_providers`, `_builtInDefaults`) stay untouched.

`Load(FileSystem.Path path)`:
- `.cs` → compile via Roslyn to in-memory bytes, then load via PluginLoadContext
- `.dll` → read bytes directly, load via PluginLoadContext
- Returns `Data.@this<Runtime.@this>`

`Runtime.@this`:
- Owns its `AssemblyLoadContext` and entry `Type` (first public class)
- `Start(Context)` → `Invoke("Start", ..., Context)`
- `Invoke(name, args, Context)` — picks `(Context)` ctor if present, else `()`,
  binds positional args via `Data.As<T>(Context)`, awaits, unwraps `Task<T>`
- `IAsyncDisposable` — `_alc.Unload()` on dispose

`code.run` handler params:
- `Data.@this<FileSystem.Path> Path`
- `Data.@this<string>? Method` (null → `Start`)
- `Data.@this<List<Data.@this>>? Options` (positional args; convention from
  `render.cs:18`)

## Decisions made in conversation

- **No hash cache, no eviction.** Recompile every call. Keep it simple; add
  caching later if profiling demands it.
- **No pre-Roslyn shape walk.** Roslyn diagnostics are good enough; don't
  pre-empt them with bespoke validation.
- **No constructor probe phase.** Try `(Context)` ctor, fall back to `()`.
- **No typed error factory file.** Two error sites in the whole stack — inline
  `Data.@this.FromError(new ActionError(msg, code))` is enough.
- **No separate `Compiler.@this` class.** Compile is one private method on
  `App.Code.@this` partial. The Roslyn details are small enough that a whole
  class for them would be overhead.

## What I'm NOT doing

- The architect's plan (Compiler/Runtime split, hash cache, eviction race,
  ValidateShape, ProbeConstructor, error factories) — over-built for v1. If
  any of those become necessary they can land later as deltas.
- Sandboxing / reference allowlist — out of scope per the design discussion.
- Touching `code.load` semantics — it stays ICode-typed for now.

## Tests

- C# tests: `PLang.Tests/App/Code/RuntimeTests/` — Start, Invoke, args binding,
  Task/Task<T> unwrap, MethodNotFound, dispose unloads ALC.
- C# tests: `PLang.Tests/App/Code/LoadTests/` — `.cs` compile path, `.dll`
  bytes path, file-not-found, bad source.
- PLang test: `Tests/code/run-default-entry.test.goal` (Start), and
  `run-named-method-args.test.goal` (named method with args + write-to).

Goal: both suites green at the end. Baseline: 2752 C# / 199 PLang.
