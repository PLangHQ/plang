# coder summary — modules.code.run/v1

**Version:** v1

## What this is

A new PLang action: `- run mycode.cs` compiles a C# file at runtime, loads it
into a collectible AssemblyLoadContext, instantiates the entry class, and
invokes its `Start(Data data)` method. The result flows back through the
standard `%__data__%` mechanism.

The architect's plan (Compiler/Runtime split, hash cache, eviction races,
pre-Roslyn shape validation, separate error-factory files) was set aside
during the live design session. Ingi sketched something much smaller; this
version implements that.

## What was done

### Production files (under `PLang/App/`)

- `Code/PluginLoadContext.cs` — collectible ALC, fallback to default load.
- `Code/Compiled/this.cs` — wraps the compiled byte[] and the assembly name.
  Owns `Load() → Data<Runtime.@this>`. OBP: the compiled thing knows how to
  load itself.
- `Code/Runtime/this.cs` — wraps the loaded assembly + entry type. Exposes
  `Start(Data data, Context)` (auto-detects `Start()` vs `Start(Data)`),
  `Invoke(method, args, Context)`, `IAsyncDisposable` (unloads ALC).
- `Code/Runner/this.cs` — path-aware facade: `new Runner(Data<Path>).Start(data)`
  loads the file at the path and dispatches. One responsibility, one method.
- `Code/this.Load.cs` — partial on `App.Code.@this`. `Load(Data<Path>)`
  dispatches `.cs` (compile via Roslyn) vs `.dll` (raw bytes), produces a
  `Compiled` and calls `.Load()` on it.
- `modules/code/run.cs` — the action handler. Two real lines:
  ```csharp
  var code = new Code.Runner.@this(Path);
  return await code.Start(Data ?? Data.Ok(null));
  ```
- `FileSystem/Path.cs` — two changes:
  - Constructor now accepts relative paths; resolves against the goal's
    runtime directory when `Context` is supplied. `new Path("notes.md", ctx)`
    works; the `Resolve` static is a one-liner.
  - New `Path.GetContent()` — Path owns reading itself. Routes through the
    registered `IFile` provider; memoises into `Path.Content`. Scripts say
    `await path.GetContent()` — no IFile lookup, no action-record wiring.

### Code example

The whole pipeline, from PLang to script:

```plang
/ Tests/Code/Hello.test.goal
Start
- [code] run scripts/hello.cs, write to %msg%
- assert %msg% equals "hello plang world"
```

```csharp
// Tests/Code/scripts/hello.cs
public class hello {
    public async System.Threading.Tasks.Task<string> Start() {
        await System.Threading.Tasks.Task.Yield();
        return "hello plang world";
    }
}
```

The builder maps `[code] run scripts/hello.cs` → `code.run Path([path] scripts/hello.cs)`.
Handler resolves the path (relative → absolute via the new ctor), Runner
loads and compiles, Runtime.Start invokes the script's entry, returns the
string. `%__data__%` carries the value to `variable.set`, assert passes.

### Tests

- C# (TUnit, `PLang.Tests/App/Code/CodeRunTests.cs`): 10 tests, all pass —
  Start no-args, Start with Data, Invoke named method + positional args,
  Task non-generic unwrap, MethodNotFound, ArityMismatch, FileNotFound,
  CompileFailed, double-load independence, Path.GetContent.
- PLang (`Tests/Code/`): `Hello.test.goal` + `RunDefault.test.goal`, both
  pass. Built `.pr` files committed.

**Both suites green:**

- C# 2762/2762 (baseline 2752 + 10 new)
- PLang 201/201 (baseline 199 + 2 new)

## What's NOT in this version

- **Named-method dispatch from the action handler.** The plang surface
  collapsed to a single `Start(Data)` contract per script. Variation lives
  inside the script. `Runtime.Invoke(name, args, ctx)` is still public —
  only the handler currently doesn't expose Method/Options properties.
- **Hash cache / eviction.** Every `code.run` recompiles. Cheap to add as a
  delta if profiling demands it.
- **Sandboxing, reference allowlist, signed-script trust.** Out of scope per
  the architect's plan — separate design pass.
- **Programmatic facades for other modules** (`app.File.Save`,
  `app.Llm.Query`, etc.). The pattern Path.GetContent established —
  domain-object-owns-its-behavior, routes through the registered provider —
  applies module by module. Not done in this version.

## Notes for future bots

- Don't decompose `Data<Path>` at the caller — thread the envelope through;
  unwrap with `.Value` only at the work site.
- For `Task` vs `Task<T>` unwrap, use `MethodInfo.ReturnType`, not
  `task.GetType()` — async state machines run as `Task<VoidTaskResult>`
  internally, which fools the runtime-type check.
- Path constructor self-resolves relative paths when Context is supplied.
  Boot-time utilities (CLI parser, source generators) can still construct
  Paths without Context — they just get the raw string until Context arrives.
