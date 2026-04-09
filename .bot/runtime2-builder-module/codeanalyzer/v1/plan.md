# Code Analyzer v1 Plan — Builder Module

## Scope

Analyze all code changes on `runtime2-builder-module` vs `origin/runtime2-builder-v2`:

**New production files (builder module):**
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` (344 lines — core logic)
- `PLang/App/modules/builder/providers/IBuilderProvider.cs` (23 lines)
- `PLang/App/modules/builder/actions.cs`, `app.cs`, `appSave.cs`, `goals.cs`, `goalsSave.cs`, `merge.cs`, `validate.cs`, `types.cs` (all thin action handlers)
- `PLang/App/modules/builder/BuilderTypeInfo.cs` (3 lines)

**Modified engine files:**
- `PLang/App/Goals/Goal/this.cs` — `Parse()`, `MergeFrom()`, `ToText()` added
- `PLang/App/Goals/Goal/Methods.cs` — `FormatForLlm()` added
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — `Merge()`, `Clone()` added
- `PLang/App/Modules/this.cs` — `Describe()`, `GetDefaults()` added
- `PLang/App/Utility/Json.cs` — new centralized JSON options
- `PLang/App/Utility/TypeMapping.cs` — `ConvertTo<T>`, `GetBuilderTypeNames()`, `GetComplexTypeSchemas()` added
- `PLang/App/Providers/this.cs` — builder provider registered

**Test files:** 10 test classes, all read.

## 5-Pass Analysis

1. **Pass 1 — OBP Compliance**: Check all 5 OBP rules against builder module and modified engine code
2. **Pass 2 — Simplification**: Dead abstractions, over-parameterized methods, redundant code
3. **Pass 3 — Readability**: Naming, method length, class cohesion, flow clarity
4. **Pass 4 — Behavioral Reasoning**: Trace data origins, type surfaces, catch patterns, clone family
5. **Pass 5 — Deletion Test**: Which code paths lack test coverage?
