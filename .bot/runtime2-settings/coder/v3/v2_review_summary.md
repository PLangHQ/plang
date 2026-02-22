# v2 Review Summary

## Sources
- auditor-report.json (v2 review)
- security-report.json (v1)
- test-report.json (v2)

## Findings to Address

### Major: Clone() shares SettingsScope by reference (Auditor #1)
- `PLangContext.Clone()` copies `SettingsScope = SettingsScope` — reference copy
- Clone and original share the same `Scope` instance
- Writes to clone's scope pollute the original
- Fix: Add `Scope.Clone()`, use it in `PLangContext.Clone()`

### Minor: Cast<T> bare catch (Auditor #4, Security #3, Tester #1)
- `catch { return fallback; }` swallows all exceptions including critical ones
- Fix: Narrow to `catch (InvalidCastException) catch (FormatException) catch (OverflowException)`

## Accepted-Risk / Deferred (no action)
- Security #1: Scope dict unbounded — accepted-risk, user-sovereign
- Security #2: Parent chain O(depth) — iterative, mitigated by CallStack.MaxDepth
- Security #4: archive max=0 — user-sovereign
- Auditor #2: Disposable scope pattern — future-proofing, not urgent
- Auditor #3 / Tester #2: Simulation test — deferred, needs Goal construction infra
