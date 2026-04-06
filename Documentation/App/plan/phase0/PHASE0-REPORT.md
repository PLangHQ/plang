# Phase 0: Foundation Audit — Report

**Date**: 2026-02-12
**Branch**: runtime2
**Test baseline**: 1078 tests (before) → 1130 tests (after) — all passing
**Build**: 0 errors, 0 new warnings

**Run all tests**: `dotnet run --project PLang.Tests`

---

## Phase 0.1 — Rename `Data.Fail()` → `Data.FromError()`

### What changed
The static factory method `Data.Fail(IError)` was renamed to `Data.FromError(IError)` across the entire codebase.

**Plan deviation**: The plan called for `Data.Error()`, but C# does not allow a static method and instance property with the same name on the same type. `Data` already has `public IError? Error { get; set; }`, so `Data.Error(...)` causes CS0102. User chose `Data.FromError()` which follows the existing factory pattern (`Type.FromName()`, `Type.FromMime()`).

### Scope
- **Method definitions**: `PLang/App/Memory/Data.cs:241`, `PLang/App/Memory/Data.cs:376`
- **Call sites renamed**: 58 in `PLang/App/` (40 files), 10 in `PLang.Tests/` (5 files), 1 in `PLang/Executor.cs`, 4 in `PLang.Generators/LazyParamsGenerator.cs`
- **Remaining `Data.Fail(` calls**: **0** (verified via grep)

### Proof — Executable tests

Each proof below is a test you can run. All are in `PLang.Tests/App/Phase0Proof.cs`.

**1. `Data.FromError()` creates an error result**
> Test: `Phase0Proof.Phase01_DataFromError_CreatesErrorResult` (`Phase0Proof.cs:21`)
```
IN:  Data.FromError(new Error("File not found", "NotFound", 404))
OUT: .Success=false, .Error.Message="File not found", .Error.StatusCode=404
```

**2. `Data.Ok()` still works**
> Test: `Phase0Proof.Phase01_DataOk_StillWorks` (`Phase0Proof.cs:35`)
```
IN:  Data.Ok("hello world")
OUT: .Success=true, .Error=null, .Value="hello world"
```

**3. Generic `Data<T>.FromError()` works**
> Test: `Phase0Proof.Phase01_GenericDataFromError` (`Phase0Proof.cs:47`)
```
IN:  Data<string>.FromError(new Error("oops"))
OUT: .Success=false, .Error.Message="oops"
```

**Additional coverage** (pre-existing tests that now use `FromError`):
- `ReturnTests.Fail_WithError_ReturnsError` (`PLang.Tests/App/Core/ReturnTests.cs:40`)
- `ReturnTests.Fail_WithErrorMessage_ReturnsErrorWithDefaultKey` (`ReturnTests.cs:53`)
- `ReturnTests.Fail_WithMessageAndKeyAndStatusCode_ReturnsCustomError` (`ReturnTests.cs:65`)
- `DataGenericTests.Fail_CreatesErrorResult` (`PLang.Tests/App/Memory/DataGenericTests.cs:48`)

---

## Phase 0.2 — Error Categories (Application vs Runtime)

### What changed
1. New enum `ErrorCategory`: `Application` | `Runtime` — `PLang/App/Errors/ErrorCategory.cs`
2. New property `ErrorCategory Category` on `IError` — `PLang/App/Errors/IError.cs:12`
3. Default in `Error` base: `StatusCode < 500 ? Application : Runtime` — `PLang/App/Errors/Error.cs:23`
4. Overrides in subclasses; category-aware `Format()` in `Error.cs`

### Category mapping

| Error Type | Category | Rule |
|-----------|----------|------|
| `Error` (base) | `StatusCode < 500 ? Application : Runtime` | Convention |
| `ValidationError` | **Always Application** | Override |
| `AssertionError` | **Always Application** | Override |
| `ActionError` | Inherits from base | StatusCode-based |
| `StepError` | **Always Runtime** | Override |
| `GoalError` | **Always Runtime** | Override |
| `ServiceError` | **Always Runtime** | Override |

### Proof — Category classification

All tests in `PLang.Tests/App/Errors/ErrorCategoryTests.cs` and `PLang.Tests/App/Phase0Proof.cs`.

**1. Base Error: 400 → Application**
> Test: `ErrorCategoryTests.Error_4xx_IsApplication` (`ErrorCategoryTests.cs:8`)
```
IN:  new Error("bad request", "BadRequest", 400).Category
OUT: ErrorCategory.Application
```

**2. Base Error: 500 → Runtime**
> Test: `ErrorCategoryTests.Error_5xx_IsRuntime` (`ErrorCategoryTests.cs:22`)
```
IN:  new Error("internal error", "InternalError", 500).Category
OUT: ErrorCategory.Runtime
```

