# Coder v7 Summary — Cycle detection + Clr boundary tests

## What this is

Addresses tester v8 findings — the blocking cycle detection test gap and the minor Clr() depth boundary gap. Test-only changes, no production code modifications.

## What was done

### 1. Cycle detection test (Finding #1, MAJOR — was blocking)

Added `MemoryStackCycleDetectionTests` class with 2 tests:

- **`Get_CircularVariableReference_LeavesUnresolved`**: Uses reflection to pre-seed the thread-static `_resolvingVars` HashSet (simulating an active circular reference), then calls `Get("data.items[idx]")`. With "idx" already in the visited set, the cycle guard fires — `[idx]` is left unresolved → navigation fails → returns null. Contrasted with normal resolution which returns "one".

- **`Get_NormalVariableResolution_WorksAfterCycleCleanup`**: Verifies thread-static cleanup works correctly — two consecutive calls to `Get("data.items[idx]")` with different idx values both resolve correctly (no leftover state).

**Note**: The cycle detection is structurally unreachable through the current public API (GetValue passes simple names that never contain brackets), so it's defensive code. The test uses reflection to reach the guard.

### 2. Clr() depth boundary tests (Finding #2, minor)

Added 2 boundary tests to `EngineTypesTests`:

- **`Clr_ExactlyAtMaxDepth_Resolves`**: 20 nested `list<>` wrappings → resolves successfully
- **`Clr_OneOverMaxDepth_ReturnsNull`**: 21 nested `list<>` wrappings → returns null

### 3. Finding #3 (observation) — no action

`JsonDepthExceeded` catch in fromJson.cs is defensive dead code. Tester acknowledged it's not a bug. Left as-is.

## Files modified

| File | Change |
|------|--------|
| `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs` | Added `MemoryStackCycleDetectionTests` class (2 tests) |
| `PLang.Tests/Runtime2/Types/EngineTypesTests.cs` | Added 2 Clr() boundary tests |

## Test results

1394 pass, 0 fail (up from 1390 — 4 new tests).

## Code example

```csharp
[Test]
public async Task Get_CircularVariableReference_LeavesUnresolved()
{
    var stack = new MemoryStack();
    stack.Set("idx", 1);
    var data = new Dictionary<string, object?>
    {
        { "items", new List<object> { "zero", "one", "two" } }
    };
    stack.Set("data", data);

    // Normal resolution works
    var normalResult = stack.Get("data.items[idx]");
    await Assert.That(normalResult!.Value).IsEqualTo("one");

    // Pre-seed visited set via reflection → simulate cycle
    var field = typeof(MemoryStack).GetField("_resolvingVars",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    field!.SetValue(null, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "idx" });

    try
    {
        var cycleResult = stack.Get("data.items[idx]");
        await Assert.That(cycleResult).IsNull(); // cycle guard prevented resolution
    }
    finally
    {
        field.SetValue(null, null);
    }
}
```
