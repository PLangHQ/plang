# UI Module (Template Rendering) — Test Plan

## Source

Architect plan: `.bot/runtime2-builder-v2-template/architect/v1/plan.md`

## Summary

The UI module has one action (`render`) that renders Liquid templates via Fluid. Templates access PLang memory stack variables by default, support explicit parameter overrides, and can call PLang goals via `callGoal` and include partials via `render`/`include`. The provider is swappable via `ITemplateProvider`.

## Test Areas

### Batch 1: Core Render Behavior (C# — 6 tests)

Tests for the `Render` action's `Run()` method, covering the basic template rendering pipeline.

| # | Test Name | Intent |
|---|-----------|--------|
| 1 | `Render_InlineTemplate_SubstitutesVariables` | Inline template with `{{ name }}` resolves from memory stack |
| 2 | `Render_FileTemplate_ReadsAndRenders` | Template value is a file path — provider reads file and renders |
| 3 | `Render_MissingFile_ReturnsError` | File path that doesn't exist returns `Data` error, not exception |
| 4 | `Render_NullTemplate_ReturnsValidationError` | `[IsNotNull]` on Template — null returns validation error |
| 5 | `Render_EmptyTemplate_ReturnsEmptyString` | Empty string template renders to empty string |
| 6 | `Render_LiquidSyntaxError_ReturnsErrorWithPosition` | Malformed `{{ ` returns error with line/column info |

### Batch 2: Variable Resolution (C# — 4 tests)

Tests for how PLang variables flow into Liquid templates.

| # | Test Name | Intent |
|---|-----------|--------|
| 7 | `Render_MemoryStackVariables_AccessibleInTemplate` | Variables set in MemoryStack are available as `{{ varName }}` |
| 8 | `Render_ExplicitParams_OverrideMemoryStack` | Parameters dict overrides same-named memory stack variable |
| 9 | `Render_ExplicitParams_CreateAliases` | Parameters dict creates new names not in memory stack |
| 10 | `Render_ScopedVars_SkippedFromMemoryStack` | Variables prefixed with `!` are not loaded into template context |

### Batch 3: Custom Tags & Partials (C# — 4 tests)

Tests for `callGoal` custom tag and partial includes.

| # | Test Name | Intent |
|---|-----------|--------|
| 11 | `Render_CallGoal_ExecutesGoalAndInsertsResult` | `{% callGoal 'GoalName' %}` calls engine goal, inserts Data.Value |
| 12 | `Render_CallGoal_ErrorReturnsErrorData` | Goal call fails — error propagated back through template |
| 13 | `Render_Include_RendersPartialInline` | `{% include 'partial.html' %}` renders another template file |
| 14 | `Render_Include_InheritsVariables` | Included partial has access to parent's variables |

### Batch 4: Provider & Path Resolution (C# — 3 tests)

Tests for provider swapping and path resolution behavior.

| # | Test Name | Intent |
|---|-----------|--------|
| 15 | `Render_CustomProvider_IsUsed` | Swapped ITemplateProvider is called instead of FluidProvider |
| 16 | `Render_FilePathRelativeToGoalDir` | Template path resolves relative to calling goal's directory |
| 17 | `Render_FilePathAbsolute_ResolvesFromRoot` | Leading `/` resolves from engine root |

### Batch 5: PLang Integration Tests (5 tests)

Full pipeline tests: `.goal` → builder → `.pr` → GoalMapper → runtime.

| # | Test Name | Intent |
|---|-----------|--------|
| 18 | `RenderFile.test.goal` | `render 'template.html', write to %result%` — file template with variables |
| 19 | `RenderInline.test.goal` | `render %templateContent%, write to %result%` — inline content rendering |
| 20 | `RenderWithParams.test.goal` | `render 'page.html' with title=%pageTitle%, write to %html%` — explicit params |
| 21 | `RenderCallGoal.test.goal` | Template with `{% callGoal 'Helper' %}` — goal invocation from template |
| 22 | `RenderInclude.test.goal` | Template with `{% include 'partial.html' %}` — nested include |

## Totals

- **C# unit tests**: 17
- **PLang integration tests**: 5
- **Total**: 22

## File Locations

| Type | Path |
|------|------|
| C# tests | `PLang.Tests/Runtime2/Modules/ui/RenderTests.cs` |
| PLang tests | `Tests/Runtime2/Ui/` (one `.test.goal` + supporting files per test) |

## Notes for Coder

- FluidProvider needs `IPLangFileSystem` to read template files — navigate via `action.Context.Engine.FileSystem`
- `callGoal` needs engine access — navigate via `action.Context.Engine`
- Template variable loading: iterate `MemoryStack.GetAll()`, skip `!`-prefixed names
- For C# tests: mock `ITemplateProvider` for provider swap test; use real `FluidProvider` for all others
- For PLang tests: create fixture template files in test directories
