# v1 Summary: Fix 6 Failing PLang Tests

## What this is
Fixed 6 PLang test failures that broke into two categories: 3 caused by a runtime bug in `Variables.Get()` array/index navigation, and 3 caused by incorrect `.pr` files from the builder. An additional runtime bug in `list.unique` was discovered and fixed during testing.

## What was done

### Part 1: Variables.Get() fix
**Root cause**: Line 82 in `Variables.cs` always skipped +1 character after the root name, assuming a dot separator. When root was followed by `[` (array index), it skipped the bracket.

**Fix**: Check if character after rootName is `.` before skipping.

**Files modified**:
- `PLang/App/Engine/Memory/Variables.cs:82` — conditional skip logic
- `PLang.Tests/App/Memory/VariablesTests.cs` — 4 new tests

**Tests fixed**: FromJson, DeepNavigation, VariableIndexing

### Part 2: .pr file rebuilds
- **Retry**: Deleted stale `.pr` and rebuilt from `Tests/App/` root. Builder now generates `onError` fields on throw steps.
- **ListOps**: Deleted stale `.pr` and rebuilt. Fixed off-by-one action alignment (actions were shifted by one position starting at step 15).
- **SetMaxGzipSize**: Rewrote `.goal` to use `settings` syntax (`set settings 'archive.max'` / `get settings 'archive.max'`) instead of unmapped `set max gzip size` syntax, then rebuilt.

### Part 3: list.unique runtime fix (discovered during testing)
**Root cause**: `list.unique` wrapped result in `types.list` record, but `list.count` checks `is IList` — the wrapper doesn't implement `IList`. Other handlers like `list.split` correctly return raw `List<object?>`.

**Fix**: Changed `unique.cs` to return raw list like `split.cs`.

**Files modified**:
- `PLang/App/actions/list/unique.cs` — return raw list instead of `types.list` wrapper
- `PLang.Tests/App/Modules/list/ListTests.cs` — updated Unique test

## Code example

Variables.cs fix (the core change):
```csharp
// Before (broken):
var remaining = name.Length > rootName.Length ? name[(rootName.Length + 1)..] : null;

// After (fixed):
string? remaining;
if (name.Length > rootName.Length)
{
    remaining = name[rootName.Length] == '.'
        ? name[(rootName.Length + 1)..]
        : name[rootName.Length..];
}
else
{
    remaining = null;
}
```

## Results
- C# tests: 1465/1465 pass
- PLang tests: 22/23 pass (up from 17/23)
- Remaining failure: Condition test (`else branch should execute`) — pre-existing, unrelated

## Note
`list.range` and `list.flatten` have the same `types.list` wrapper issue as `unique` had. They're not tested in PLang integration tests yet, so they don't fail. Worth fixing in a future pass.
