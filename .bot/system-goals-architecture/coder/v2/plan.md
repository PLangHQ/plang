# Plan v2 — Everything is Data: Phase 1 Implementation

## Scope
Phase 1 only: Rename leaf types + build navigator registry. This is the lowest-risk phase and proves the pattern before touching Engine/Goal/Step.

## Phase 1 Tasks

### 1a. Rename PathData → Path : Data<Path>
- `PLang/Runtime2/Engine/FileSystem/PathData.cs` → rename class to `Path`, keep in same file (or rename file)
- Update `Data<T>` base: `PathData : Data` → `Path : Data<Path>`
- Update all references across the codebase (GlobalUsings, imports, handlers)
- Existing PathData properties (Exists, Size, Extension, etc.) stay as-is — they're own properties

### 1b. Rename IdentityData → Identity : Data<Identity>
- `PLang/Runtime2/modules/identity/types.cs` — rename class
- Update all references

### 1c. Rename SettingsData → Settings : Data<Settings>
- `PLang/Runtime2/Engine/Settings/SettingsData.cs` — rename class
- Update all references

### 1d. Build Navigator Registry
- Create `PLang/Runtime2/Engine/Navigators/` directory
- `INavigator` interface: `object? Navigate(Data data, string key)`
- `NavigatorRegistry` on engine: `Dictionary<Type, INavigator>`, with `Get(Type)` and `Register<T>(INavigator)`
- Built-in navigators:
  - `ReflectionNavigator` — DeclaredOnly reflection (default for domain Data<T> types)
  - `ListNavigator` — index, .first, .last, .count
  - `DictionaryNavigator` — case-insensitive key lookup
  - `JsonNavigator` — JSON element traversal (may overlap with DictionaryNavigator)
  - `ClrReflectionNavigator` — fallback for non-Data objects (existing ValueNavigators behavior)
- Register defaults in engine constructor

### 1e. Wire GetChildValue to use navigators
- For `.` navigation: delegate to `engine.Navigators.Get(typeof(T))` or `Get(Value.GetType())`
- For `!` navigation: direct reflection on Data base properties
- Don't change the `.` vs `!` parsing yet (that's Phase 2) — just prepare the navigator plumbing

## What NOT to change (Phase 2+)
- Engine, Goal, Step, Action — stay as-is
- `.` vs `!` parsing in GetChild — Phase 2
- Source generator — Phase 4
- DataList<T> removal — Phase 5

## Risk Assessment
- Renaming is mechanical but touches many files
- Navigator registry is additive — doesn't break existing code
- The bridge: existing GetChildValue keeps working but delegates to navigators internally

## Build/Test
- `dotnet build` after each rename to catch all references
- Run `plang --test` from Tests/Runtime2 to verify 85+ tests still pass
- Any new failures = regression from rename, investigate immediately
