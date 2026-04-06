# v4 Summary — Fix string→enum regression in Cast<T>

## What this is
Fixes the regression introduced in v3: narrowing the `catch` in `Cast<T>` broke the string→enum path because `Enum.ToObject` throws `ArgumentException` for string values, which wasn't in the catch filter.

## What was done

### 1. String→enum handling via Enum.TryParse
- **File**: `PLang/App/Settings/this.cs` line 51-53
- Added `Enum.TryParse(target, s, ignoreCase: true, out var parsed)` before `Enum.ToObject`
- String→enum now actually works (returns correct value) instead of crashing or falling back

### 2. ArgumentException added to catch filter
- **File**: `PLang/App/Settings/this.cs` line 57
- Safety net for any `Enum.ToObject` failures not covered by TryParse (e.g., out-of-range int)

### 3. Tests added (3 new, 1268 total)
- `Resolve_ConvertsStringToEnum` — "Fastest" → CompressionLevel.Fastest
- `Resolve_ConvertsStringToEnum_CaseInsensitive` — "fastest" → CompressionLevel.Fastest
- `Resolve_InvalidEnumString_ReturnsClassDefault` — "not-a-level" → fallback to Optimal

## Code example

Before (crashes on string→enum):
```csharp
if (target.IsEnum) return (T)Enum.ToObject(target, value);
```

After (handles string, int, and invalid values):
```csharp
if (target.IsEnum)
{
    if (value is string s && Enum.TryParse(target, s, ignoreCase: true, out var parsed))
        return (T)parsed;
    return (T)Enum.ToObject(target, value);
}
```
