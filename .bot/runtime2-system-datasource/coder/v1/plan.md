# Plan: Fix 6 Failing PLang Tests

## Part 1: Runtime Fix — Array/Index Navigation (3 tests)
Fix `Variables.Get()` line 82 — the `+1` skip assumes a dot separator after root name but when root is followed by `[`, it incorrectly skips the bracket character.

**Fix**: Check if the character after rootName is `.` before skipping it.

**File**: `PLang/App/Engine/Memory/Variables.cs`

**New C# tests**: 4 tests in `PLang.Tests/App/Memory/VariablesTests.cs`:
- `Get_ArrayIndexWithProperty_NavigatesCorrectly` — `arr[0].id`
- `Get_NestedArrayNavigation_NavigatesCorrectly` — `list[0].items[0].val`
- `Get_VariableIndex_ResolvesAndNavigates` — `items[idx]`
- `Get_DirectArrayIndex_NavigatesCorrectly` — `items[1]`

## Part 2: Rebuild .pr Files (3 tests)
- **Retry**: Missing `onError` field on throw steps — rebuild
- **ListOps**: Actions misaligned by one position (off-by-one builder bug) — rebuild
- **SetMaxGzipSize**: Wrong module mapping — rewrite goal to use `settings` syntax, then rebuild

## Verification
1. `dotnet test` — C# tests pass
2. Rebuild PlangConsole, rebuild affected .pr files
3. Run PLang tests to verify failure count drops
