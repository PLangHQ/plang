# Stage 3: Handler one-liners + `IFile`/`DefaultFileProvider`/`[Code]`-partial death

**Goal:** Collapse the file-handler call path. Action handlers (`read.cs`, `save.cs`, `copy.cs`, `move.cs`, `delete.cs`, `exists.cs`, `list.cs`) degenerate to (near-)one-line bodies over `Path.Value!.X()`. Delete `IFile`, `DefaultFileProvider`, the `[Code] public partial IFile Files { get; }` line on each file handler, and the `app/filesystem/Default/` (or `app/types/path/Default/`) System.IO.Abstractions wrapper layer. Migrate ~50 non-action callers.

**Scope:** Handler collapse, dead-code deletion, caller migration, surface shape tests flipped. **One cohesive stage — no half-migrated intermediate state.**

**Out of scope:**
- Adding new schemes (stage 5+).
- Reshaping Verb options.
- Changing PLang program-facing semantics of file actions.

## Why one cohesive stage

Splitting "handlers degenerate" from "IFile deleted" from "callers migrated" creates intermediate states where both surfaces coexist. That's the worst of both worlds — reviewers and follow-on coders have to ask "which path should I use here?" and the rebuild surface gets touched twice.

This stage *is* the migration. Land it in one commit, two if size demands. No `[Obsolete]` shims.

## Today's call chain (to be collapsed)

```
Handler (read.cs)
  └─ Path.Authorize(verb)                              ← copy-pasted preamble across all 7 handlers
  └─ Files.Read(this)
       └─ IFile.Read    (DefaultFileProvider)          ← deleted
            └─ PLangFileSystem (System.IO.Abstractions)← deleted
                 └─ System.IO.File.ReadAllText
```

Becomes:

```
Handler (read.cs)
  └─ Path.Value!.ReadText()
       └─ FilePath.ReadText() (from stage 2)
            └─ Authorize(read)                         ← moved inside FilePath
            └─ System.IO.File.ReadAllText
```

Authorize moves *inside* the per-scheme verb impl. That's the codeanalyzer v2 #1 fix — no copy-paste preamble per handler.

### Today's handler shape (read.cs as the canonical example)

```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }
    public partial data.@this<bool> ResolveVariables { get; init; }

    [Code] public partial IFile Files { get; }                   // ← removed in this stage

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;

        var result = Files.Read(this);                           // ← becomes Path.Value!.ReadText()
        if (ResolveVariables.Value && result.Success && result.Value is string content)
        {
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
            return new data.@this(result.Name, resolved, result.Type);
        }
        return result;
    }
}
```

### Target handler shape (post-stage-3)

```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<Path> Path { get; init; }          // ← Path alias, no filesystem.path
    public partial data.@this<bool> ResolveVariables { get; init; }

    public async Task<data.@this> Run()
    {
        var read = await Path.Value!.ReadText();                 // ← Authorize is inside
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;

        if (ResolveVariables.Value && read.Value is string content)
            return new data.@this(read.Name,
                Context.Variables.Resolve(content, skipInfrastructure: true),
                read.Type);
        return read;
    }
}
```

`save`, `copy`, `move`, `delete`, `exists`, `list` go to a single line:

```csharp
public Task<data.@this> Run() => Path.Value!.WriteText(Content.Value);
public Task<data.@this> Run() => Source.Value!.CopyTo(Dest.Value!);
public Task<data.@this> Run() => Source.Value!.MoveTo(Dest.Value!);
public Task<data.@this> Run() => Path.Value!.Delete();
public Task<data.@this> Run() => Path.Value!.Exists();
public Task<data.@this> Run() => Path.Value!.List();
```

Each loses its `[Code] public partial IFile Files { get; }` line.

## Deliverables

### Handler degenerations

Each of `read.cs`, `save.cs`, `copy.cs`, `move.cs`, `delete.cs`, `exists.cs`, `list.cs`:

1. Remove `[Code] public partial IFile Files { get; }`.
2. Remove the explicit `Authorize` preamble.
3. Replace `Files.X(this)` with `Path.Value!.X(...)` (or `Source.Value!.X(Dest.Value!)` for two-Path verbs).
4. Update the property type from `data.@this<filesystem.path>` (or whatever the post-stage-1 reference is) to `data.@this<Path>` (global alias).
5. Strip any now-unused `using` directives.

`copy` and `move` take two Paths — body is `Source.Value!.CopyTo(Dest.Value!)` / `Source.Value!.MoveTo(Dest.Value!)`. The cross-scheme defaults on Path base handle file→http (degenerates to ReadBytes + WriteBytes) and same-scheme fast paths get scheme-specific overrides.

`read` keeps a few more lines for the `ResolveVariables` post-processing — that's not Authorize logic, so it stays.

### Authorize moves into FilePath verb impls

`FilePath.ReadText()` (and friends) gains the Authorize call internally:

