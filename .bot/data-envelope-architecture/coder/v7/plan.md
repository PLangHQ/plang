# Coder v7 Plan — Cycle detection test + Clr boundary tests

## Goal
Address tester v8 findings. One blocking, two minor.

## Finding 1 (MAJOR, blocking): Cycle detection untested

`ResolveVariablesInPath` uses a `[ThreadStatic] HashSet<string> _resolvingVars` to detect circular variable references. If a variable name is already in the set, the `[varName]` bracket is left unresolved instead of recursing infinitely.

**Problem**: The cycle detection is structurally unreachable through the public API today. `GetValue(varName)` passes a simple variable name (extracted from brackets), and that name never contains brackets itself — so the inner `Get` call never triggers `ResolveVariablesInPath` again. The guard is defensive code.

**Test approach**: Use reflection to pre-seed the thread-static `_resolvingVars` field, simulating a circular reference already in progress. Then call `Get("items[idx]")` and verify the bracket is left unresolved (different result from normal resolution).

```csharp
// Normal: items[idx] → items[1] → "one"
// With cycle guard: items[idx] → [idx] left unresolved → navigation fails → null
```

**Files**: `PLang.Tests/App/Memory/VariablesTests.cs`

## Finding 2 (Minor): Clr() depth boundary at 20/21

Existing test nests 25 levels (well above MaxGenericDepth=20). Add boundary tests:
- 20 nestings → should resolve (exactly at limit)
- 21 nestings → should return null (one over limit)

**Files**: `PLang.Tests/App/Types/EngineTypesTests.cs`

## Finding 3 (Minor/observation): JsonDepthExceeded dead code

No action. Tester acknowledged it's defensive code that would activate if STJ's MaxDepth were raised. Leaving as-is.

## Summary of changes

| File | Change |
|------|--------|
| `PLang.Tests/App/Memory/VariablesTests.cs` | Add cycle detection test |
| `PLang.Tests/App/Types/EngineTypesTests.cs` | Add Clr() boundary tests at 20/21 |

No production code changes needed — tests only.
