# v5 review — auditor v1 (PASS)

## Verdict
**PASS** — 1 minor + 3 nit, no critical or major. C# 2468/2468 green.

## Findings

### #1 (minor, cross-file) — Data<T> emission doesn't surface `As<T>` ServiceError
- **File:** `PLang.Generators/Emission/Property/Data/this.cs:32,36,40,44`
- **Issue:** v2 added cycle / depth-trip → `FromError(ServiceError)` in `Data.AsT_Impl`. Legacy emission honors it (`__Resolve<T>` sets `__resolutionError`, ExecuteAsync short-circuits at line 232 before Run). Data<T> emission does not — `__ResolveData(...).As<T>(Context)` is assigned straight to the backing field; if it returns FromError, the FromError-Data lives on the field with `Error` set but `Value=default(T)`. Run() reads `.Value` and proceeds.
- **Suggestion:** Mirror the legacy pattern — branch on `Success` after the As<T> call, route to `__resolutionError`. Single line per branch.
- **Auditor scope correction:** even with the property-side change, `__resolutionError` is only checked at line 232 (before Run). The Data<T> getter only fires *during* Run, so capturing into `__resolutionError` from the getter alone is dead. We also need a post-Run check in ExecuteAsync (`if (__resolutionError != null) return __resolutionError;` after `await Run()`).
- **Action:** close in v6.

### #2 (nit, review-gap) — Sensitive snapshot test misnamed
- **File:** `PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs:105`
- **Issue:** `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue` — handler always touches `ApiKey.Value` and `Endpoint.Value`, so `__X_set` is always true. Test pins the PrValue null-guard, NOT the unaccessed-and-sensitive `{SetFlag}` guard for FinalValue.
- **Action:** acknowledge in v6 plan; fix is either rename or add a non-touching sensitive handler. Cheap fix — do it.

### #3 (nit, contract) — Sensitive FinalValue masks even when backing is null
- **File:** `PLang.Generators/Emission/Property/Data/this.cs:60` (and Legacy line 82)
- **Issue:** `{SetFlag} ? (object?)"******" : null` masks regardless of whether the resolved backing value is null. Slight over-masking.
- **Action:** tighten to `{SetFlag} ? ({Backing} != null ? (object?)"******" : null) : null`. Cheap precision improvement, not blocking. Do it for symmetry with the PrValue null-guard.

### #4 (nit, architectural) — `SensitiveAttribute` matched by short name in Discovery
- **File:** `PLang.Generators/Discovery/this.cs:174`
- **Issue:** Same convention as ProviderAttribute / VariableNameAttribute matches. Theoretical only; no current namespace collision.
- **Action:** **leave**. Auditor explicitly says fixing this alone creates a different inconsistency. Worth tracking as a follow-up if/when we standardize attribute matching across the generator.

## Cross-bot agreement
- codeanalyzer/v3, tester/v4, security/v1 all approved their respective coder versions.
- security partial-disagreement: their #3 fix is half-completed (legacy surfaces, Data<T> doesn't). Auditor #1 captures that.