**3. ValidationError overrides: even with 500, still Application**
> Test: `ErrorCategoryTests.ValidationError_AlwaysApplication` (`ErrorCategoryTests.cs:36`)
> Test: `Phase0Proof.Phase02_ValidationError_AlwaysApplication_Even500` (`Phase0Proof.cs:82`)
```
IN:  new ValidationError("invalid input", "Validation", 500).Category
OUT: ErrorCategory.Application
```

**4. GoalError overrides: even with 400, still Runtime**
> Test: `ErrorCategoryTests.GoalError_AlwaysRuntime` (`ErrorCategoryTests.cs:57`)
> Test: `Phase0Proof.Phase02_GoalError_AlwaysRuntime_Even400` (`Phase0Proof.cs:92`)
```
IN:  new GoalError("goal not found", "NotFound", 400).Category
OUT: ErrorCategory.Runtime
```

**5. AssertionError → Application**
> Test: `ErrorCategoryTests.AssertionError_AlwaysApplication` (`ErrorCategoryTests.cs:43`)

**6. StepError → Runtime**
> Test: `ErrorCategoryTests.StepError_AlwaysRuntime` (`ErrorCategoryTests.cs:50`)

**7. ServiceError → Runtime**
> Test: `ErrorCategoryTests.ServiceError_AlwaysRuntime` (`ErrorCategoryTests.cs:64`)

**8. ActionError inherits base: 404 → Application, 500 → Runtime**
> Test: `ErrorCategoryTests.ActionError_InheritsBaseLogic_4xx_IsApplication` (`ErrorCategoryTests.cs:71`)
> Test: `ErrorCategoryTests.ActionError_InheritsBaseLogic_5xx_IsRuntime` (`ErrorCategoryTests.cs:78`)

### Proof — Format output

**9. Application errors → concise format (no ======, no call stack)**
> Test: `ErrorCategoryTests.ApplicationError_FormatIsConcise` (`ErrorCategoryTests.cs:85`)
> Test: `Phase0Proof.Phase02_ApplicationFormat_IsConcise` (`Phase0Proof.cs:102`)
```
IN:  ValidationError("Email address is required") at step "validate %email% is not empty" in Start.goal:5
OUT: Contains "Error: Email address is required"
     Contains "Step:" and "validate %email% is not empty"
     Does NOT contain "================="
     Does NOT contain "Call stack"
     Does NOT contain "C# Developers"
```

**10. Runtime errors → full detail with ====== and exception info**
> Test: `ErrorCategoryTests.RuntimeError_FormatHasFullDetail` (`ErrorCategoryTests.cs:96`)
> Test: `Phase0Proof.Phase02_RuntimeFormat_HasFullDetail` (`Phase0Proof.cs:122`)
```
IN:  Error("Failed to read file", status=500, exception=InvalidOperationException("Access denied"))
OUT: Contains "=================="
     Contains "FileError(500)"
     Contains "Failed to read file"
     Contains "InvalidOperationException: Access denied"
```

---

## Phase 0.3 — `/system/error/` Layer

### What changed
Created PLang goals and HTML templates for structured error display. Updated event goals to delegate.

### New files
| File | Purpose |
|------|---------|
| `system/error/ConsoleError.goal` | Routes by `%!error.Category%` — concise for Application, full for Runtime |
| `system/error/error.html` | Generic HTML error template (Scriban, for future web server) |
| `system/error/500.html` | Detailed HTML error template with stack trace (future web server) |

### Modified files — Before/After

**`system/events/Runtime/OnAppError.goal`:**
```
BEFORE: - write out system error "%!error%"
AFTER:  - call /system/error/ConsoleError
```
Same change for `OnStepError.goal` and `OnGoalError.goal`.

### ConsoleError.goal
```plang
ConsoleError
/ Formats %!error% for console display
/ Application errors get concise output; Runtime errors get full detail
- if %!error.Category% = "Application" then
    - write out "Error: %!error.Message%"
- if %!error.Category% = "Runtime" then
    - write out "%!error.Format%"
```

### Proof
No C# tests for this phase — these are PLang goal files and static HTML templates. Verification:
- Files exist at the paths listed above
- No C# changes → build still passes
- Error goals will be exercised when the PLang builder is rebuilt and integration tests run

---

## Phase 0.4 — Type Preservation Gaps

### What changed
9 list module handlers were returning `Data.Ok(value)` without explicit PLang type. Auto-inference gave the CLR type name of the `types.list` wrapper record instead of `"list"`. Now all pass `Type.FromName("list")`.

### Fixed handlers (all in `PLang/App/modules/list/`)

