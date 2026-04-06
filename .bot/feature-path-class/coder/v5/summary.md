# v5 Summary — Handlers pass `this` to Path (OBP rule 2)

## What this is

v4 review identified that all file handlers decompose action record properties into method parameters (e.g., `Path.Delete(Recursive, IgnoreIfNotFound)`) instead of passing the action record itself (`Path.Delete(this)`). This violates OBP rule 2: "navigate, don't pass". v5 fixes this so Path methods accept the action record and navigate it internally.

## What was done

### Path.cs — Method signatures accept action records
- `Copy(Path, bool, bool)` → `Copy(actions.file.Copy action)` — navigates `action.Destination`, `action.Overwrite`, `action.IncludeSubfolders`
- `Move(Path, bool)` → `Move(actions.file.Move action)` — navigates `action.Destination`, `action.Overwrite`
- `Delete(bool, bool)` → `Delete(actions.file.Delete action)` — navigates `action.Recursive`, `action.IgnoreIfNotFound`
- `List(string, bool)` → `List(actions.file.List action)` — navigates `action.Pattern`, `action.Recursive`
- `Save(object)` → `Save(actions.file.Save action)` — navigates `action.Value`
- `Read()` and `AsFile()` — unchanged (no extra params to navigate)

**Note:** The plan called for renaming `AsFile()` → `Exists()`, but this conflicts with the existing `bool Exists` property on Path. Kept `AsFile()`.

### Handlers — all pass `this`
Each handler's `Run()` now passes `this` to Path:
- `save.cs`: `Path.Save(this)`
- `delete.cs`: `Path.Delete(this)`
- `copy.cs`: `Source.Copy(this)`
- `move.cs`: `Source.Move(this)`
- `list.cs`: `Path.List(this)`
- `exists.cs`: unchanged (`Path.AsFile()`, no params)
- `read.cs`: unchanged (`Path.Read()`, no params)

### PathTests.cs — Action records in behavior tests
All tests that called Copy/Move/Delete/List/Save now construct action records with init properties. Added `using App.actions.file;`.

### FileHandlerTests.cs — No changes needed
Handler `Run()` signatures unchanged. Internal delegation is transparent.

## Files modified
- `PLang/App/Memory/Path.cs` — method signatures
- `PLang/App/actions/file/save.cs` — `Path.Save(this)`
- `PLang/App/actions/file/delete.cs` — `Path.Delete(this)`
- `PLang/App/actions/file/copy.cs` — `Source.Copy(this)`
- `PLang/App/actions/file/move.cs` — `Source.Move(this)`
- `PLang/App/actions/file/list.cs` — `Path.List(this)`
- `PLang.Tests/App/Modules/Path/PathTests.cs` — action records in tests

## Code example

Before (v4):
```csharp
// Handler
public Task<Data> Run() => Task.FromResult(Path.Delete(Recursive, IgnoreIfNotFound));

// Path method
public Data Delete(bool recursive = false, bool ignoreIfNotFound = false)
```

After (v5):
```csharp
// Handler
public Task<Data> Run() => Task.FromResult(Path.Delete(this));

// Path method
public Data Delete(actions.file.Delete action)
{
    // Navigate the action record for what we need
    _fs.Directory.Delete(_absolutePath, action.Recursive);
    // ...
}
```

## Verification
- `dotnet build PLang/PLang.csproj` — 0 errors
- `dotnet build PLang.Tests/PLang.Tests.csproj` — 0 errors
- `dotnet run --project PLang.Tests` — 1221/1221 tests pass
