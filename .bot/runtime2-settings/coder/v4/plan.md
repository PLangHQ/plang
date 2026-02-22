# v4 Plan — Fix string→enum regression in Cast<T>

## 1. Handle string→enum in Cast<T>
- Before `Enum.ToObject`, try `Enum.TryParse(target, s, ignoreCase: true, out var parsed)`
- This makes string→enum actually work (returns correct value) instead of just catching the crash

## 2. Add ArgumentException to catch filter
- Safety net for any other `Enum.ToObject` failures not covered by TryParse

## 3. Add tests
- `Resolve_ConvertsStringToEnum` — "Fastest" → CompressionLevel.Fastest
- `Resolve_ConvertsStringToEnum_CaseInsensitive` — "fastest" → CompressionLevel.Fastest
- `Resolve_InvalidEnumString_ReturnsClassDefault` — "not-a-level" → fallback
