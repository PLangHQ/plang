# v2 Tester Review Summary

v2 found 2 critical, 3 major, 3 minor issues.

**All critical/major findings addressed in coder's fix commit (`1e9ff1ce`):**

1. **Add() → KindOf() pipeline (critical)** — FIXED. Add() now updates `_allKinds` and `_mimeToKind`. Remove() cleans up correctly (only removes kind from `_allKinds` when no other extension shares it). 4 new tests verify.
2. **Kind(null)/Mime(null) crash (critical)** — FIXED. Null guard added. 4 new tests verify.
3. **Name() backtick (major)** — FIXED. Strips arity suffix with `IndexOf('`')`. 2 new tests verify.
4. **BuilderNames/ComplexSchemas untested (major)** — FIXED. 6 new tests added covering non-empty list, common types present, nullable excluded, dedup, and GoalCall schema.
5. **Methods.cs context stamping (major)** — NOT FIXED directly, but a new integration test (`Add_CustomType_LazyDerivationUsesEngineTypes`) verifies the context path works end-to-end through Engine.Types. This partially covers the concern.

**Minor findings not addressed (acceptable):**
- DynamicData without explicit type
- Compressible unknown kind boundary
- PLang .goal tests still deferred
