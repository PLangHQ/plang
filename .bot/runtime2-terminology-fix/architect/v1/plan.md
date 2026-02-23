# Terminology Fix — Coder Handoff

## Goal

Align the codebase to consistent terminology: **Module** (grouping) and **Action** (the class that does the work). Rename `actions/` folder to `modules/`, `IClass` to `IAction`, and clean up internal variable names.

## What's Already Done

The Action entity (`PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs`) is **already correct**:
- `Module` property with `[JsonPropertyName("module")]`
- `ActionName` property with `[JsonPropertyName("action")]`
- No `Class`/`Method` properties on the entity

## What Needs Changing

### 1. Delete stale entity IAction interface

**File:** `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/IAction.cs`

This interface has old `Class`/`Method` properties and is **never referenced** anywhere. Delete it.

Also remove the global using alias in `PLang/Runtime2/GlobalUsings.cs`:
```csharp
// DELETE THIS LINE:
global using IAction = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.IAction;
```

And in `PLang.Tests/GlobalUsings.cs`:
```csharp
// DELETE THIS LINE:
global using IAction = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.IAction;
```

### 2. Rename folder `actions/` → `modules/`

**Move:** `PLang/Runtime2/actions/` → `PLang/Runtime2/modules/`

This changes the namespace in **every file** inside that folder. There are ~98 files.

Every file changes from:
```csharp
namespace PLang.Runtime2.actions.variable;
```
to:
```csharp
namespace PLang.Runtime2.modules.variable;
```

Use `git mv PLang/Runtime2/actions PLang/Runtime2/modules` for the folder move, then find-and-replace the namespace.

### 3. Rename `IClass` → `IAction`

**File:** `PLang/Runtime2/modules/IClass.cs` → `PLang/Runtime2/modules/IAction.cs` (after folder move)

```csharp
// Before
public interface IClass
{
    EngineType Engine { get; }
    PLangContext Context { get; }
    System.Type? ParameterType { get; }
    void Initialize(EngineType engine, PLangContext context);
    Task<Data> ExecuteAsync(object? parameters);
}

// After
public interface IAction
{
    EngineType Engine { get; }
    PLangContext Context { get; }
    System.Type? ParameterType { get; }
    void Initialize(EngineType engine, PLangContext context);
    Task<Data> ExecuteAsync(object? parameters);
}
```

References to update:
- `PLang/Runtime2/Engine/Libraries/Library/this.cs` — field type `IClass` → `IAction`, method signatures
- `PLang/Runtime2/Engine/Libraries/this.cs` — method signatures
- All test files that reference `IClass` (25 files in PLang.Tests)

### 4. Library/Libraries — rename internal variables

**File:** `PLang/Runtime2/Engine/Libraries/Library/this.cs`

| Before | After |
|--------|-------|
| `_handlers` (field) | `_actions` |
| `handler` (variables/parameters) | `action` |
| Comments: "handler" | "action" |

**File:** `PLang/Runtime2/Engine/Libraries/this.cs`

| Before | After |
|--------|-------|
| `(ICodeGenerated? Handler, IError? Error)` return tuple | `(ICodeGenerated? Action, IError? Error)` |
| `handler` variables | `action` |
| Comments: "handler" | "action" |
| `"HandlerError"` error key | `"ActionError"` |

**IMPORTANT:** The return tuple field name `Handler` → `Action` is a breaking change at call sites. Search for `.Handler` on the result of `GetCodeGenerated` and update.

### 5. Source Generator — namespace reference

**File:** `PLang.Generators/LazyParamsGenerator.cs`

Three string literals reference the old namespace:
```csharp
// Line 65: attribute namespace check
"PLang.Runtime2.actions"  →  "PLang.Runtime2.modules"

// Line 71: IContext namespace check
"PLang.Runtime2.actions"  →  "PLang.Runtime2.modules"

// Line 140: generated code implements
"PLang.Runtime2.actions.ICodeGenerated"  →  "PLang.Runtime2.modules.ICodeGenerated"
```

### 6. GlobalUsings updates

**`PLang/Runtime2/GlobalUsings.cs`:**
- Delete the `IAction` line (step 1)
- No new alias needed — `IAction` lives in `PLang.Runtime2.modules` which action files already import via their own namespace

**`PLang.Tests/GlobalUsings.cs`:**
- Delete the `IAction` line (step 1)

### 7. Test files — namespace updates

25 test files reference `PLang.Runtime2.actions` namespace. Update to `PLang.Runtime2.modules`:
```csharp
// Before
using PLang.Runtime2.actions;

// After
using PLang.Runtime2.modules;
```

Some test files have inner classes implementing `IClass` — update to `IAction`.

### 8. Other references to old namespace outside actions/

These files import `PLang.Runtime2.actions` but live outside the actions folder:
- `PLang/Runtime2/Engine/this.cs` — Discover namespace string
- `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs`
- `PLang/Runtime2/Engine/Libraries/Library/this.cs`
- `PLang/Runtime2/Engine/Libraries/this.cs`
- `PLang/Runtime2/Engine/Settings/this.cs`

Search pattern: `grep -rn "PLang.Runtime2.actions" --include="*.cs"` and update all.

## Execution Order

1. Delete `IAction.cs` entity interface + remove GlobalUsing aliases
2. `git mv PLang/Runtime2/actions PLang/Runtime2/modules`
3. Find-and-replace namespace: `PLang.Runtime2.actions` → `PLang.Runtime2.modules` (all .cs files)
4. Rename `IClass.cs` → `IAction.cs`, `IClass` → `IAction` in interface and all references
5. Rename Library variables: `_handlers` → `_actions`, `handler` → `action`, tuple `Handler` → `Action`
6. Update source generator namespace strings
7. Update all test files (namespace + `IClass` → `IAction`)
8. Build and verify compilation
9. Run full test suite

## What NOT to Change

- **No .pr file changes** — the JSON fields `"module"` and `"action"` are already correct
- **No builder template changes** — already uses correct field names
- **No `BaseClass` rename** — there is no `BaseClass` in the codebase
- **No handler class name changes** (e.g., dropping `Handler` suffix) — the action classes don't have a `Handler` suffix

## Verification

```bash
# Should find ZERO references after:
grep -rn "IClass" --include="*.cs" PLang/ PLang.Tests/ PLang.Generators/
grep -rn "PLang\.Runtime2\.actions" --include="*.cs" PLang/ PLang.Tests/ PLang.Generators/
grep -rn "_handlers" --include="*.cs" PLang/Runtime2/Engine/Libraries/

# Build
dotnet build PLang/PLang.csproj
dotnet build PLang.Tests/PLang.Tests.csproj

# Tests
dotnet run --project PLang.Tests
```
