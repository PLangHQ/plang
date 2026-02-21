# Scaffolder Plan — ISettings: Strongly Typed, Goal-Scoped Module Settings

## Overview

Translate the architect's ISettings design into compilable type skeletons and failing tests. The architect defined 4 phases; I scaffold Phases 1–3 (Foundation, Source Generator prep, First Use Case). Phase 4 (Builder Integration) is deferred.

## Key Design Decisions

### 1. File Placement & Naming

Following `@this` and OBP conventions:

| Type | Path | Namespace |
|------|------|-----------|
| `ISettings` marker interface | `PLang/Runtime2/Engine/Settings/ISettings.cs` | `PLang.Runtime2.Engine.Settings` |
| `SettingsScope` (scope stack) | `PLang/Runtime2/Engine/Settings/Scope.cs` | `PLang.Runtime2.Engine.Settings` |
| `Settings` (@this, engine-level registry) | `PLang/Runtime2/Engine/Settings/this.cs` | `PLang.Runtime2.Engine.Settings` |
| `ModuleView<T>` (context-bound view) | `PLang/Runtime2/Engine/Settings/ModuleView.cs` | `PLang.Runtime2.Engine.Settings` |
| `ArchiveSettings` (first use case) | `PLang/Runtime2/actions/archive/Settings.cs` | `PLang.Runtime2.actions.archive` |
| `settings` action handler | `PLang/Runtime2/actions/archive/settings.cs` | `PLang.Runtime2.actions.archive` |
| `types` for archive | `PLang/Runtime2/actions/archive/types.cs` | `PLang.Runtime2.actions.archive` |

### 2. OBP Decisions

- **`Settings` on Engine** — `engine.Settings` is the registry (@this in `Engine/Settings/this.cs`). It owns module lookup, scope management, and the engine-level default scope.
- **Navigate, don't pass** — handlers reach settings via `Context.Engine.Settings`. No settings passed as parameters.
- **`Scope` (not SettingsScope)** — OBP one-word noun. The scope stack is a property of `Settings`, not a standalone class on PLangContext. The architect wanted it on PLangContext but OBP says scope behavior belongs to the settings owner. **Compromise**: PLangContext gets a `Settings` dictionary (just storage), and `Engine.Settings` owns the resolution logic that walks context → parent → engine default → class default.
- **`ModuleView<T>`** — context-bound view returned by `engine.Settings.For<T>(context)`. Lightweight class (not struct — we need reference semantics for property resolution). This is the architect's `Module<T>()` concept, scoped to settings.
- **No `Module<T>()` on Engine** — The architect proposed `engine.Module<Archive>().Settings.Max` but there's no Module concept in Runtime2 yet, and creating one just for settings would be premature. Instead: `engine.Settings.For<ArchiveSettings>(context).Max`. When a proper Module system arrives, it can delegate to this.

### 3. Scope Mechanics

- **Per-context storage**: PLangContext gets a `Dictionary<string, object>? SettingsValues` property — raw key-value storage for settings set in the current goal scope.
- **Resolution chain**: `Engine.Settings` resolves by walking: `context.SettingsValues` → `context.Parent.SettingsValues` → ... → `Engine.Settings.Defaults` → class default.
- **Push/pop**: Goal.RunAsync saves/restores `context.SettingsValues` in the same try/finally pattern as `context.Goal` and `context.Step`. New goals start with null (inherit from parent via resolution chain). Settings handlers write to `context.SettingsValues` (lazy-initialized).
- **Engine defaults**: `Engine.Settings.Defaults` holds engine-level persistent values (written when `Default=true`).

### 4. Source Generator (skeleton only)

The source generator changes are **Phase 2** — I scaffold the interface (`ISettings`) and the shape of what the generator will produce, but I do NOT modify `LazyParamsGenerator.cs`. The coder will add generator logic. I produce:
- The `ISettings` marker interface that triggers generation
- Example of what generated code should look like (as comments/docs)
- The `settings` action handler for archive module (manually written — generator automates this later)

## Files to Create

### Skeletons (compilable, empty)

1. **`PLang/Runtime2/Engine/Settings/ISettings.cs`** — marker interface
2. **`PLang/Runtime2/Engine/Settings/this.cs`** — `@this` class: registry + resolution logic
3. **`PLang/Runtime2/Engine/Settings/Scope.cs`** — scope level (key-value store for one goal level)
4. **`PLang/Runtime2/Engine/Settings/ModuleView.cs`** — context-bound view `ModuleView<T>`

### Modifications to existing files

5. **`PLang/Runtime2/Engine/this.cs`** — add `Settings` property to Engine
6. **`PLang/Runtime2/Engine/Context/PLangContext.cs`** — add `SettingsValues` dictionary
7. **`PLang/Runtime2/Engine/Goals/Goal/Methods.cs`** — push/pop settings scope in RunAsync
8. **`PLang/Runtime2/GlobalUsings.cs`** — add alias for Settings @this

### First use case skeletons

9. **`PLang/Runtime2/actions/archive/Settings.cs`** — `ArchiveSettings : ISettings`
10. **`PLang/Runtime2/actions/archive/settings.cs`** — `[Action("settings")]` handler
11. **`PLang/Runtime2/actions/archive/types.cs`** — result types

### Failing Tests

12. **`PLang.Tests/Runtime2/Engine/Settings/SettingsTests.cs`** — C# tests for scope resolution, push/pop, defaults
13. **`PLang.Tests/Runtime2/Engine/Settings/ModuleViewTests.cs`** — C# tests for context-bound view
14. **`Tests/Runtime2/Settings/SetMaxGzipSize/Start.test.goal`** — PLang test: set setting → verify it took effect

## Test Strategy

### C# Tests (TUnit)

- `Resolve_ReturnsClassDefault_WhenNoScopeSet` — no settings written, resolve returns class default
- `Resolve_ReturnsGoalScopedValue_WhenSet` — settings handler writes to context, resolve finds it
- `Resolve_InheritsFromParentContext` — child context resolves from parent's settings values
- `Resolve_EngineDefaultOverridesClassDefault` — engine default set, no goal scope, returns engine default
- `Resolve_GoalScopeOverridesEngineDefault` — both engine and goal scope set, goal scope wins
- `Resolve_ResetsAfterGoalCompletes` — after goal pops, settings revert
- `ModuleView_ResolvesThroughContext` — ModuleView reads through context chain
- `ModuleView_DifferentContextsGetDifferentValues` — thread safety: two contexts, two different values

### PLang Test

- `- set max gzip size to 20mb` → `- get %setting%` → `- assert %setting% equals 20971520`
  *(This test will only fully pass after builder integration, but it defines the target.)*

## Sequence

1. Create `ISettings` marker interface
2. Create `Scope` (key-value store)
3. Create `Settings` @this (registry + resolution)
4. Create `ModuleView<T>` (context-bound view)
5. Add `Settings` property to Engine
6. Add `SettingsValues` to PLangContext
7. Add push/pop to Goal.RunAsync
8. Add global using alias
9. Create archive module skeletons
10. Create C# failing tests
11. Create PLang failing test
12. Verify compilation
