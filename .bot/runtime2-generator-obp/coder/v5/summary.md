# v5 — close security/v1 findings 1 + 3

## What this is

Security/v1 returned PASS but flagged four findings. Ingi triaged them:
fix #1 + #3, defer #2 (doc-only), close #4 (already mitigated). v5 closes
those two without touching the deferred / closed ones.

- **#1** (medium, info-disclosure) — `__SnapshotParams` emitted by both
  `Emission/Property/Data/this.cs` and `Emission/Property/Legacy/this.cs`
  captured `PrValue` and `FinalValue` verbatim into `Error.Params`,
  ignoring `[Sensitive]` on the property. Currently dormant (no handler
  uses `[Sensitive]` on a `Data<T>` param) but the first author who
  reaches for the attribute would leak plaintext to `Error.cs:215-226`'s
  log output. Same recurring shape as `Variables.Snapshot()`.
- **#3** (low, resource-exhaustion) — `Data.AsT_Impl` cycle and depth-bound
  branches both returned `ConvertAndWrap<T>(strVal, ctx)` — the unresolved
  `%var%`-bearing string flowed through silently. No signal to the caller
  that resolution failed. The security report listed only the depth case;
  the cycle branch had the same shape and is fixed in the same edit.

## What was done

### Finding 1 — `[Sensitive]` masking in snapshot emission

- `PLang.Generators/Discovery/this.cs` — `BuildProperty` now scans the
  property's attribute list for `SensitiveAttribute` (matched by name to
  match the existing `ProviderAttribute` / `VariableNameAttribute`
  convention) and threads the bool through to both record constructors.
- `PLang.Generators/Emission/Property/Data/this.cs` — record gains
  `IsSensitive`. `EmitSnapshotEntry` builds `prValueExpr` and
  `finalValueExpr` once, branching on `IsSensitive`:
  - sensitive `PrValue` → `__pr?.Value != null ? "******" : null`
    (preserves null when the .pr literal was absent — no false positive)
  - sensitive `FinalValue` → `{SetFlag} ? (object?)"******" : null`
- `PLang.Generators/Emission/Property/Legacy/this.cs` — same shape.
- Mirrors the convention `App/Channels/Serializers/SensitivePropertyFilter`
  already enforces during JSON serialization.

### Finding 3 — surface cycle / depth-trip as Data errors

- `PLang/App/Data/this.cs:413-454` — restructured the `%var%` branch:
  - cycle hit (HashSet `Add` returns false) → return
    `ServiceError("Cyclic %var% reference detected …", "VariableResolutionCycle", 400)`
    *before* the try/finally, since the outer frame still owns the entry —
    we must not Remove what we didn't Add.
  - depth-trip (`Count > ResolveDepthLimit`) → inside try/finally, return
    `ServiceError("Variable resolution exceeded depth limit ({32}) …", "ResolveDepthExceeded", 400)`.
    The finally Removes `strVal` (which we did Add this frame), keeping
    the set balanced.
- Distinct error keys so callers can tell cycle from depth-trip.

### Tests

- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` — four existing
  tests pinned the previous swallow-and-pass-through behavior:
  `AsT_CyclicVarReference_*`, `AsT_SelfReferencingVar_*`,
  `AsT_PartialMatchSelfReference_*`, `AsT_ExpandingCycle_*`. Renamed and
  rewritten to assert `result.Success == false` and the corresponding
  `Error.Key`. The "must not stack-overflow" guarantee is preserved.
- `PLang.Tests/Generator/Matrix/Snapshot/SensitiveHandlers.cs` — new matrix
  handler `SensitiveSnapshot` with `[Sensitive]` on a `Data<string> ApiKey`
  and a plain `Data<string> Endpoint`, fails on Run() to force snapshot.
- `PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs` — two new tests
  `SnapshotOnError_SensitiveProperty_MasksPrValueAndFinalValue` and
  `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue` (the
  null-guard branch — `PrValue=null` must stay `null`, not `"******"`).
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — record ctor calls
  updated for the new `IsSensitive` field (six call sites).

### Verification

- **Build**: green (one Windows target excluded; PLang + PLang.Tests
  build clean).
- **C# tests**: 2468/2468 passing (was 2466 — +2 new `[Sensitive]`
  snapshot tests; cycle/depth tests renamed in place).
- **Generated source spot-check**: `SensitiveSnapshot.Action.g.cs`
  emits the masked expression for `ApiKey` and the raw expression for
  `Endpoint` — confirms the per-property branch is wired correctly.

## Code example — the pattern

`PLang.Generators/Emission/Property/Data/this.cs` — the masked vs. raw
expressions are built once, then dropped into the same emission template:

```csharp
var prValueExpr = IsSensitive
    ? "__pr?.Value != null ? \"******\" : null"
    : "__pr?.Value";
var finalValueExpr = IsSensitive
    ? $"{SetFlag} ? (object?)\"******\" : null"
    : $"{SetFlag} ? (object?){Backing} : null";
sb.AppendLine($"                PrValue = {prValueExpr},");
sb.AppendLine($"                FinalValue = {finalValueExpr},");
```

`PLang/App/Data/this.cs` — the cycle branch returns before entering the
try, the depth-trip lives inside try so `finally` balances the Add:

```csharp
if (!_resolvingValues.Add(strVal))
{
    return @this<T>.FromError(new ServiceError(
        $"Cyclic %var% reference detected while resolving '{strVal}'.",
        "VariableResolutionCycle", 400));
}
try
{
    if (_resolvingValues.Count > ResolveDepthLimit)
    {
        return @this<T>.FromError(new ServiceError(
            $"Variable resolution exceeded depth limit ({ResolveDepthLimit}) at '{strVal}'.",
            "ResolveDepthExceeded", 400));
    }
    // … full-match / partial-match resolution …
}
finally
{
    _resolvingValues.Remove(strVal);
    if (isCycleRoot) _resolvingValues = null;
}
```

## What's deferred / closed

- **#2** — Documented as "leave it" in the close-out conversation.
  PLang is user-sovereign; flipping `Variables.Resolve` default behavior
  is broader than this PR. Doc tightening at most.
- **#4** — Closed: `Error.cs:280-296`'s `FormatVerboseValue` already caps
  string values at 200 chars (`s.Length > 200 ? s[..200] + "..."`),
  dict/list JSON at 300, ToString fallback at 200. The "add truncation"
  recommendation is already in place. The "honor `[Sensitive]` as a
  second line of defence" is now redundant with #1.

## Next bot

**codeanalyzer** — review the v5 delta (no architectural changes; surgical
fixes to two emission sites and one resolution branch, plus tests). After
that, **security** can re-check that #1 and #3 are honestly closed.
