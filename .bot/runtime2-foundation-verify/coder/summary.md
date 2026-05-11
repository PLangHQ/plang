# coder — runtime2-foundation-verify

## v1 — 2026-05-11 — Stage 6 landed: error.handle recovery-value tests

**What this is.** Architect's `stage-6-error-handle-recovery-value-tests.md` carved three behavioral pins for `PLang/App/modules/error/handle.cs` recovery-value semantics. The behavior is correct today; these `.test.goal` files pin it against future regression — in particular, the 2026-04-27 symmetry fix that made `Order=RetryFirst` return `recoveryResult` (with `Handled=true` on the errored Call) instead of `Ok()`.

**What was done.**

Three self-contained `.test.goal` files added under `Tests/Errors/` (flat — matching the `Tests/App/CallStack/*.test.goal` convention; the `SimpleRecovery/Start.goal` subfolder convention only carves the recovery, never asserts the post-state):

- `Tests/Errors/GoalFirstReturnsRecoveryValue.test.goal` — Order=GoalFirst path (handle.cs:109-114)
- `Tests/Errors/RetryFirstReturnsRecoveryValue.test.goal` — Order=RetryFirst, retry=1 path (handle.cs:120-131 — the symmetry fix)
- `Tests/Errors/MultiActionRecoveryLastActionPropagates.test.goal` — chain semantics, RunRecovery returns `last` (handle.cs:177-184)

Each test asserts two layers: (a) the side-effect inside the recovery (variable.set happened, observable as `%content% equals "..."`), and (b) `%!error% is null` outside the scope — which only holds if `erroredCall.Handled = true` ran, which only runs in the same branch as `return recoveryResult`. The `%!error%` assertion is the symmetry pin: if a future refactor regresses RetryFirst to `return Ok()`, the side-effect would still land but `%!error%` would still be set.

Build was non-deterministic on the first try for test 1 (LLM emitted three steps with index 0 — builder validation correctly rejected). Retry built clean.

**Trigger choice.** Used `throw error "boom"` rather than architect's `read 'missing.txt'` recipe — no filesystem dependency, no platform path quirks, identical recovery semantic. Same idiom as `Tests/App/CallStack/HandledFlagSetWhenRecoverySucceeds.test.goal` and siblings.

**Test results.**
- PLang `plang --test`: **203 total, 202 pass, 1 fail** (pre-existing `Code/HelloPlain.test.goal` from modules.code.run branch; not mine). Delta: +3 tests, +3 pass.
- C# `dotnet run --project PLang.Tests`: **2752/2752 pass**, unchanged.

**Code example — what the GoalFirst test looks like:**

```
TestGoalFirstReturnsRecoveryValue
/ Order=GoalFirst: when recovery's chain succeeds, handle.cs returns recoveryResult and sets Handled=true on the errored Call. Pinned by side-effect AND by %!error% null — they live in the same branch.

- throw error "boom", on error set %content% = "from-recovery", order GoalFirst
- assert %content% equals "from-recovery", "recovery's variable.set must have executed in GoalFirst path"
- assert %!error% is null, "Handled flag suppresses the error — proves the recoveryResult-return branch was taken"
```

The resulting `.pr` has `error.handle` modifier with `Order=GoalFirst` and a single-action `Actions=[variable.set Name=%content% Value=from-recovery]` chain. Same shape as the architect's brief.

**Hand-off.** No new modules or actions — straight to codeanalyzer review.
