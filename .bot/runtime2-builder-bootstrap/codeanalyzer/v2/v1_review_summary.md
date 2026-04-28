# v1 Review Summary

v1 (2026-04-27) of codeanalyzer on `runtime2-builder-bootstrap` returned **NEEDS WORK** with 10 findings on the new builder/diagnostics work:

**MAJOR (3)**
1. `DefaultBuilderProvider.cs` lines 171–198 — DiagGoal probe still walking every goal/step/action on every save (deletion-test win).
2. `TypeConverter.TryConvertTo` lines 266, 295 — throws `InvalidOperationException` from a `Try*` method, bypassing structured error handling.
3. Bare-catch sites swallow `Exception` without filter at 5 places: `TypeConverter:88,190`, `Variables/this:170`, `FluidProvider:140`, `Errors/Error:292`, plus `LazyParamsGenerator` emitting unfiltered catch into 118 generated handlers.

**MEDIUM (4)**
4. `Data.IsDeferredActionTemplate` matches on PLang type-name string — collides with user `[PlangType("action")]` aliases.
5. `Variables.Set` snapshot-clone catch silently falls back to alias mode — re-creates the bug it was added to fix.
6. `Actor.Context.@this` Clone vs CreateChild propagate different state (clone-family divergence).
7. `PlangTypeIndex.Reset()` only clears 2 of 4 caches; `_clrTypeFullNamesInitialized` non-volatile DCL fragile on ARM.

**MINOR (3)**
8. `error.handle.Wrap` — RetryFirst path returns bare `Ok()` while GoalFirst returns `recoveryResult`. Asymmetric.
9. Three "formal syntax" renderers (Catalog/`ExampleRenderer`, `FluidProvider`, builder/`DefaultBuilderProvider`) — same logic three places, will drift.
10. Culture-sensitive `ToString` on numbers/bools at `ExampleRenderer:105`, `FluidProvider:138`.

## Coder response (commit `80200746`)

Closed 8 of 10 findings:
- **#1, #2, #3, #4, #5, #6, #7, #8** addressed mechanically or by design fix.
- **#9, #10** explicitly deferred to a separate consolidation pass.

`Documentation/Runtime2/todos.md` updated with a follow-up entry to lock the recovery-value behavior with PLang tests (test gap, not a bug).

## v2 scope

This version verifies the 8 fixes are correct and behaviorally sound (Pass 4: are the fixes themselves new code that needs review?), and confirms the 2 deferred items are tracked. It does NOT re-review the broader branch — that review is locked at v1.
