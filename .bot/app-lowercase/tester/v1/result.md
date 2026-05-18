# Tester v1 — Result

**Branch:** app-lowercase
**Date:** 2026-05-18
**Verdict:** PASS

## Test runs (clean rebuild from zero)

### C# (`dotnet run --project PLang.Tests`)

- Total: **2752**
- Passed: **2752**
- Failed: **0**
- Duration: ~14.7s

Identical to coder v1 baseline. Zero regressions from the rename + OBP
merges + foundation-verify merge.

### PLang (`cd Tests && plang --test`)

- `[Pass]` lines: **206**
- `[Fail]` lines: **6** — all pre-existing expected-fail fixtures
  (`_fixtures_sensitive/sensitivefail.fixture.goal` ×4,
   `_fixtures_fail/failsvar.fixture.goal` ×2). These intentionally fail
  as part of testing the test runner; coder v1 baseline documents them.

Delta vs baseline (203 pass): **+3** — exactly the three new
`Errors/*RecoveryValue*` tests merged in from runtime2-foundation-verify.

## Builder false-green check — 3 new `.pr` files

For each step in each new test, I read `text` and matched it against
`actions[*].module.action` (+ modifiers, since `error.handle` lands as
a modifier on `error.throw`):

### `Tests/Errors/.build/goalfirstreturnsrecoveryvalue.test.pr`

| Step | Text fragment | Actions / modifiers | Match? |
| ---- | --- | --- | --- |
| 0 | `throw error "boom", on error set %content% = ..., order GoalFirst` | `error.throw` + mod `error.handle` { Order=GoalFirst, Actions=[`variable.set`] } | ✓ |
| 1 | `assert %content% equals "from-recovery"` | `assert.equals` | ✓ |
| 2 | `assert %!error% is null` | `assert.isNull` | ✓ |

### `Tests/Errors/.build/retryfirstreturnsrecoveryvalue.test.pr`

| Step | Text fragment | Actions / modifiers | Match? |
| ---- | --- | --- | --- |
| 0 | `... order RetryFirst, retry 1` | `error.throw` + mod `error.handle` { RetryCount=1, Order=RetryFirst, Actions=[`variable.set`] } | ✓ |
| 1 | `assert %content% equals "from-recovery"` | `assert.equals` | ✓ |
| 2 | `assert %!error% is null` | `assert.isNull` | ✓ |

### `Tests/Errors/.build/multiactionrecoverylastactionpropagates.test.pr`

| Step | Text fragment | Actions / modifiers | Match? |
| ---- | --- | --- | --- |
| 0 | `throw ... set %first% = "early", set %second% = "middle", set %content% = "final"` | `error.throw` + mod `error.handle` { Actions=[`variable.set` ×3] } | ✓ |
| 1–4 | 3× `assert.equals` + 1× `assert.isNull` | matches the four assertions in the text | ✓ |

No builder shift, no false greens. All `error.handle` parameters
(`Order`, `RetryCount`, `Actions`) landed where the text said they should.

## Assertion quality

The architect comment in each `.test.goal` calls out the symmetry pin:
recovery's `variable.set` must have run (side-effect), AND `%!error%`
must be null after the scope (proves the handled-flag + recoveryResult
branch was taken, not `Ok()` without `Handled=true`).

Every test pins both halves:

- **GoalFirst:** `%content% == "from-recovery"` AND `%!error% is null`.
- **RetryFirst (retry exhausts):** same pair, different code path
  (handle.cs:120-131 vs 109-114).
- **MultiAction:** all three intermediate sets observable
  (`%first%`, `%second%`, `%content%`) AND `%!error% is null` — pins
  chain execution order, terminal-value propagation, and the success-return
  branch.

If the 2026-04-27 fix regressed (return `Ok()` without setting `Handled`),
`%!error%` would surface non-null and all three tests would fail. Deletion
test: if I removed `handle.cs:109-114` (or the symmetric 120-131), these
tests would catch it. No weak-assertion findings.

## Pre-existing stderr stack — namespace flip confirmed

Baseline noted a deserialize warning:

> Failed to deserialize List\`1 to this: ... `App.Goals.Goal.Steps.Step.Actions.Action.this`

Current output:

> Failed to deserialize List\`1 to this: ... `app.goals.goal.steps.step.actions.action.this`

The error's *shape* is unchanged (same path, same byte position); only
the namespace string lowercased — exactly the prediction in the coder
v1 baseline. The warning is pre-existing on `runtime2` and not surfaced
by the rename. Not a regression; not a finding for this version. If it's
ever to be fixed, that's its own ticket.

## Findings

None. No false greens, no missing assertions, no regressions, no weak
assertions on the new tests.

## Process notes

- Coder v3 has no `baseline-tests.md` (only v1 does). For this version
  (a merge-integration check, not a code change), the v1 baseline is the
  right comparison point — it captures the pre-rename state.
- I'm not raising the missing v3 baseline as a finding because v2/v3 were
  iterative reviews on the same rename, and the only meaningful baseline
  is pre-vs-post the entire rename arc, which v1 captures correctly.

## Verdict

**PASS.** The merge is safe to leave on `app-lowercase`. C# 2752/2752,
PLang 206/206 real passes (+6 expected fixtures), 3 new tests are
honestly green, namespace rename composes with the foundation-verify work
without surfacing any reflection-discovery surprises.
