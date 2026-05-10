# `code.run` — Test Coverage

The reference test-designer reads top-to-bottom. One row per behavior this branch commits to. Test-designer writes one test per row.

Layers: **C#** = `PLang.Tests` TUnit suite. **goal** = PLang `Tests/**/*.test.goal`. **integ** = end-to-end goal test exercising the full `code.run` path with a real `.cs` file on disk.

Sense: **green** = positive path. **negative** = the failure shape it asserts.

---

## Coverage matrix

### Stage 1 — `App.Compiler.Runtime.@this`

| # | Behavior | Layer | Sense |
|---|----------|-------|-------|
| 1.1 | `Start(Context)` instantiates entry class with `()` ctor and dispatches to `Start` | C# | green |
| 1.2 | `Start(Context)` uses `(Context)` ctor when entry class has one | C# | green |
| 1.3 | `Start(Context)` returns `Data.Ok(null)` for `Task` (void) entry | C# | green |
| 1.4 | `Start(Context)` returns `Data.Ok(value)` for `Task<T>` entry | C# | green |
| 1.5 | `Start(Context)` raises `MethodNotFound("Start")` when no `Start` method | C# | negative |
| 1.6 | `Invoke("Foo", [], ctx)` dispatches to method `Foo` and returns its result | C# | green |
| 1.7 | `Invoke` binds positional args by parameter order, coercing per slot via `Data.As<T>` | C# | green |
| 1.8 | `Invoke` with unknown method name raises `MethodNotFound(name)` | C# | negative |
| 1.9 | `Invoke` with mismatched arg count raises `ArityMismatch` | C# | negative |
| 1.10 | `Invoke` wraps inner-method exceptions as `InvocationFailed` (preserves message) | C# | negative |
| 1.11 | `DisposeAsync` calls `_alc.Unload()` and is idempotent | C# | green |

### Stage 2 — `App.Compiler.@this`

| # | Behavior | Layer | Sense |
|---|----------|-------|-------|
| 2.1 | First `Compile(path)` produces a working `Runtime` (smoke) | C# | green |
| 2.2 | Second `Compile(path)` with unchanged content returns the cached `Runtime` (same instance) | C# | green |
| 2.3 | `Compile(path)` after content change recompiles, evicts old, returns new `Runtime` | C# | green |
| 2.4 | `Compile(path)` for two paths with identical content shares a single `Runtime` (content-addressed cache) | C# | green |
| 2.5 | `Compile` rejects sync methods at syntax-tree analysis with `MustReturnTask` (no Roslyn round-trip) | C# | negative |
| 2.6 | `Compile` rejects multiple public classes with `MultipleEntryClasses` | C# | negative |
| 2.7 | `Compile` rejects no public class with `NoEntryClass` | C# | negative |
| 2.8 | `Compile` rejects two methods with the same name as `OverloadedMethod` | C# | negative |
| 2.9 | `Compile` rejects unsupported ctors as `UnsupportedConstructor` | C# | negative |
| 2.10 | `Compile` returns `CompileFailed` with Roslyn diagnostics when source has a syntax/semantic error | C# | negative |
| 2.11 | Reference set includes `App`, `App.Variables`, `App.FileSystem` so scripts can `var goal = context.Goal` | C# | green |
| 2.12 | `Compile` against a missing file returns a packaged file-not-found error (not an unhandled exception) | C# | negative |
| 2.13 | `DisposeAsync` on `Compiler` disposes every cached `Runtime` | C# | green |

### Stage 3 — `code.run` action handler

| # | Behavior | Layer | Sense |
|---|----------|-------|-------|
| 3.1 | Handler missing `Path` raises `MissingRequiredParameter` (generator-emitted guard) | C# | negative |
| 3.2 | Handler with `Method` null dispatches to `Start` via `Runtime.Start` | C# | green |
| 3.3 | Handler with `Method` set dispatches via `Runtime.Invoke` with the supplied positional args | C# | green |
| 3.4 | Handler reports back `CompileError`/`RuntimeError` shapes verbatim — no double-wrapping | C# | negative |

### Integration cuts (end-to-end .goal)

| # | Behavior | Layer | Sense |
|---|----------|-------|-------|
| I.A | `- run mycode.cs, write to %answer%` invokes `Start` and `%answer%` carries the returned value | integ | green |
| I.B | `- run SumList %x%, %y% in mycode.cs, write to %sum%` binds positional args, coerces, returns | integ | green |
| I.C | After overwriting `mycode.cs`, the next `- run` observes the new behavior | integ | green |

### Developer-facing failure surfaces (PLang)

| # | Behavior | Layer | Sense |
|---|----------|-------|-------|
| 4.1 | `- run nonexistent.cs` surfaces a typed file-not-found error | goal | negative |
| 4.2 | `- run BogusMethod in mycode.cs` surfaces `MethodNotFound` | goal | negative |
| 4.3 | `- run SumList %x% in mycode.cs` (one arg, two expected) surfaces `ArityMismatch` | goal | negative |
| 4.4 | `- run mycode.cs` when `mycode.cs` has no `Start` surfaces `MethodNotFound("Start")` | goal | negative |
| 4.5 | `- run mycode.cs` when `mycode.cs` has a sync method surfaces `MustReturnTask` at compile | goal | negative |

---

## Failure matrix

Consolidated negative paths. Each row is a failure mode the system *should* fail on; the test asserts the failure is hard, typed, and at the right layer.

