# Code Analyzer v4 — plan

## Trigger
Coder v2 (`c9d39b093`) landed fundamental changes responding to tester v1. User asked
for a review of "fundamental changes." Scope = diff `30aa4db46..HEAD` (since my v3 PASS).

## What changed (the fundamentals)
1. **`Data.Context` flipped non-null** (`_context = null!`, `Context` non-null). Internal
   `_context == null` guards stripped across `this.cs`/Navigation/Transport.
2. **`type.@this.Null` sentinel** replaces the historical `Data.Type == null`. `Data.Type`
   non-null end-to-end; Wire skips emission for the sentinel; Type setter clears `_type`
   on Null assignment.
3. **`Promote()` throws** on unstamped non-primitive fold reads (was silent `return this`).
   2-arg ctor sets `_foldLoaded = true` so primitive/catalog entities skip the throw.
4. **Producer stamping**: `Permission.Find` stamps Context on SQLite-rehydrated grants;
   `Sqlite.RehydrateValue` + `variable/set.ValidateBuild` route through `data.Type.ClrType`.
5. **Test-honesty fixes** F2–F7 (golden SHA256, F3 registry-only `path`, F1 back-ref flips,
   PLang `.goal` rewrites).

## Review passes
- **Pass 1 (OBP):** sentinel detection shape (string-magic vs reference equality), courier
  rules untouched.
- **Pass 4 (behavioral):** trace the non-null invariant — can a production read hit a null
  Context? Does the Promote throw fire during cache build (my v3 safety claim depended on
  short-circuit)? Full type surface for the fallback-drop.
- **Pass 4.5 (root vs symptom):** is the Null sentinel a real unify or a literal-name
  special-case? Is the throw root-cause or symptom?
- **Verify green is real:** rebuild clean, run the affected C# subsets myself (tester found
  false-greens — confirm coder v2 fixed, didn't re-green).

## Findings (see report.md)
- Promote throw is **safe** in cache build (every catalog entry uses 2-arg ctor →
  `_foldLoaded=true` → early return before the Context check). v3 safety claim holds.
- Build clean (0 errors). Affected subsets green: golden 2/2, nullability 7/7, DataTests 310/310.
- Minor/latent: `IsNull` string-magic (recommend `ReferenceEquals`); one test name overpromises
  ("ThrowsHard" asserts `IsNull`); `As(typeName)` fallback-drop asymmetry (no caller);
  `Scheme` getter lost null-safety.

## Verdict
PASS with minor notes. No blockers.
