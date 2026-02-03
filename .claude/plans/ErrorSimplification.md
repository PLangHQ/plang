# Plan: Error Simplification

## Summary
Simplified PLang error handling by removing `MultipleError` and `MultipleBuildError` classes. Error chaining is now done directly via `error.ErrorChain.Add()` on any `IError`. Created PLang error display templates for formatted error output.

## Files Changed

### C# Files Modified
- `PLang/Errors/Error.cs` - Added `Add(IError)` method with duplicate detection and `IsErrorHandled` property
- `PLang/Errors/MultipleError.cs` - Deleted `MultipleError` record (kept `GroupedErrors` and `GroupedUserInputErrors`)
- `PLang/Errors/Builder/MultipleBuildError.cs` - Deleted `MultipleBuildError` record (kept `GroupedBuildErrors`)
- `PLang/Utils/ErrorHelper.cs` - Removed `GetMultipleError` and `GetMultipleBuildError` helpers, updated `ToFormat` and `GetErrorMessageFromChain` to work without `MultipleError`
- `PLang/Errors/Methods/MethodNotMatchingWithParameterError.cs` - Changed `MultipleError` to `IError` for `ParameterErrors` property
- `PLang/Runtime/Engine.cs` - Replaced `MultipleError` usages with `error.ErrorChain.Add()`, updated `FindErrorHandled` to work with `IError`
- `PLang/Events/EventRuntime.cs` - Replaced `new MultipleError(error).Add()` with `error.ErrorChain.Add()`
- `PLang/Building/Builder.cs` - Replaced `new MultipleError(error)` with `error.ErrorChain.Add()`
- `PLang/Building/GoalBuilder.cs` - Replaced `new MultipleBuildError()` with `validationError.Add()`
- `PLang/Building/StepBuilder.cs` - Replaced `GetMultipleBuildError()` with `error.ErrorChain.Add()`
- `PLang/Modules/BaseProgram.cs` - Replaced `GetMultipleError()` with `error.ErrorChain.Add()`
- `PLang/Modules/MessageModule/Program.cs` - Replaced `GetMultipleError()` with `error.ErrorChain.Add()`

### New PLang Files Created
- `system/errors/Error.goal` - PLang goal for formatting errors
- `system/errors/error.html` - Scriban template for error display

## Key Decisions

1. **Direct ErrorChain access**: Instead of creating wrapper classes (`MultipleError`), error chaining is now done via `error.ErrorChain.Add()` directly on any `IError`
2. **Keep `Error.Add()` method**: The `Add()` method with duplicate detection was kept on `Error` class for cases where that feature is needed, but direct `ErrorChain.Add()` is the standard pattern
3. **Preserve GroupedErrors**: `GroupedErrors` and `GroupedBuildErrors` remain for collecting independent errors (validation, multiple build errors) that aren't causally linked
4. **IError vs Error casting**: Since `IError` is the interface type used throughout, we use `error.ErrorChain.Add()` directly rather than casting to `Error`

## Lessons Learned

1. **Interface design matters**: The `IError.ErrorChain` property already exists on the interface, making the `MultipleError` wrapper redundant. Direct list manipulation is simpler.
2. **Casting complexity**: Initially tried to use `error.Add()` which required casting from `IError` to `Error`. User correctly pointed out that `error.ErrorChain.Add()` works on any `IError` without casting.
3. **Find all usages**: Build errors revealed additional files using `MultipleError` that weren't in the original plan (`MessageModule`, `MethodNotMatchingWithParameterError`).

## Verification
- Build: `dotnet build PLang/PLang.csproj` - 0 errors, 1560 warnings (pre-existing)
- Tests: `dotnet test PLang.Tests/PLang.Tests.csproj` - 563 passed, 0 failed

---

## Phase 2: Use Error.goal Template Instead of C# ToFormat (2026-02-03)

### Summary
Replaced error output with calls to `system/errors/Error.goal`, removing dependency on C# `ErrorHelper.ToFormat()` for top-level error display. This allows users to customize error display through PLang code.

### Files Changed

#### Moved
- `PLang/Utils/ErrorHelper.cs` -> `PLang/Errors/ErrorHelper.cs` - Changed namespace from `PLang.Utils` to `PLang.Errors`

#### Modified
- `PLang/Errors/ErrorHelper.cs` - Added `OutputError()` async method that calls Error.goal with fallback console output
- `PlangConsole/Program.cs` - Changed runtime error output from `logger.LogError(result.Error.ToFormat())` to `await ErrorHelper.OutputError(engine, result.Error)`
- `PLang/Building/Builder.cs` - Changed `ShowBuilderErrors()` to async, calls `ErrorHelper.OutputError()` for each build error

