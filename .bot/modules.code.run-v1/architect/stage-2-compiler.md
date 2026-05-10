# Stage 2: `App.Compiler.@this`

**Goal:** The compile-and-cache service that produces `Runtime`
instances. Privately owns source-read, hash, the Roslyn invocation,
the cache, and eviction. Adds `App.Compiler` as a property on
`App.@this` (peer of `App.Code`).

**Scope:**
- New class: `PLang/App/Compiler/this.cs`.
- New error factory: `PLang/App/Errors/CompileError.cs` with
  `NoEntryClass`, `MultipleEntryClasses`, `MustReturnTask`,
  `OverloadedMethod`, `UnsupportedConstructor`, `CompileFailed`.
- New `App.Compiler` property on `App.@this`, constructed in the App
  ctor, disposed in `App.DisposeAsync`.
- Excluded: anything user-facing. The action handler is Stage 3.

**Deliverables:**
- `PLang/App/Compiler/this.cs` — the class.
- `PLang/App/Errors/CompileError.cs` — typed error factory.
- One-line addition to `PLang/App/this.cs`:
  `public Compiler.@this Compiler { get; }` and ctor wiring.
- Disposal hook in `App.DisposeAsync`: `await Compiler.DisposeAsync()`.
- `PLang.Tests/App/Compiler/CompilerTests/` — TUnit tests for rows
  2.1 through 2.13 in [plan/test-coverage.md](plan/test-coverage.md).

**Dependencies:** Stage 1 (`Runtime.@this` and `RuntimeError`).

## Design

