# Coder — Stage 6: error.handle recovery-value tests

## What this stage is

Architect's `stage-6-error-handle-recovery-value-tests.md` carved three behavioral pins for `PLang/App/modules/error/handle.cs` recovery-value semantics:

1. **GoalFirst returns recovery's value** (handle.cs:113 — `return recoveryResult`)
2. **RetryFirst returns recovery's value after retry exhausts** (handle.cs:130 — the 2026-04-27 symmetry fix; before that it returned `Ok()` and the value was lost)
3. **Last action in a multi-action recovery chain is what propagates** (handle.cs:184 — `RunRecovery` returns `last`)

Behavior is already correct. Tests pin it against future regression.

## What I am writing

Three self-contained `.test.goal` files under `Tests/Errors/` (flat — matching the `Tests/App/CallStack/*.test.goal` convention, not the `SimpleRecovery/Start.goal` subfolder convention; the subfolder pattern leaves recovery side-effects implicit, the self-contained pattern co-locates the assert with the trigger and is the form used everywhere recovery semantics are pinned today).

| File | Pins |
|---|---|
| `Tests/Errors/GoalFirstReturnsRecoveryValue.test.goal` | handle.cs:113 — recovery runs in GoalFirst path, side-effect lands, `Handled=true` suppresses `%!error%` |
| `Tests/Errors/RetryFirstReturnsRecoveryValue.test.goal` | handle.cs:130 — after retry=1 exhausts, recovery runs, side-effect lands, `Handled=true` suppresses `%!error%` |
| `Tests/Errors/MultiActionRecoveryLastActionPropagates.test.goal` | handle.cs:177-184 — chain of three actions runs in order, each side-effect observable, terminal value is the chain's last action's |

## Trigger choice — `throw error` not `read 'missing.txt'`

Architect's design used `read 'missing.txt'` to provoke the error. I use `throw error "boom"` instead because:

- No filesystem dependency, no ordering issues with test isolation, no platform path quirks.
- The recovery semantic under test is independent of the *kind* of error. handle.cs:90-99 filters by Match, then dispatches. Both file-not-found and `throw` reach the same recovery path.
- Existing recovery tests in `Tests/App/CallStack/` (`HandledFlagSetWhenRecoverySucceeds`, `CauseLink`, …) all use `throw error "boom"` — same idiom, same coverage.

## Assertion strategy

Each test asserts two things:
1. **Side-effect** — the variable.set inside the recovery happened (`%content% equals "..."`). This proves the recovery body ran.
2. **`%!error% is null` outside the scope** — handle.cs:112 / :129 set `erroredCall.Handled = true` *in the same branch* that returns `recoveryResult`. If a regression replaces that with `return Ok()` (the exact pre-2026-04-27 bug), `Handled` would not be set and `%!error%` would still surface outside the recovery scope. So this assertion is the symmetry pin — not redundant with the side-effect assertion.

For test 3, intermediate-action observability is asserted too (`%first% equals "early"`, `%second% equals "middle"`, `%content% equals "final"`) to prove the chain executes in order, not just that the last action ran.

## Order parameter — explicit phrasing in the goal text

Per the GoalFirst `.pr` example in `Tests/Error/GoalFirst/.build/`, the builder inferred `Order=GoalFirst` from "call X, then retry" word order. To make this stage's tests robust against builder drift, I write the order **explicitly** in the goal text: `order GoalFirst` / `order RetryFirst, retry 1`. After `plang build`, I will read each `.pr` and verify the `Order` parameter and `RetryCount` parameter resolved as intended — if they didn't, the .goal text needs adjusting before the test is meaningful.

## Acceptance

- All three new `.test.goal` files green under `plang --test`.
- The `.pr` for each test has the expected `Order` (and for test 2, `RetryCount=1`) on the `error.handle` modifier.
- No existing test goes red (only pre-existing fail is `Code/HelloPlain.test.goal`, unrelated).
- C# suite stays green.

## Hand-off

After green, this branch is done — coder → codeanalyzer review next (no new modules/actions, so no builder handoff). Architect's summary already names the codeanalyzer step.