```csharp
public override async Task<data.@this> ReadText()
{
    var auth = await Authorize(new Verb { Read = new ReadVerb() });
    if (!auth.Success || auth.Type?.ClrType.Exit() == true) return auth;
    return /* System.IO.File.ReadAllText, wrapped in data.@this */;
}
```

Same pattern for Write/Delete/etc. The Authorize partial (`this.Authorize.cs`) stays on the base `path` class — `FilePath` inherits it, just calls it from inside each verb impl.

When `HttpPath` lands in stage 5, it follows the same internal-Authorize pattern.

### Deletions

- `PLang/app/modules/file/code/IFile.cs`
- `PLang/app/modules/file/code/Default.cs` (DefaultFileProvider)
- `PLang/app/modules/file/code/` folder if it becomes empty after the above
- `PLang/app/types/path/Default/*` (the System.IO.Abstractions wrapper layer from `app/filesystem/Default/`, now under path/) — every file in that folder is reviewed; survivors fold into `FilePath`, the rest delete

### Non-action caller migration

`grep -rln "IFile\\|DefaultFileProvider\\|PLangFileSystem"` after stage 2 returns roughly the following clusters of callers (verify count when starting):

- **Builder** (`PLang/app/modules/builder/`) — uses file I/O to read goals, write .pr files. Migrate to `app.Types.Scheme.From(...).ReadText()` etc. The Builder already has access to App via its existing wiring.
- **App.Save / App.Load** — top-level App serialization. Same migration pattern.
- **http module** — there's an internal file write somewhere (download). Migrate the same way.
- **code module** — `app.Code.this.cs` referenced `IFile`. Likely a code-loading or templating concern. Migrate.
- **`PLang/app/Code/this.cs`** — referenced IFile per the inventory. Same migration.
- **Test fixtures** — any test that mocked `IFile` switches to: register a test scheme into `app.Types.Scheme`, or use a temp dir for `FilePath`. Prefer the temp-dir approach for FilePath tests (more realistic, no mock to maintain).

The signature change for non-action callers: where they used to inject `IFile`, they now either:
- accept `App app` (or `Context ctx`) and call `app.Types.Scheme.From(raw).X()`; or
- accept a `Path` directly, if the caller already knows the path at construction time (cleaner).

Prefer the second option where feasible — it's more OBP-aligned (passing the value vs. passing a service).

### Surface shape tests

Any pre-existing `FileSystemSurfaceShapeTests` (or however it's named on this branch after the merge) had assertions documenting deferred work in `filesystem-permission` v1/v2. After this stage they flip:

- "no `IFile` reference in production" — was "deferred"; now "asserted."
- "handlers contain only one-line bodies" — was "deferred"; now "asserted" (with `read.cs` excepted for the `ResolveVariables` block).
- "no `[Code]` partial IFile injection on file handlers" — new assertion.

If the test class doesn't exist (the merge removed `Tests/Permission/*`), create equivalent shape tests in `PLang.Tests/app/types/PathTests/`.

## Migration mechanics

Recommended sequence within the stage:

1. Add Authorize calls inside `FilePath`'s verb impls (already-stubbed verbs from stage 2 grow the gate internally).
2. Update each non-action caller, one cluster at a time, to use `app.Types.Scheme.From(...).X()`. After each cluster: build + test suite.
3. Update each file action handler (`read.cs`, etc.). After: build + file-handler test suite (`Tests/app/modules/file/`).
4. Delete `IFile.cs` and `Default.cs`. Compiler immediately flags any remaining caller.
5. Delete `app/types/path/Default/*` files that aren't needed. Compiler flags any remaining caller.
6. Run full PLang `--test` and C# test suites. Clean rebuild before claiming.

Don't reverse the order — deletions last. Deleting first creates a sea of build errors that obscures missed callers.

## Tests

See `plan-test-designer.md` Stage 3. Key surfaces:

- Survey: no production reference to `IFile`, `DefaultFileProvider`, `PLangFileSystem`, or any other deleted symbol.
- Survey: no `[Code]`-decorated partial typed `IFile` anywhere.
- File-handler PLang `--test` fixtures stay green (PLang-side behavior unchanged).
- C# unit tests for handler one-liner shape — handler bodies match the `Path.Value!.X()` pattern.
- Cross-scheme `CopyTo` lands here in skeleton (FilePath → FilePath); fuller cross-scheme tests come in stage 7.

## Risk

Highest-risk stage in the branch. Volume is substantial (~50 callers), mechanical but easy to miss one. The compiler is the safety net once `IFile.cs` is deleted — but only if the deletion happens last. The `Tests/app/modules/file/` suite is the behavioral safety net for the handlers themselves.

If a caller cluster is so entangled with `PLangFileSystem` that migration would explode this stage, **flag and split**: do the handlers + IFile death first, leave that one cluster behind a temporary adapter, and clean up in a follow-up commit on the same branch. But verify with the architect before splitting — the "one cohesive stage" rule exists for a reason.
