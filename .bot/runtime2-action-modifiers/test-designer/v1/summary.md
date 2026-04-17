# v1 Summary — Action Modifiers Test Suite

## What this is

Test contracts for the action modifiers feature — promoting `onError`/`cache`/`timeout` from step-level properties to per-action modifier actions using `IModifier.Wrap()` fold pattern. These tests define the behavioral spec that the coder implements against.

## What was done

Created 48 tests across 7 batches covering the full modifier system:

**C# tests (42 tests, 6 files):**
- `ModifierFoldTests.cs` — Core fold mechanics: 0/1/2/3 modifiers, nesting order, non-IModifier error handling
- `TimeoutAfterTests.cs` — timeout.after: success passthrough, timeout fires, cancellation propagation, 0ms edge case
- `CacheWrapTests.cs` — cache.wrap: miss/hit, failure skips cache, custom/default keys, sliding, __data__ restore
- `ErrorHandleTests.cs` — error.handle: ignore, filters (status/key/message), retry/goal ordering, retry success
- `ModifierRegistryTests.cs` — IsModifier/GetModifierOrder, Step.Clone with modifiers, Describe includes modifiers
- `GroupModifiersTests.cs` — Builder pipeline: flat→grouped, ordering, leading modifier edge case

**PLang tests (6 files):**
- Real step text with cache, error retry, timeout, modifier composition, per-action scope, error goal calls
- Timeout test stubbed (needs slow action mechanism — coder decides approach)

All C# test bodies use `Assert.Fail`-equivalent pattern. PLang tests have real step text ready for building.

**7 expected compile errors** — all reference `Action.Modifiers` property which the coder adds in Phase 1. This is by design.

## Code example

C# test pattern:
```csharp
[Test]
public async Task Handle_IgnoreError_SwallowsErrorReturnsOk()
{
    // IgnoreError = true -> action error is swallowed, returns Data.Ok()
    await Assert.That(true).IsFalse(); // Assert.Fail("Not implemented")
}
```

PLang test pattern:
```plang
TestCacheOnFileRead
/ Verifies that cache.wrap modifier caches a file.read result
- save 'testdata/cache_test.txt' with content 'cached content'
- read file 'testdata/cache_test.txt'
    cache for 60 seconds
    write to %content1%
- read file 'testdata/cache_test.txt'
    cache for 60 seconds
    write to %content2%
- assert %content1% equals %content2%
- delete file 'testdata/cache_test.txt'
```

## Status

All test files written. Ready for **coder** to implement Phase 1 (runtime infrastructure) and Phase 2 (builder pipeline) to make these tests pass.
