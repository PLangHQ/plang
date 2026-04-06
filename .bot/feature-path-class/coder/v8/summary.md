# v8 Summary — Auditor v4: Save serialization exception gap + test consistency

## What this is

The auditor (v4) found that Save's exception filter only catches IO exceptions, not serialization exceptions from `JsonSerializer.SerializeAsync()`. Also flagged inconsistent StatusCode assertions and a duplicate test.

## What was done

### #1 Major — Save catches serialization exceptions

Added a second catch clause for `JsonException | NotSupportedException`:

```csharp
// Path.cs — Save method, after the existing IO catch:
catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
{
    return Data.FromError(new ServiceError(ex.Message, "SerializationError", 500));
}
```

Added test `Save_NonSerializableObject_ReturnsSerializationError` — creates a circular reference dictionary, verifies `SerializationError` key and 500 status.

### #3 Nit — Consistent StatusCode assertions

Added `await Assert.That(result.Error!.StatusCode).IsEqualTo(404)` to:
- `Copy_NotFound_ReturnsError`
- `Move_NotFound_ReturnsError`
- `Delete_NotFound_ReturnsError`

### #4 Nit — Removed duplicate test

Removed `Delete_NonEmptyDirectory_WithoutRecursive_ReturnsError` (weaker duplicate). Kept the stronger `Delete_NonEmptyDirectory_WithoutRecursive_ReturnsDirectoryNotEmpty` which checks Key + StatusCode.

### #2 Minor — Move non-atomic (skipped)

Not blocking. Industry-standard behavior. Noted for future improvement.

## Files modified

- `PLang/App/Engine/Memory/Path.cs` — added serialization catch clause
- `PLang.Tests/App/Modules/Path/PathTests.cs` — 1 new test, 3 strengthened assertions, 1 duplicate removed

## Verification

1239/1239 tests passing (net zero: +1 new, -1 duplicate removed).