The shape (sketch — final code is the coder's):

```csharp
namespace App.Compiler;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.Loader;
using System.Security.Cryptography;

public sealed partial class @this : IAsyncDisposable
{
    private readonly App.@this _app;
    private readonly object _lock = new();
    private readonly Dictionary<string, Runtime.@this> _byHash = new();
    private readonly Dictionary<string, string> _pathToHash = new();   // absolute path → current hash
    private bool _disposed;

    public @this(App.@this app) { _app = app; }

    public async Task<Data.@this<Runtime.@this>> Compile(FileSystem.Path path)
    {
        // 1. Read source. The Path's Context is wired before this is reached
        //    (handler-side guarantee).
        if (!_app.FileSystem.File.Exists(path.Absolute))
            return Data.@this<Runtime.@this>.FromError(
                new ServiceError($"Script file not found: {path.Relative}"));

        var source = await _app.FileSystem.File.ReadAllTextAsync(path.Absolute);
        var hash = Sha256(source);

        // 2. Cache lookup with eviction-on-content-change.
        Runtime.@this? toEvict = null;
        lock (_lock)
        {
            if (_pathToHash.TryGetValue(path.Absolute, out var oldHash) && oldHash != hash)
            {
                _pathToHash.Remove(path.Absolute);
                if (!_pathToHash.Values.Any(h => h == oldHash))
                {
                    if (_byHash.TryGetValue(oldHash, out var stale))
                    {
                        _byHash.Remove(oldHash);
                        toEvict = stale;
                    }
                }
            }

            if (_byHash.TryGetValue(hash, out var hit))
            {
                _pathToHash[path.Absolute] = hash;
                if (toEvict is not null) _ = toEvict.DisposeAsync();
                return Data.@this<Runtime.@this>.Ok(hit);
            }
        }

        // 3. Cache miss → build (outside the lock; build is slow).
        var built = Build(source);
        if (!built.Success)
        {
            if (toEvict is not null) await toEvict.DisposeAsync();
            return built;
        }

        // 4. Insert under lock; if a race produced a duplicate, prefer the
        //    other's instance and dispose ours.
        lock (_lock)
        {
            if (_byHash.TryGetValue(hash, out var raceWinner))
            {
                _pathToHash[path.Absolute] = hash;
                _ = built.Value!.DisposeAsync();
                if (toEvict is not null) _ = toEvict.DisposeAsync();
                return Data.@this<Runtime.@this>.Ok(raceWinner);
            }
            _byHash[hash] = built.Value!;
            _pathToHash[path.Absolute] = hash;
        }

        if (toEvict is not null) await toEvict.DisposeAsync();
        return built;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Runtime.@this[] runtimes;
        lock (_lock)
        {
            runtimes = _byHash.Values.ToArray();
            _byHash.Clear();
            _pathToHash.Clear();
        }
        foreach (var r in runtimes) await r.DisposeAsync();
    }

    // --- Build pipeline ---

    private Data.@this<Runtime.@this> Build(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        // Pre-Roslyn shape checks. Cheap fail-fast with clean error messages
        // before we hand source to Roslyn diagnostics.
        var shape = ValidateShape(tree);
        if (!shape.Success) return shape.Cast<Runtime.@this>();
        var entryClassName = (string)shape.Properties["EntryClass"]!;

        // Roslyn compile.
        var compilation = CSharpCompilation.Create(
            assemblyName: $"PlangScript_{Guid.NewGuid():N}",
            syntaxTrees: new[] { tree },
            references: ReferenceSet(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        if (!emit.Success)
            return CompileError.CompileFailed(emit.Diagnostics);

        // Load into its own ALC so it can unload on eviction.
        ms.Position = 0;
        var alc = new AssemblyLoadContext(name: $"PlangScript_{entryClassName}", isCollectible: true);
        var assembly = alc.LoadFromStream(ms);
        var entry = assembly.GetType(entryClassName)!;     // ValidateShape guarantees one type

        // Probe ctor: () or (Context).
        var ctorProbe = ProbeConstructor(entry);
        if (!ctorProbe.Success)
        {
            alc.Unload();
            return ctorProbe.Cast<Runtime.@this>();
        }
        var takesContextCtor = (bool)ctorProbe.Properties["TakesContext"]!;

        return Data.@this<Runtime.@this>.Ok(
            new Runtime.@this(alc, entry, takesContextCtor));
    }

    /// <summary>
    /// Walks the parsed syntax tree to enforce the script-shape rules
    /// before handing source to Roslyn:
    ///   - exactly one public class
    ///   - all public instance methods return Task or Task&lt;T&gt;
    ///   - no method overloads
    /// </summary>
    private static Data.@this ValidateShape(SyntaxTree tree) { /* details */ }

    /// <summary>
    /// Reflects the entry type's constructors and decides whether to use
    /// the (Context) ctor or the () ctor. Returns the chosen flag, or a
    /// CompileError.UnsupportedConstructor if neither matches.
    /// </summary>
    private static Data.@this ProbeConstructor(System.Type entry) { /* details */ }

    /// <summary>
    /// References for the compile. v1: every loaded assembly in the
    /// AppDomain. Permissive — to be tightened when sandboxing is designed.
    /// </summary>
    private static IEnumerable<MetadataReference> ReferenceSet()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }

    private static string Sha256(string text)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
```

The error factory:

```csharp
namespace App.Errors;

public static class CompileError
{
    public static Data.@this NoEntryClass()
        => Data.@this.FromError(new ActionError(
            "Script must contain exactly one public class — found none",
            "NoEntryClass", 400));

    public static Data.@this MultipleEntryClasses(IEnumerable<string> classNames)
        => Data.@this.FromError(new ActionError(
            $"Script must contain exactly one public class — found: {string.Join(", ", classNames)}",
            "MultipleEntryClasses", 400));

    public static Data.@this MustReturnTask(string methodName)
        => Data.@this.FromError(new ActionError(
            $"Script method '{methodName}' must return Task or Task<T>; sync methods are not supported",
            "MustReturnTask", 400));

    public static Data.@this OverloadedMethod(string methodName)
        => Data.@this.FromError(new ActionError(
            $"Script method '{methodName}' is overloaded; one method per name only",
            "OverloadedMethod", 400));

    public static Data.@this UnsupportedConstructor(System.Type entry)
        => Data.@this.FromError(new ActionError(
            $"Script class {entry.Name} must have a () or (Context) constructor",
            "UnsupportedConstructor", 400));

    public static Data.@this CompileFailed(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                                .Select(d => d.ToString())
                                .ToList();
        return Data.@this.FromError(new ActionError(
            "Script compilation failed: " + string.Join("; ", errors),
            "CompileFailed", 400));
    }
}
```

### Why these decisions

**Why two cache maps (`_byHash` and `_pathToHash`) instead of one
keyed by path.** Two reasons. First: content-addressed cache lets
two paths with identical content share the same `Runtime` (cheap and
correct). Second: the path-to-hash side-table is what makes eviction
*work* — when the file at `mycode.cs` changes from hash A to hash B,
we need to know what the *previous* hash was so we can drop A's
runtime. A single path-keyed cache loses that history.

**Why the build runs outside the lock.** Roslyn compilation is
slow (tens to hundreds of ms). Holding the lock during compile would
serialize concurrent calls to different files. Releasing the lock
during build introduces the race that the second `if
(_byHash.TryGetValue(hash, out var raceWinner))` check handles —
prefer the winner's runtime, dispose ours.

**Why `ValidateShape` walks the syntax tree before Roslyn compile.**
Shape errors (multiple public classes, sync method, overloaded
method) are violations of *PLang's* contract, not C#. Surfacing them
as Roslyn diagnostics would bury the message in compile output and
sometimes wouldn't surface at all (overloaded methods are valid C#).
A pre-Roslyn walk gives clean, PLang-flavored error messages.

**Why `ReferenceSet` is "every loaded assembly" in v1.** This is
permissive on purpose — it's the only way to make `var goal =
context.Goal` work without per-assembly opt-in. Tightening this is
the sandboxing pass; baking restrictions in now would force us to
loosen them later. The doc note "Out of scope: sandboxing" is the
record.

**Why `isCollectible: true` on the ALC.** Required for `Unload()` to
do anything. Without it, the assembly stays loaded for the App's
lifetime — the cache never actually evicts.

**Why `ServiceError` for file-not-found instead of `CompileError`.**
File-not-found is a filesystem error, not a compile error. `module/
add.cs` uses `ServiceError` for the same shape ("module not found:
..."). Match the sibling.

### App wiring

`PLang/App/this.cs` change:

```csharp
public Compiler.@this Compiler { get; }      // new property

// In ctor (alongside the other @this initialisations):
Compiler = new Compiler.@this(this);

// In DisposeAsync (alongside Code's disposal):
await Compiler.DisposeAsync();
```

Initialisation order: after `FileSystem` is set (Compiler doesn't
read at construction, but the back-ref is consistent with how `Code`
and friends are wired).

### Files

```
PLang/
└── App/
    ├── Compiler/
    │   └── this.cs                       NEW
    ├── Errors/
    │   └── CompileError.cs               NEW
    └── this.cs                           MODIFIED  (+ Compiler property, ctor + dispose lines)

PLang.Tests/
└── App/
    └── Compiler/
        └── CompilerTests/
            ├── CompileGreenPathTests.cs  NEW   (rows 2.1–2.4, 2.11)
            ├── ShapeRejectionTests.cs    NEW   (rows 2.5–2.9)
            ├── CompileFailedTests.cs     NEW   (row 2.10)
            ├── FileNotFoundTests.cs      NEW   (row 2.12)
            └── DisposeTests.cs           NEW   (row 2.13)
```

`Support/ScriptCompileFixture.cs` from Stage 1 may now be unnecessary
for some tests — Compiler IS the fixture for those. Keep it for
Runtime tests that want to skip Compiler.