| Handler | Before | After |
|---------|--------|-------|
| `add.cs` | `Data.Ok(new types.list{...})` | `Data.Ok(new types.list{...}, Type.FromName("list"))` |
| `flatten.cs` | same | same fix |
| `range.cs` | same | same fix |
| `remove.cs` | same | same fix |
| `reverse.cs` | same | same fix |
| `set.cs` | same | same fix |
| `sort.cs` | same | same fix |
| `split.cs` | same | same fix |
| `unique.cs` | same | same fix |

### Handlers NOT changed (auto-inference works correctly)
`join.cs` (already had `Type.String`), `contains.cs` (bool), `count.cs` (int), `indexof.cs` (int), `first.cs`/`last.cs`/`get.cs` (dynamic item type).

### Proof — Executable tests

**1. List type is preserved as "list"**
> Test: `Phase0Proof.Phase04_ListType_IsPreserved` (`Phase0Proof.cs:148`)
```
IN:  Data.Ok(new types.list { count=3, value=list }, Type.FromName("list"))
OUT: .Type.Value = "list"
```

**2. Scalar auto-inference still works**
> Test: `Phase0Proof.Phase04_ScalarType_AutoInferred` (`Phase0Proof.cs:164`)
```
IN:  Data.Ok(42)   // no explicit type
OUT: .Type.Value = "int"
```

**Additional coverage** (pre-existing tests): The existing `PLang.Tests/App/Modules/list/` tests exercise all list handlers end-to-end.

---

## Phase 0.5 — TString & CultureInfo

### What changed
1. Created `TString` with `%var%` resolution — `PLang/App/Memory/TString.cs`
2. Registered in TypeMapping as `"tstring"` / `"translatable"` — `PLang/App/Utility/TypeMapping.cs`
3. Added `CultureInfo Culture` to `PLangAppContext` — `PLang/App/Context/PLangAppContext.cs`

### TString design
- `Value`: raw template with `%var%` placeholders (e.g. `"hello %name%"`)
- `resolver`: `Func<string, object?>` — backed by Variables at runtime
- `ToString()`: resolves all `%var%` placeholders; no resolver → returns raw value
- Unresolved variables → kept as `%var%`
- Dot notation supported: `%user.name%`
- Edge cases handled: unclosed `%`, empty `%%`

### Proof — Variable resolution

All tests in `PLang.Tests/App/Memory/TStringTests.cs` and `PLang.Tests/App/Phase0Proof.cs`.

**1. No resolver → returns raw template**
> Test: `TStringTests.ToString_NoResolver_ReturnsRawValue` (`TStringTests.cs:24`)
> Test: `Phase0Proof.Phase05_TString_NoResolver_ReturnsRaw` (`Phase0Proof.cs:179`)
```
IN:  new TString("hello %name%").ToString()
OUT: "hello %name%"
```

**2. With resolver → resolves variables**
> Test: `TStringTests.ToString_WithResolver_ResolvesVariables` (`TStringTests.cs:31`)
> Test: `Phase0Proof.Phase05_TString_WithResolver_ResolvesVariables` (`Phase0Proof.cs:189`)
```
IN:  new TString("hello %name%", resolver: n => {"name":"John"}[n]).ToString()
OUT: "hello John"
```

**3. Multiple variables**
> Test: `TStringTests.ToString_MultipleVariables` (`TStringTests.cs:40`)
```
IN:  new TString("Hello %first% %last%!", resolver: {"first":"Jane","last":"Doe"}).ToString()
OUT: "Hello Jane Doe!"
```

**4. Unresolved variable → kept as-is**
> Test: `TStringTests.ToString_UnresolvedVariable_KeptAsIs` (`TStringTests.cs:53`)
> Test: `Phase0Proof.Phase05_TString_UnresolvedVar_KeptAsIs` (`Phase0Proof.cs:206`)
```
IN:  new TString("hello %name%, age %age%", resolver: {"name":"John"}).ToString()
OUT: "hello John, age %age%"
```

**5. Dot notation**
> Test: `TStringTests.ToString_DotNotation_PassedToResolver` (`TStringTests.cs:86`)
> Test: `Phase0Proof.Phase05_TString_DotNotation` (`Phase0Proof.cs:219`)
```
IN:  new TString("name: %user.name%", resolver: "user.name"->"Alice").ToString()
OUT: "name: Alice"
```

**6. Unclosed percent (edge case)**
> Test: `TStringTests.ToString_UnclosedPercent_PreservesAsIs` (`TStringTests.cs:78`)
> Test: `Phase0Proof.Phase05_TString_UnclosedPercent_Safe` (`Phase0Proof.cs:231`)
```
IN:  new TString("50% done", resolver: _ => null).ToString()
OUT: "50% done"
```