#### Using Statement Updates
The following files had their using statements updated to work with ErrorHelper's new location:
- `PLang/Errors/Error.cs` - Added `using PLang.Utils` for ReservedKeywords/MaxLength
- `PLang/Errors/ExceptionError.cs` - Added `using PLang.Utils` for ReservedKeywords/MaxLength
- `PLang/Errors/ExceptionWrapper.cs` - Added `using PLang.Utils` for ReservedKeywords/MaxLength
- `PLang/Errors/CancelledError.cs` - Added `using PLang.Utils` for ReservedKeywords/MaxLength
- `PLang/Errors/MultipleError.cs` - Added `using PLang.Utils` for ReservedKeywords/MaxLength
- `PLang/Errors/Events/RuntimeEventError.cs` - Added `using PLang.Utils` for TypeHelper
- `PLang/Errors/Events/BuilderEventError.cs` - Added `using PLang.Utils` for TypeHelper
- `PLang/Services/OutputStream/Transformers/Converters/IErrorConverter.cs` - Added `using PLang.Utils` for ToBase64

### Key Decisions
1. **Namespace move**: Moved ErrorHelper to `PLang.Errors` since it's closely tied to error handling
2. **Fallback output**: The `OutputError()` method includes comprehensive fallback console output if Error.goal fails, including exception chain and stack traces
3. **Preserve ToFormat()**: The `ToFormat()` method is still used by `error.ToString()` for string interpolation cases

### What Was NOT Changed
- **Variable string conversion** (`- write out "%!error%"`): Still uses `error.ToString()` which calls `ToFormat()`. Full async solution deferred to future.
- **EventRuntime.cs debug code**: Will be removed in a future cleanup

### Verification
- Build: `dotnet build PLang/PLang.csproj` - 0 errors, 1560 warnings
- Build: `dotnet build PlangConsole/PlangConsole.csproj` - 0 errors
- Tests: `dotnet test PLang.Tests/PLang.Tests.csproj` - 563 passed, 0 failed

---

## Phase 3: /system/ Path Resolution for Goals and Files (2026-02-03)

### Summary
Implemented `/system/` path prefix handling that allows:
1. System goals/files to be referenced via `/system/` paths
2. Developers to override system files by creating local `/system/` folders in their apps
3. Fallback to actual system folder when no local override exists

### Problem
When `OnAppError.goal` (in `system/events/Runtime/`) tried to call `Error.goal` (in `system/errors/`), the path resolution failed because:
- Relative paths didn't work across different system folders
- Absolute paths like `/errors/Error` only searched the app root, not the system folder

### Solution
Added `/system/` prefix handling to path resolution in three places:

1. **PathHelper.GetPath()** - For file paths (templates, etc.)
2. **GoalHelper.GetGoal()** - For goal resolution during build
3. **PrParser.GetGoalByAppAndGoalName()** - For goal resolution during runtime

The resolution logic:
1. When path starts with `/system/`, first check `{app_root}/system/...` (allows overrides)
2. If not found locally, check `{actual_system_folder}/...` (strip `/system/` prefix)

### Files Changed

#### C# Files Modified
- `PLang/Utils/PathHelper.cs` - Added `/system/` prefix handling in `GetPath()` method
- `PLang/Utils/GoalHelper.cs` - Added `/system/` prefix handling in `GetGoal()` method
- `PLang/Building/Parsers/PrParser.cs` - Added `/system/` prefix handling in `GetGoalByAppAndGoalName()` method

#### PLang Files Modified
- `system/events/Runtime/OnAppError.goal` - Changed to `call goal /system/errors/Error`
- `system/errors/Error.goal` - Changed to `render /system/errors/error.html`
- `system/errors/error.html` - Fixed Scriban syntax errors:
  - Changed `func render_callstack goal` to `func render_callstack(g)` (parameters need parentheses)
  - Removed invalid `object.to_string` filter (not available in Scriban)

### Key Decisions
1. **Reserved `/system/` folder**: The `/system/` path prefix is now reserved for system file/goal access
2. **Override mechanism**: Developers can override any system file by creating a matching path in their app's `system/` folder
3. **Fallback behavior**: Always checks local first, then falls back to actual system folder

### Lessons Learned
1. **Build vs Runtime path resolution**: The builder (GoalHelper) and runtime (PrParser) use different methods for goal resolution - both needed updating
2. **Scriban syntax**: Functions require parentheses for parameters: `func name(param)`, not `func name param`
3. **Scriban filters**: `object.to_string` doesn't exist - just output the variable directly or use other filters
4. **PLang build constraints**: Cannot build individual .goal files - must build from app root folder

### Verification
- System build: `plang build` in `/system` folder - successful
- Test app: `Tests/ErrorDisplay` - error.html template renders correctly
- Override test: Apps can create `/system/errors/error.html` to customize error display
