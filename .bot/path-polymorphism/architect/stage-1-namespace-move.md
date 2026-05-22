# Stage 1: Namespace move — `app.filesystem/` → `app.types/path/` + `path` to `@this` convention

**Goal:** Rename the entire `app.filesystem` namespace and its contents under `app.types/path/`. Convert `path` from class-named-after-namespace (today: `app.filesystem.path`) to the `@this` convention (target: `app.types.path.@this`). Zero behavioral change. Big blast radius.

**Scope:** File moves, namespace changes, class rename (`class path` → `class @this`), partial-file renames, using-directive updates, global alias additions. Tests stay green unchanged.

**Out of scope:**
- Making `path` abstract (stage 2).
- Adding the Scheme registry (stage 2).
- Deleting `IFile`/`DefaultFileProvider`/the `[Code]` partial mechanism (stage 3).

## Why this is its own stage

It's a rename sweep. Doing it cleanly first means every subsequent stage references the final namespace + `@this` shape. Mixing the rename with logic changes would obscure both.

## Deliverables

### Folder moves

| From | To |
|------|----|
| `PLang/app/filesystem/path.cs` | `PLang/app/types/path/this.cs` |
| `PLang/app/filesystem/path.Operations.cs` | `PLang/app/types/path/this.Operations.cs` |
| `PLang/app/filesystem/path.Authorize.cs` | `PLang/app/types/path/this.Authorize.cs` |
| `PLang/app/filesystem/permission/this.cs` | `PLang/app/types/path/permission/this.cs` |
| `PLang/app/filesystem/permission/verb/this.cs` | `PLang/app/types/path/permission/verb/this.cs` |
| `PLang/app/filesystem/permission/verb/Read.cs` | `PLang/app/types/path/permission/verb/Read.cs` |
| `PLang/app/filesystem/permission/verb/Write.cs` | `PLang/app/types/path/permission/verb/Write.cs` |
| `PLang/app/filesystem/permission/verb/Delete.cs` | `PLang/app/types/path/permission/verb/Delete.cs` |
| `PLang/app/filesystem/IPLangFileSystem.cs` | `PLang/app/types/path/IPLangFileSystem.cs` (deleted in stage 3 — kept here only for the rename pass) |
| `PLang/app/filesystem/Default/*` | `PLang/app/types/path/Default/*` (also deleted/refactored in stages 2–3; lowercase the folder if the surrounding `path/` tree is lowercase — `default/` — see "Casing nit" below) |

### `path` → `@this` conversion

This is the substantive change in the stage. Three coordinated edits:

1. **File rename:** `path.cs` → `this.cs` (and the two partials follow: `path.Operations.cs` → `this.Operations.cs`, `path.Authorize.cs` → `this.Authorize.cs`).
2. **Class rename inside each file:** `public partial class path : modules.IContext` → `public partial class @this : modules.IContext`. (Stage 2 will further mark it `abstract`. Stage 1 keeps it concrete to preserve behavior.)
3. **Add global alias:** in `PLang/app/GlobalUsings.cs`, under the existing "// FileSystem types" stub (currently empty), add:
   ```csharp
   global using Path = app.types.path.@this;
   ```
   Subsequent stages add `FilePath`, `HttpPath` aliases here.

### Namespace changes

- `namespace app.filesystem` → `namespace app.types.path`
- `namespace app.filesystem.permission` → `namespace app.types.path.permission`
- `namespace app.filesystem.permission.verb` → `namespace app.types.path.permission.verb`
- `namespace app.filesystem.Default` (or `default` if lowercased) → `namespace app.types.path.Default` (or `default`)

### Using-directive sweeps

Every file currently containing `using app.filesystem;` (or a sub-namespace) updates to `using app.types.path;` (or matching sub). The handler files have a particular pattern with explicit aliases:

```csharp
// Before
using Verb = global::app.filesystem.permission.verb.@this;
using ReadVerb = global::app.filesystem.permission.verb.Read;

// After
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;
```

Also the property type reference:

```csharp
// Before
public partial data.@this<filesystem.path> Path { get; init; }

// After (with the global Path alias)
public partial data.@this<Path> Path { get; init; }
```

Final count to verify: `git grep -E "app\\.filesystem"` returns zero hits after the sweep.

### Global aliases

`PLang/app/GlobalUsings.cs` has a "// FileSystem types" comment stub today with nothing under it. Fill it in:

```csharp
// FileSystem types  →  rename comment to "Path types"
global using Path = app.types.path.@this;
```

If `PLang.Tests/GlobalUsings.cs` aliases anything from `app.filesystem`, update those too. Per `CLAUDE.md` "Test alias clash" — the alias-to-`@this` pattern continues to work the same way.

### Casing nit

The surrounding tree under `PLang/app/filesystem/` mixes case (`Default/` capitalized, `permission/` lowercase). Pick a convention and apply it consistently inside `PLang/app/types/path/`. Recommend lowercase throughout (`default/` if the folder survives) for consistency with the rest of `app/types/`. Confirm with neighbours under `app/` before committing — whatever pattern the runtime2 merge established for sub-folder casing wins.

### What stays put

- `PLang/app/types/` itself (existing folder with `this.cs`, `Registry.cs`, `Conversion.cs`, `choices/`) — unchanged in this stage. `path/` lands as a sibling of `choices/` underneath.
- `PLang/app/modules/file/` — file action handlers stay where they are. Their internal namespace doesn't change. They reference Path types via the new global alias.

## Tests

See `plan-test-designer.md` Stage 1. No new behavioral tests. Pass condition:

1. `dotnet build PlangConsole` clean.
2. `dotnet run --project PLang.Tests` all green.
3. `git grep -E "app\\.filesystem"` returns nothing.
4. Survey assertion (one new test, easy to retire later): the namespace `app.filesystem` contains zero loaded types in the App assembly.
5. Survey assertion: `app.types.path.@this` is reachable via the global `Path` alias from any test file.

## Risk

Low per-file (mechanical), moderate by volume. The risk is missing a reference and shipping a broken build — guard with the grep assertion and a clean-build before commit. The `class path` → `class @this` rename is the one place where a missed reference would produce a confusing error (rather than a "namespace not found" error) — verify by also grepping for `\\bpath\\b` in `app/types/path/` after the rename: outside of partials and tests, you shouldn't find `class path` or `new path(` anywhere.
