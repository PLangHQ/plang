# v5 Summary — Security hardening + test gaps

## What this is

Addresses security audit findings (unbounded recursion DoS vectors), auditor finding #9 (zip bomb untested), tester finding #3 (Merge untested), and security finding #9 (Verified settable without crypto). The user (Ingi) explicitly rejected findings #4 (system variable writes) and #5 (ObjectNavigator reflection) as by-design — PLang's trust model gives the user sovereignty over their runtime.

## What was done

### 1. Depth limits on all 5 recursive methods

| Location | Guard | Max |
|----------|-------|-----|
| `Data.cs:UnwrapJsonElement` | `int depth` param, throws `InvalidOperationException` | 128 |
| `Data.Envelope.cs:RehydrateNestedData` | `int depth` param, throws `InvalidDataException` | 128 |
| `Data.Navigation.cs:GetChild` | `int depth` param, returns null | 100 |
| `MemoryStack.cs:ResolveVariablesInPath` | `HashSet<string>` cycle detection (thread-static) | N/A (cycle, not depth) |
| `Types/this.cs:Clr()` | `int depth` param, returns null | 20 |

### 2. fromJson.cs deduplication

Deleted the duplicate `UnwrapJsonElement` from `fromJson.cs`. Now calls `Data.UnwrapJsonElement` (made `internal static`). One implementation, one fix point.

### 3. Verified → private set

Changed `Verified { get; set; }` to `Verified { get; private set; }`. Added `internal void SetVerified(bool value)` for future crypto verification. Prevents external code from claiming "verified" without cryptographic proof. Added `InternalsVisibleTo("PLang.Tests")` to PLang.csproj so tests can call `SetVerified()`.

### 4. Tests (12 new, 1384 total)

- **Zip bomb**: Compresses 110MB of zeros, decompresses, asserts `DecompressError` with StatusCode 500
- **JSON depth limit**: 200-level nested JSON, asserts `InvalidOperationException`
- **Navigation depth limit**: 150-level nested dict, asserts null return
- **Generic type depth limit**: 25-level `list<list<...>>`, asserts null return
- **Merge**: 3 tests — combine by name, null other, non-list silent drop
- **Verified**: Updated 2 existing tests to use `SetVerified()`, added default-null test
- **Decompress StatusCode**: 4 tests asserting `Error.StatusCode == 500`

### 5. Clr() behavior change

Removed the `?? typeof(object)` fallback in generic type parsing. Now `list<unknownType>` returns null instead of `List<object>`. This is more correct — returning a typed collection with wrong element type is worse than returning null. No existing tests depended on the old behavior.

## Files modified

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Memory/Data.cs` | Depth limit on UnwrapJsonElement/Object/Array, made `internal static` |
| `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` | Depth limit on RehydrateNestedData, Verified → private set + SetVerified() |
| `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` | Depth limit on GetChild |
| `PLang/Runtime2/Engine/Memory/MemoryStack.cs` | Cycle detection on ResolveVariablesInPath |
| `PLang/Runtime2/Engine/Types/this.cs` | Depth limit on Clr(), removed ?? typeof(object) fallback |
| `PLang/Runtime2/actions/convert/fromJson.cs` | Deleted duplicate UnwrapJsonElement, calls Data's version |
| `PLang/PLang.csproj` | Added InternalsVisibleTo for PLang.Tests |
| `PLang.Tests/Runtime2/Memory/DataTests.cs` | 12 new tests + 2 updated |
| `PLang.Tests/Runtime2/Types/EngineTypesTests.cs` | 1 new test |

## Code example

The depth limit pattern (same across all 5 locations):

```csharp
// Data.cs — UnwrapJsonElement
private const int MaxJsonDepth = 128;

internal static object? UnwrapJsonElement(object? value, int depth = 0)
{
    if (depth > MaxJsonDepth)
        throw new InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxJsonDepth})");
    // ... recurse with depth + 1 ...
}
```

The cycle detection pattern (different — graph, not tree):

```csharp
// MemoryStack.cs — ResolveVariablesInPath
[ThreadStatic]
private static HashSet<string>? _resolvingVars;

private string ResolveVariablesInPath(string path)
{
    _resolvingVars ??= new HashSet<string>();
    // In regex callback:
    if (!_resolvingVars.Add(varName))
        return match.Value; // Circular — leave unresolved
    try { /* resolve */ }
    finally { _resolvingVars.Remove(varName); }
}
```
