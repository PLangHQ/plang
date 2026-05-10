# Stage 1: `App.Compiler.Runtime.@this`

**Goal:** Define the live object that wraps a compiled assembly +
entry type and exposes `Start`/`Invoke`/`DisposeAsync`. This is the
leaf of the design — Stage 2's `Compiler` produces instances of this,
so build the consumer-facing shape first.

**Scope:**
- New class: `PLang/App/Compiler/Runtime/this.cs`.
- Owns: `AssemblyLoadContext`, the entry `System.Type`, and a
  pre-computed flag indicating whether the entry's ctor takes
  `Context`.
- Exposes: `Start(Context)`, `Invoke(string, IReadOnlyList<Data>,
  Context)`, `DisposeAsync()`.
- Owns its own typed errors: `App.Errors.RuntimeError` factory file
  next to it.
- Excluded: source reading, hashing, Roslyn, the cache. All of that
  lives in Stage 2's `Compiler`.

**Deliverables:**
- `PLang/App/Compiler/Runtime/this.cs` — the class.
- `PLang/App/Errors/RuntimeError.cs` — the typed error factory with
  `MethodNotFound`, `ArityMismatch`, `InvocationFailed`.
- `PLang.Tests/App/Compiler/RuntimeTests/` — TUnit tests for rows 1.1
  through 1.11 in [plan/test-coverage.md](plan/test-coverage.md). Tests
  construct `Runtime` directly with a hand-built assembly (Roslyn or
  pre-baked `.dll` fixture); no `Compiler` dependency yet.

**Dependencies:** none. Stage 1 ships independently.

## Design

The shape (sketch — final code is the coder's):

```csharp
namespace App.Compiler.Runtime;

public sealed partial class @this : IAsyncDisposable
{
    private readonly AssemblyLoadContext _alc;
    private readonly System.Type _entry;
    private readonly bool _takesContextCtor;
    private bool _disposed;

    internal @this(AssemblyLoadContext alc, System.Type entry, bool takesContextCtor)
    {
        _alc = alc;
        _entry = entry;
        _takesContextCtor = takesContextCtor;
    }

    public Task<Data.@this> Start(Actor.Context.@this context)
        => Invoke("Start", Array.Empty<Data.@this>(), context);

    public async Task<Data.@this> Invoke(
        string methodName,
        IReadOnlyList<Data.@this> args,
        Actor.Context.@this context)
    {
        var mi = _entry.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance);

        if (mi is null)
            return RuntimeError.MethodNotFound(methodName, _entry);

        var parameters = mi.GetParameters();
        if (parameters.Length != args.Count)
            return RuntimeError.ArityMismatch(methodName, parameters.Length, args.Count);

        // Bind positional args. Each arg coerces to its parameter's CLR type
        // via the existing Data.As<T>(context) machinery — no new conversion path.
        var bound = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var slot = args[i].As(parameters[i].ParameterType, context);
            if (!slot.Success) return slot;          // typed conversion error from Data
            bound[i] = slot.Value;
        }

        var instance = _takesContextCtor
            ? Activator.CreateInstance(_entry, context)
            : Activator.CreateInstance(_entry);

        try
        {
            var task = (Task)mi.Invoke(instance, bound)!;
            await task.ConfigureAwait(false);
            return UnwrapTaskResult(task);   // Task → Ok(null), Task<T> → Ok(value)
        }
        catch (TargetInvocationException tex)
        {
            return RuntimeError.InvocationFailed(methodName, tex.InnerException ?? tex);
        }
        catch (Exception ex)
        {
            return RuntimeError.InvocationFailed(methodName, ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _alc.Unload();
        return ValueTask.CompletedTask;
    }

    private static Data.@this UnwrapTaskResult(Task task)
    {
        // Task<T> → reflect Result; Task (non-generic) → Ok(null).
        var taskType = task.GetType();
        if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var result = taskType.GetProperty("Result")!.GetValue(task);
            return Data.@this.Ok(result);
        }
        return Data.@this.Ok();
    }
}
```

The error factory:

```csharp
namespace App.Errors;

public static class RuntimeError
{
    public static Data.@this MethodNotFound(string methodName, System.Type entry)
        => Data.@this.FromError(new ActionError(
            $"Script method '{methodName}' not found on {entry.Name}",
            "MethodNotFound", 404));

    public static Data.@this ArityMismatch(string methodName, int expected, int actual)
        => Data.@this.FromError(new ActionError(
            $"Script method '{methodName}' expects {expected} argument(s), got {actual}",
            "ArityMismatch", 400));

    public static Data.@this InvocationFailed(string methodName, Exception ex)
        => Data.@this.FromError(ActionError.FromException(ex, "InvocationFailed", 500)
            .With(detail: $"Script method '{methodName}' threw: {ex.Message}"));
}
```

(Use whatever `ActionError`/`Data.FromError` shape the rest of the
codebase uses. Match `App/modules/code/load.cs` for style — it's the
sibling that already does `Data.@this.FromError(new ActionError(...))`
in the same module.)

### Why these decisions

**Why pre-compute `_takesContextCtor` instead of probing per call.**
The probe is a reflection scan of constructors — cheap, but it's
information that doesn't change for the lifetime of the Runtime. Hoist
it once at construction. Stage 2 will compute it when building the
Runtime and pass it in.

**Why `Start(ctx)` is just `Invoke("Start", [], ctx)`.** No special
case. The default-entry behavior is "method named Start, zero args."
If the script has no `Start`, the same `MethodNotFound` error fires as
for any other missing method. Single code path, single error shape.

**Why `IReadOnlyList<Data>` not `List<Data>`.** The handler hands us
`Args ?? new()`. We don't mutate the list. Match the contract to the
need.

**Why `Activator.CreateInstance` per call.** Fresh instance per
invocation is the design (see plan.md "Lifetime and caching"). Cheap.
Don't cache instances — they'd race across concurrent goals.

**Why `_alc.Unload()` is fire-and-forget in `DisposeAsync`.**
`Unload()` marks the ALC for collection but the actual unload happens
when the GC runs. That's the BCL contract; tests must not assert
"the assembly is gone immediately after Dispose." They can assert
"`Unload` was called," which is what test 1.11 covers (use a
subclass-friendly hook or a custom ALC type if the test needs to
observe the unload signal).

### Test fixtures

Tests in `PLang.Tests/App/Compiler/RuntimeTests/` need pre-cooked
assemblies for the entry type. Two paths:

1. **Inline Roslyn at test setup.** Each test compiles its source
   string into an in-memory assembly and hands the resulting `Type`
   to `Runtime`. Heaviest, but no Compiler dependency. Use this.
2. **Pre-baked test DLLs.** Faster but couples test to build artifacts
   on disk. Skip for Stage 1.

The inline-Roslyn helper can live in `PLang.Tests/App/Compiler/Support/
ScriptCompileFixture.cs`. Stage 2 may end up reusing it; if so, lift
to shared scope then.

### Files

```
PLang/
└── App/
    ├── Compiler/
    │   └── Runtime/
    │       └── this.cs                    NEW
    └── Errors/
        └── RuntimeError.cs                NEW

PLang.Tests/
└── App/
    └── Compiler/
        └── RuntimeTests/
            ├── StartTests.cs              NEW   (rows 1.1–1.5)
            ├── InvokeTests.cs             NEW   (rows 1.6–1.10)
            ├── DisposeTests.cs            NEW   (row 1.11)
            └── Support/
                └── ScriptCompileFixture.cs NEW
```

No existing files modified. No global usings added. No registrations
added. Stage 1 is purely additive at the leaf of the dependency
graph.
