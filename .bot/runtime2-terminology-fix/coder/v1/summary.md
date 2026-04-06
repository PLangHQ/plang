# Coder v1 Summary — Terminology Consistency Rename

## What this is

Aligns the codebase to consistent terminology: **Module** (grouping/namespace) and **Action** (the class that does the work). Renames the `actions/` folder to `modules/`, `IClass` interface to `IAction`, and cleans up internal variable names in Libraries.

## What was done

1. **Deleted stale `IAction.cs`** entity interface (dead code) + removed global using aliases
2. **`git mv actions/ → modules/`** — folder rename (~98 files)
3. **Namespace replace**: `App.actions` → `App.modules` across all .cs files (134 files, 174 occurrences)
4. **Renamed `IClass` → `IAction`**: interface + file + all 10 referencing files
5. **Library internals cleanup**:
   - `_handlers` → `_actions` (field name)
   - `handler` → `action` (variable names, comments)
   - Tuple `(ICodeGenerated? Handler, IError? Error)` → `(ICodeGenerated? Action, IError? Error)`
   - Error key `"HandlerError"` → `"ActionError"`
6. **Source generator**: namespace strings already updated by bulk replace
7. **Fixed stragglers**: `Path.cs` (used `actions.file.*`), `PlangModule/Program.cs` (used `App.actions.*`)
8. **Test assertions**: `"HandlerError"` → `"ActionError"` in EngineTests and LibrariesTests

## Files modified

~140 files total. Key changes:
- `PLang/App/modules/` — all files (namespace change)
- `PLang/App/modules/IAction.cs` — renamed from IClass.cs
- `PLang/App/Engine/Libraries/Library/this.cs` — internal variable cleanup
- `PLang/App/Engine/Libraries/this.cs` — tuple + variable + error key
- `PLang/App/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs` — tuple destructuring
- `PLang/App/Engine/Memory/Path.cs` — `actions.file.*` → `modules.file.*`
- `PLang/Modules/PlangModule/Program.cs` — `App.actions.*` → `App.modules.*`
- `PLang.Generators/LazyParamsGenerator.cs` — namespace strings
- `PLang/App/GlobalUsings.cs` + `PLang.Tests/GlobalUsings.cs` — removed stale IAction alias
- All test files referencing old namespace or IClass

## Test results

1423 pass, 0 fail.

## Code example

```csharp
// Before (Libraries/this.cs)
public (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(...)
{
    var handler = library.Get(module, actionName);
    if (handler is not ICodeGenerated codeGenerated)
        return (null, new ActionError(..., "HandlerError", 500));
}

// After
public (ICodeGenerated? Action, IError? Error) GetCodeGenerated(...)
{
    var action = library.Get(module, actionName);
    if (action is not ICodeGenerated codeGenerated)
        return (null, new ActionError(..., "ActionError", 500));
}
```
