# v4 review summary — security/v1 verdict PASS

Security pushed `47ba8a96 security v1: PASS on runtime2-generator-obp — 1 medium, 3 low`.
Verdict: PASS, but the report listed four open findings.

## Findings reviewed by Ingi

| # | severity | category | call |
|---|----------|----------|------|
| 1 | medium | info-disclosure | **fix** — `__SnapshotParams` ignores `[Sensitive]` (same pattern as standing `Variables.Snapshot()` finding) |
| 2 | low | info-disclosure | **defer** — `Variables.Resolve(skipInfrastructure: false)` default; doc-only at most |
| 3 | low | resource-exhaustion | **fix** — `Data.AsT_Impl` swallows cycle + depth-trip; security report's term: "swallowing error is not good" |
| 4 | low | info-disclosure | **close** — `FormatVerboseValue` already truncates (200/300/200 chars at `Error.cs:280-296`); the proposed mitigation is already in place |

## Scope of v5

Findings 1 + 3, both surgical:
- **#1**: detect `[Sensitive]` in `Discovery.BuildProperty`, plumb as `IsSensitive` on `DataProperty` / `LegacyProperty` records, branch in `EmitSnapshotEntry` to mask `PrValue` / `FinalValue` with `"******"`.
- **#3**: replace the silent `ConvertAndWrap<T>(strVal, ctx)` returns at `Data/this.cs:417,420` with `@this<T>.FromError(new ServiceError(..., "VariableResolutionCycle" or "ResolveDepthExceeded", 400))`. Restructure the try/finally so the cycle path doesn't Remove an entry it didn't add.
