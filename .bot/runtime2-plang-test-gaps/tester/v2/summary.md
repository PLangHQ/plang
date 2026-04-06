# Tester v2 — Validation of Coder v2 Fixes

## What this is
Re-validation after coder v2 addressed tester v1 findings: C# build failure fix, PrPath keying tests, convention discovery tests, and strict Goal.Path enforcement.

## Test Run Results

### C# Tests: 1509/1509 PASS
- Build failure resolved. All tests compile and pass.
- 9 new tests added (7 PrPath keying + 2 convention discovery).
- 60+ test sites updated to set `Path` on all goals.

### PLang Tests: 59/65 passed, 6 failed
Same failures as tester v1 — all are pre-existing and deferred:
- **ErrorProps** — missing onError in .pr (builder limitation)
- **ErrorInHandler** — missing onError in .pr (builder limitation)
- **RecursionDepthLimit** — missing onError in .pr (builder limitation)
- **ConditionCompound** — NullRef, expression evaluation not implemented (deferred)
- **CacheDynamicKey** — wrong assertion value in .pr (builder limitation)
- **Settings scaffolder** — unrelated test from another branch's .bot/ directory

No new failures. No regressions.

## Tester v1 Finding Resolution

| # | Severity | Finding | v1 Status | v2 Status |
|---|----------|---------|-----------|-----------|
| 1 | Critical | C# build failure (DiscoverAsync private) | FAIL | **FIXED** — 3 tests rewritten to use RunAsync |
| 2 | Critical | 3 PLang tests missing onError | FAIL | Deferred (builder) |
| 3 | Major | ConditionCompound NullRef | FAIL | Deferred (design decision) |
| 4 | Major | CacheDynamicKey wrong assertion | FAIL | Deferred (builder) |
| 5 | Major | Steps.RunAsync return value — no C# test | No test | **Deferred** — covered by PLang integration tests |
| 6 | Major | Goals PrPath keying — no C# test | No test | **FIXED** — 7 new tests |
| 7 | Minor | Setup convention discovery — no test | No test | **FIXED** — 2 new tests |
| 8 | Minor | Assertion error swallowing | Acceptable | Acceptable |

**Score: 3/5 actionable findings fixed. 5 deferred (3 builder, 1 design, 1 acceptable).**

## New Findings from v2 Review

### Finding 9 (Major): `Names` property includes Setup goals — inconsistent with Get()/All/Count

**Location:** `PLang/App/Engine/Goals/this.cs:197`

```csharp
public IEnumerable<string> Names => _goals.Values.Select(g => g.Name);
```

Every other public API on this class excludes setup goals:
- `Get()` — excludes setup (line 57)
- `All` — filters `!g.IsSetup` (line 212)
- `Count` — filters `!g.IsSetup` (line 217)
- `Value` — filters `!g.IsSetup` (line 207)

But `Names` returns ALL names including setup goals. This is an inconsistency introduced during the v2 fix (Names was changed from `_goals.Keys` to `_goals.Values.Select(g => g.Name)` but the setup filter was missed).

**Fix:** `public IEnumerable<string> Names => _goals.Values.Where(g => !g.IsSetup).Select(g => g.Name);`

**Test needed:** `Names_ExcludesSetupGoals`

### Finding 10 (Medium): Empty Path bypasses enforcement

**Location:** `PLang/App/Engine/Goals/this.cs:36` and `Goal/this.cs:45`

When `Path = ""`, `PrPath` computes to `".build\\.pr"` (not null, not empty). The enforcement check `string.IsNullOrEmpty(goal.PrPath)` passes, silently adding a garbage key.

**Fix (in Goal/this.cs):** Change `if (Path == null) return null;` to `if (string.IsNullOrEmpty(Path)) return null;`

**Test needed:** `Add_ThrowsWhenPathIsEmptyString`

### Finding 11 (Low): Semantic mislabeling in enforcement

The `Add()` method checks `goal.PrPath` but the error message says "must have a Path set". While functionally correct (PrPath derives from Path, so null PrPath implies null Path), checking `goal.Path` directly would be clearer:

```csharp
// Current (correct but indirect):
if (string.IsNullOrEmpty(goal.PrPath))
    throw new ArgumentException($"Goal '{goal.Name}' must have a Path set.");

// Clearer:
if (string.IsNullOrEmpty(goal.Path))
    throw new ArgumentException($"Goal '{goal.Name}' must have a Path set.");
```

## Verdict

```json
{
  "status": "pass-with-findings",
  "summary": "C# build fixed, 1509/1509 pass. PrPath keying and convention discovery well-tested. 3 new findings: Names inconsistency (major), empty Path bypass (medium), semantic mislabeling (low). PLang test failures are all pre-existing/deferred."
}
```

### Must fix before merge
1. **Finding 9**: Add setup filter to `Names` property
2. **Finding 10**: Guard empty Path in PrPath getter

### Should fix
3. **Finding 11**: Check `goal.Path` instead of `goal.PrPath` in Add() for clarity