| Failure mode | Detected by | Error code | Layer |
|--------------|-------------|------------|-------|
| Source file missing | Compiler (file-read boundary) | `FileNotFound` (existing FS error) | C# |
| Source has no public class | Compiler (syntax-tree walk) | `CompileError.NoEntryClass` | C# |
| Source has multiple public classes | Compiler (syntax-tree walk) | `CompileError.MultipleEntryClasses` | C# |
| Method has sync return | Compiler (syntax-tree walk) | `CompileError.MustReturnTask` | C# |
| Two methods share a name | Compiler (syntax-tree walk) | `CompileError.OverloadedMethod` | C# |
| Class has unsupported ctor | Compiler (post-load probe) | `CompileError.UnsupportedConstructor` | C# |
| Roslyn syntax/semantic error | Compiler (Roslyn diagnostics) | `CompileError.CompileFailed` (diagnostics in detail) | C# |
| `Method` parameter names a method that doesn't exist | Runtime | `RuntimeError.MethodNotFound` | C# / goal |
| `Args` count mismatches method arity | Runtime | `RuntimeError.ArityMismatch` | C# / goal |
| Method throws while running | Runtime | `RuntimeError.InvocationFailed` (inner exception in detail) | C# |
| Handler invoked without `Path` | Generator-emitted pre-`Run` guard | `MissingRequiredParameter` | C# |

Negative paths NOT in this matrix because they're impossible by design:

- "Method has wrong arg types" — args are `List<Data>?`; coercion via `Data.As<T>(Context)` succeeds or returns its own typed `ConversionError`. That error path is owned by `Data.As<T>`, not by `Runtime`.
- "Two `Start` methods" — covered by `OverloadedMethod`. No separate test.
- "Race between two concurrent `code.run` calls on the same path" — Compiler holds an internal lock around cache mutation; testing the lock is testing implementation, not contract. Skip.
- "Script class is in a non-default namespace" — irrelevant; the Compiler's class search is namespace-blind, takes the only public class. If we add namespace constraints later, then test.

---

## New surfaces this branch introduces

What test-designer uses to name tests against without spelunking.

### Interfaces and types

| Path | Signature / shape |
|------|-------------------|
| `PLang/App/Compiler/this.cs` | `public sealed partial class @this : IAsyncDisposable` |
| `PLang/App/Compiler/Runtime/this.cs` | `public sealed partial class @this : IAsyncDisposable` |
| `PLang/App/Errors/CompileError.cs` | typed error factory: `NoEntryClass`, `MultipleEntryClasses`, `MustReturnTask`, `OverloadedMethod`, `UnsupportedConstructor`, `CompileFailed` |
| `PLang/App/Errors/RuntimeError.cs` | typed error factory: `MethodNotFound`, `ArityMismatch`, `InvocationFailed` |

### New methods on `App.Compiler.@this`

| Signature | Purpose |
|-----------|---------|
| `Task<Data.@this<Runtime.@this>> Compile(FileSystem.Path path)` | Read source, hash, compile-or-cache, return Runtime. |
| `ValueTask DisposeAsync()` | Dispose every cached Runtime. |

### New methods on `App.Compiler.Runtime.@this`

| Signature | Purpose |
|-----------|---------|
| `Task<Data.@this> Start(Context context)` | Default-entry dispatch. Equivalent to `Invoke("Start", [], context)`. |
| `Task<Data.@this> Invoke(string method, IReadOnlyList<Data> args, Context context)` | Resolve method by name, bind positional args (with coercion via `Data.As<T>(Context)`), invoke, await, wrap result. |
| `ValueTask DisposeAsync()` | Unload the AssemblyLoadContext. |

### New PLang actions

| Action | Module file | Pattern |
|--------|-------------|---------|
| `code.run` | `PLang/App/modules/code/run.cs` | `[Action("run", Cacheable = false)]` partial class `run : IContext` with `Path` (Data&lt;Path&gt;), optional `Method` (Data&lt;string&gt;), optional `Args` (List&lt;Data&gt;) |

### New registrations

| Surface | Where | Why |
|---------|-------|-----|
| `App.Compiler` property on `App.@this` | `PLang/App/this.cs` | Same shape as `App.Code`, `App.Tester`, `App.Builder`. Constructor-initialized; disposed in `DisposeAsync`. |
| Module registration for `code.run` | Source generator pickup | Standard `[Action]`-based discovery — no manual registration. |

### Existing surfaces this branch touches by reference

| Surface | What we use |
|---------|-------------|
| `App.FileSystem.Path` (`PLang/App/FileSystem/Path.cs`) | Handler parameter type. Auto-wrapped via `Path.Resolve`. |
| `Data.As<T>(Context)` (`PLang/App/Data/...`) | Per-slot arg coercion in `Runtime.Invoke`. |
| `Microsoft.CodeAnalysis.CSharp.Scripting` (NuGet, already in `PLang.csproj`) | Roslyn entry point for parsing and emitting. No new package reference. |
| `Assembly.LoadFrom` / `AssemblyLoadContext` (BCL) | Same loading path `module/add.cs` already uses. |

### Existing surfaces this branch does NOT touch

- `App.Code.@this` and the provider registry — completely untouched. No new entries, no filter relaxation, no API additions.
- `App.modules.code.{load, list, remove, setDefault}` — unchanged.
- `App.modules.module.add` — unchanged. (The two systems are intentionally parallel: `module.add` registers a DLL of *modules* into the action catalog; `code.run` compiles a single class file and invokes a method by name. Different verbs, different shapes.)