**7. Empty %% (edge case)**
> Test: `TStringTests.ToString_EmptyPercent_PreservesLiteral` (`TStringTests.cs:70`)
```
IN:  new TString("100%%", resolver: _ => null).ToString()
OUT: "100%%"
```

**8. Null resolver result → keeps placeholder**
> Test: `TStringTests.ToString_NullResolverResult_KeepsPlaceholder` (`TStringTests.cs:95`)
```
IN:  new TString("hello %missing%", resolver: _ => null).ToString()
OUT: "hello %missing%"
```

### Proof — Implicit conversions

**9. string → TString (implicit)**
> Test: `TStringTests.ImplicitFromString` (`TStringTests.cs:103`)
> Test: `Phase0Proof.Phase05_TString_ImplicitConversions` (`Phase0Proof.cs:241`)
```
IN:  TString ts = "hello";
OUT: ts.Value = "hello"
```

**10. TString → string (implicit)**
> Test: `TStringTests.ImplicitToString` (`TStringTests.cs:110`)
```
IN:  string s = new TString("hello");
OUT: s = "hello"
```

### Proof — TypeMapping registration

**11. Forward lookup**
> Test: `TStringTests.TypeMapping_ResolvesTString` (`TStringTests.cs:141`)
> Test: `Phase0Proof.Phase05_TypeMapping_RegistersTString` (`Phase0Proof.cs:252`)
```
IN:  TypeMapping.GetType("tstring")
OUT: typeof(App.Memory.TString)
```

**12. Alias lookup**
> Test: `TStringTests.TypeMapping_ResolvesTranslatable` (`TStringTests.cs:148`)
```
IN:  TypeMapping.GetType("translatable")
OUT: typeof(App.Memory.TString)
```

**13. Reverse lookup**
> Test: `TStringTests.TypeMapping_ReverseLookup` (`TStringTests.cs:155`)
```
IN:  TypeMapping.GetTypeName(typeof(TString))
OUT: "tstring"
```

### Proof — CultureInfo

**14. Defaults to InvariantCulture**
> Test: `Phase0Proof.Phase05_CultureInfo_DefaultsToInvariant` (`Phase0Proof.cs:268`)
```
IN:  new PLangAppContext("/app").Culture
OUT: CultureInfo.InvariantCulture
```

### Proof — Equality

**15. TString equals TString with same value**
> Test: `TStringTests.Equals_SameValue_True` (`TStringTests.cs:118`)
```
IN:  new TString("hello").Equals(new TString("hello"))
OUT: true
```

**16. TString equals plain string**
> Test: `TStringTests.Equals_String_True` (`TStringTests.cs:134`)
```
IN:  new TString("hello").Equals("hello")
OUT: true
```

### Note on v1 TString conflict
Old v1 `PLang.TString` at `PLang/Models/TString.cs` still exists. Tests use `using R2 = App.Memory;` and `R2.TString` to disambiguate.

---

## Test file index

| File | Tests | Covers |
|------|-------|--------|
| `PLang.Tests/App/Phase0Proof.cs` | 19 | All phases — integration proofs |
| `PLang.Tests/App/Errors/ErrorCategoryTests.cs` | 13 | Phase 0.2 — category classification + format |
| `PLang.Tests/App/Memory/TStringTests.cs` | 20 | Phase 0.5 — TString resolution, conversions, TypeMapping |
| `PLang.Tests/App/Core/ReturnTests.cs` | 6 | Phase 0.1 — Data.FromError (pre-existing, adapted) |
| `PLang.Tests/App/Memory/DataGenericTests.cs` | 1 | Phase 0.1 — Data<T>.FromError (pre-existing, adapted) |
| `PLang.Tests/App/Errors/ErrorInfoTests.cs` | 2 | Phase 0.2 — Format with FixSuggestion/Exception (adapted) |

**Total new tests**: 52
**Total adapted tests**: 9
**Final test suite**: 1130/1130 pass, 0 failures

## Summary

| Phase | What | Proof Tests |
|-------|------|-------------|
| 0.1 | `Data.Fail()` → `Data.FromError()` | `Phase0Proof.Phase01_*` (3) + `ReturnTests.Fail_*` (3) + `DataGenericTests` (1) |
| 0.2 | ErrorCategory enum + category-aware Format() | `ErrorCategoryTests.*` (13) + `Phase0Proof.Phase02_*` (4) |
| 0.3 | system/error/ goals + HTML templates | No C# tests (PLang goals + static HTML) |
| 0.4 | List handlers → explicit `Type.FromName("list")` | `Phase0Proof.Phase04_*` (2) + existing list tests |
| 0.5 | TString with %var% resolution + CultureInfo | `TStringTests.*` (20) + `Phase0Proof.Phase05_*` (8) |
