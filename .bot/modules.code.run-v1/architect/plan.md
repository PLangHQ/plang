# `code.run` — Plan Spine

A new PLang action: compile a C# script file at runtime and invoke a
method in it. Companion to (but architecturally separate from) the
existing `code.load`/`code.list`/`code.remove`/`code.setDefault`
provider-registry surface.

## What it is

PLang syntax:

```
- run mycode.cs                              # default entry — Start()
- run SumList %x%, %y% in mycode.cs, write to %sum%
```

The developer writes a normal C# class file. One public class, all
methods `public Task` or `public Task<T>`, no overloads. Optional
`(Context)` ctor. PLang reads the file, hands it to a compiler,
receives a runtime, asks the runtime for a method by name (or `Start`
if none named), invokes it with the supplied args, and returns the
result through the standard `%__data__%` flow.

Why this matters: PLang's stance is "everything is goals, except where
you need code." `code.run` is the seam where developers escape into C#
without leaving the goal flow.

## Why this approach

Three architectural decisions that the rest of the plan rests on:

1. **`App.Compiler.@this` is a new peer of `App.Code` on `App`.**
   `App.Code` is the named provider registry — a different lifecycle
   (registered ICode instances, snapshot-aware, type-keyed). Mixing
   script lifecycle into it failed the OBP shape check (see the
   transcript). `App.Compiler` privately owns source-read, hash,
   compile, cache, eviction.

2. **`Compiler.Compile()` returns a `Runtime.@this`, not a record.**
   The Runtime is a live object that owns its `AssemblyLoadContext`
   and entry type, exposes `Start(Context)` and
   `Invoke(method, args, Context)`, is `IAsyncDisposable`. No
   `ScriptEntry` record, no side-table indexing — the live object
   owns its state and behavior.

3. **No special return-mapping syntax.** The handler returns
   `Task<Data.@this>` like every other action. The runtime drops the
   value in `%__data__%`. Developers chain `, write to %var%` if they
   want a name. `code.run`'s output is not special.

The handler is consequently three lines:

```csharp
public async Task<Data.@this> Run()
{
    var runtime = await Context.App.Compiler.Compile(Path.Value);
    return Method?.Value is null
        ? await runtime.Start(Context)
        : await runtime.Invoke(Method.Value, Args ?? new(), Context);
}
```

## Stage index

Three stages, in dependency order. Each is small enough to review in
one sitting.

| # | File | Status | One-line summary |
|---|------|--------|------------------|
| 1 | [stage-1-runtime](stage-1-runtime.md) | pending | `App.Compiler.Runtime.@this` — wraps a compiled assembly + entry type, exposes `Start`/`Invoke`/`DisposeAsync` |
| 2 | [stage-2-compiler](stage-2-compiler.md) | pending | `App.Compiler.@this` — Roslyn parse + compile + cache; produces Runtime |
| 3 | [stage-3-code-run-action](stage-3-code-run-action.md) | pending | `code.run` action handler + builder Example |

Stages 1 and 2 are testable independently. Stage 3 is a thin
dispatcher and gets its coverage from end-to-end PLang tests.

## Deep-dive index

| File | Purpose |
|------|---------|
| [plan/transcript.md](plan/transcript.md) | Full design transcript (read this first). Captures what got rejected and why. |
| [plan/test-strategy.md](plan/test-strategy.md) | Test-designer narrative: scope, layer mapping, integration cuts. |
| [plan/test-coverage.md](plan/test-coverage.md) | Coverage matrix, failure matrix, new-surfaces inventory. |

## Cross-cutting decisions

### Path parameters use `Data.@this<FileSystem.Path>`

Not `string`. Not "just a filename." Every path-taking handler in the
codebase uses the typed `Path` so that `Absolute`/`Relative`/
`Extension`/`MimeType`/`Exists`/etc. are available without re-parsing.
Auto-wrap is wired by `[PlangType("path")]` + the static
`Path.Resolve(string, Context)`. See `file/read.cs` for the canonical
shape.

### Script file rules (enforced by `Compiler.Compile`)

- Exactly one public class. Multiple → `NoEntryClass` (with the
  ambiguity reported).
- Methods are `public Task` or `public Task<T>`. Sync method →
  `MustReturnTask`. Detected by syntax-tree analysis before the
  Roslyn compile so the diagnostic is clean.
- No method overloads. Two methods with the same name → reject at
  compile time with `OverloadedMethod`.
- Optional ctor signature: `()` or `(Context)`. Anything else →
  `UnsupportedConstructor`.

### Default entry method is `Start`

If `Method` is null, dispatch to `Start`. If the script has no
`Start`, fail with `MethodNotFound("Start")`. Don't fall back to "the
first method." Predictable.

### Lifetime and caching

- **Compiler cache:** keyed by source-text SHA-256. Path → hash map
  on the side so an updated file evicts the old runtime cleanly.
- **Runtime instance per invocation:** every `code.run` call
  constructs a fresh script class instance (with `()` or `(Context)`
  ctor as detected). Don't share instances across calls — concurrent
  goals would race.
- **AssemblyLoadContext per Runtime.** Eviction calls
  `Runtime.DisposeAsync()` which calls `_alc.Unload()`.

### Return shape

Method returns `Task` → handler returns `Data.@this.Ok(null)`. Method
returns `Task<T>` → handler returns `Data.@this.Ok(unwrapped)`. The
runtime stores whatever in `%__data__%`. No `code.run`-specific
mapping.

### Errors typed at the class that detects them

- `App.Errors.CompileError` — Compiler-detected: `CompileFailed`,
  `NoEntryClass`, `MultipleEntryClasses`, `MustReturnTask`,
  `OverloadedMethod`, `UnsupportedConstructor`.
- `App.Errors.RuntimeError` — Runtime-detected: `MethodNotFound`,
  `ArityMismatch`, `InvocationFailed` (wraps inner exception).

Each carries an error code so the PLang error-handling pipeline can
route on it.

## Out of scope (separate design pass)

- **Sandboxing.** No reference allowlist, no AppDomain isolation, no
  filesystem scope. Scripts run with the App's full trust. This is a
  security conversation that needs its own pass before `code.run`
  ships outside trusted contexts.
- **Signed-script trust model.** PLang signs goals. Whether and how
  scripts get a parallel signature/verification flow is open.
- **Reference set tuning.** v1 references everything in
  `AppDomain.CurrentDomain.GetAssemblies()`. That is the most
  permissive choice — every Roslyn doc cookbook does it this way —
  and the right knob to revisit when sandboxing is designed.

## Branch note

This branch (`modules.code.run/v1`) was carved off `runtime2-cleanup`
at commit `a9791ad5` — the last commit before the design session
that produced this plan. The cleanup branch itself stays focused on
its 27 OBP stages; `code.run` is post-cleanup feature work that lives
here.
